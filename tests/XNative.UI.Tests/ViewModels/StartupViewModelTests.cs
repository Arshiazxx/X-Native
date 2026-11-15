using System.Collections.Generic;
using System.Linq;
using XNative.Core;
using XNative.Core.Abstractions;
using XNative.Core.Devices;
using XNative.UI.ViewModels;
using Xunit;

namespace XNative.UI.Tests.ViewModels
{
    public sealed class StartupViewModelTests
    {
        private sealed class StubModule : IModuleDescriptor
        {
            public StubModule(string id, string displayName)
            {
                Id = id;
                DisplayName = displayName;
            }

            public string Id { get; }

            public string DisplayName { get; }
        }

        private sealed class StubDeviceInfoService : IDeviceInfoService
        {
            private readonly DeviceInfo _deviceInfo;

            public StubDeviceInfoService(DeviceInfo deviceInfo) => _deviceInfo = deviceInfo;

            public DeviceInfo GetDeviceInfo() => _deviceInfo;
        }

        private static IReadOnlyList<IModuleDescriptor> AllModules() => new IModuleDescriptor[]
        {
            new CoreModuleDescriptor(),
            new StubModule("xnative.platform", "Platform"),
            new StubModule("xnative.graphics", "Graphics"),
            new StubModule("xnative.plugins", "Plugins"),
            new StubModule("xnative.wine", "Wine"),
            new StubModule("xnative.ui", "Presentation"),
        };

        private static StartupViewModel CreateViewModel(IEnumerable<IModuleDescriptor> modules, DeviceInfo? deviceInfo = null)
        {
            return new StartupViewModel(modules, new StubDeviceInfoService(deviceInfo ?? DeviceInfo.CreateUnknown()));
        }

        [Fact]
        public void LoadReportsEveryModuleInTheExpectedOrder()
        {
            StartupViewModel viewModel = CreateViewModel(AllModules());

            viewModel.Load();

            Assert.Equal(
                new[] { "xnative.core", "xnative.platform", "xnative.graphics", "xnative.plugins", "xnative.wine", "xnative.ui" },
                viewModel.Modules.Select(module => module.Id));
            Assert.All(viewModel.Modules, module => Assert.True(module.IsLoaded));
            Assert.All(viewModel.Modules, module => Assert.EndsWith("Loaded \u2713", module.Line));
        }

        [Fact]
        public void LoadReportsAllSixSharedModulesAsLoaded()
        {
            // Exactly the six modules both composition roots register:
            // core, platform, graphics, plugins, wine and ui. The UI module
            // must be reported, otherwise the startup screen would claim
            // the very module rendering it is missing.
            StartupViewModel viewModel = CreateViewModel(AllModules());

            viewModel.Load();

            Assert.Equal(6, viewModel.Modules.Count);
            Assert.All(viewModel.Modules, module => Assert.True(module.IsLoaded));
            ModuleStatusViewModel ui = viewModel.Modules.Single(module => module.Id == "xnative.ui");
            Assert.Equal("Presentation: Loaded \u2713", ui.Line);
        }

        [Fact]
        public void LoadReportsHeadSpecificModulesAsLoadedAfterTheSharedModules()
        {
            // "xnative.xboxhost" belongs to the Xbox head only, but that
            // head's startup screen must still list it: a module a head
            // registers on top of the shared set is reported after the
            // shared modules, as loaded. (It is deliberately not part of
            // the expected order — see the next test.)
            IEnumerable<IModuleDescriptor> modules = AllModules()
                .Concat(new IModuleDescriptor[] { new StubModule("xnative.xboxhost", "Xbox Host") });

            StartupViewModel viewModel = CreateViewModel(modules);
            viewModel.Load();

            Assert.Equal(7, viewModel.Modules.Count);
            Assert.All(viewModel.Modules, module => Assert.True(module.IsLoaded));
            ModuleStatusViewModel host = viewModel.Modules[6];
            Assert.Equal("xnative.xboxhost", host.Id);
            Assert.Equal("Xbox Host: Loaded \u2713", host.Line);
        }

        [Fact]
        public void LoadNeverReportsAnAbsentHeadSpecificModuleAsMissing()
        {
            // The console composition registers exactly the six shared
            // modules. Because "xnative.xboxhost" is not in the expected
            // order, its absence must produce no entry at all — above
            // all not a "Missing" one on a head that never had it.
            StartupViewModel viewModel = CreateViewModel(AllModules());

            viewModel.Load();

            Assert.Equal(6, viewModel.Modules.Count);
            Assert.DoesNotContain(viewModel.Modules, module => module.Id == "xnative.xboxhost");
            Assert.All(viewModel.Modules, module => Assert.True(module.IsLoaded));
        }

        [Fact]
        public void LoadReportsAdditionalModulesInSuppliedOrderWithoutDuplicates()
        {
            // Additional modules keep the order the composition supplied
            // them in, and a descriptor registered twice (or differing
            // only by ID casing) is reported once — the same first-wins
            // rule the shared modules get.
            IEnumerable<IModuleDescriptor> modules = AllModules()
                .Concat(new IModuleDescriptor[]
                {
                    new StubModule("xnative.xboxhost", "Xbox Host"),
                    new StubModule("xnative.extras", "Extras"),
                    new StubModule("XNative.XboxHost", "Xbox Host Again"),
                });

            StartupViewModel viewModel = CreateViewModel(modules);
            viewModel.Load();

            Assert.Equal(
                new[]
                {
                    "xnative.core", "xnative.platform", "xnative.graphics",
                    "xnative.plugins", "xnative.wine", "xnative.ui",
                    "xnative.xboxhost", "xnative.extras",
                },
                viewModel.Modules.Select(module => module.Id));
            Assert.All(viewModel.Modules, module => Assert.True(module.IsLoaded));
        }

        [Fact]
        public void LoadReportsAMissingModule()
        {
            IEnumerable<IModuleDescriptor> modules = AllModules().Where(module => module.Id != "xnative.wine");

            StartupViewModel viewModel = CreateViewModel(modules);
            viewModel.Load();

            ModuleStatusViewModel wine = viewModel.Modules.Single(module => module.Id == "xnative.wine");

            Assert.False(wine.IsLoaded);
            Assert.Equal("Wine: Missing \u2717", wine.Line);
        }

        [Fact]
        public void LoadProjectsTheDeviceInformation()
        {
            DeviceInfo deviceInfo = new DeviceInfo("Windows.Xbox", "10.0.19041.0", "X64", "Microsoft", "Xbox Series X");

            StartupViewModel viewModel = CreateViewModel(AllModules(), deviceInfo);
            viewModel.Load();

            Assert.True(viewModel.DeviceInfo.IsXbox);
            Assert.Contains("Windows.Xbox", viewModel.DeviceInfoText);
            Assert.Contains("Xbox Series X", viewModel.DeviceInfoText);
        }

        [Fact]
        public void LoadIsIdempotent()
        {
            StartupViewModel viewModel = CreateViewModel(AllModules());

            viewModel.Load();
            viewModel.Load();

            Assert.Equal(6, viewModel.Modules.Count);
            Assert.False(viewModel.IsBusy);
        }
    }
}
