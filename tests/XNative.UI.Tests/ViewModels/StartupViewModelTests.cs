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
        public void LoadDoesNotReportHeadSpecificModules()
        {
            // "xnative.xboxhost" belongs to the Xbox head only; the shared
            // startup view model must not list it even when a head supplies it.
            IEnumerable<IModuleDescriptor> modules = AllModules()
                .Concat(new IModuleDescriptor[] { new StubModule("xnative.xboxhost", "Xbox Host") });

            StartupViewModel viewModel = CreateViewModel(modules);
            viewModel.Load();

            Assert.Equal(6, viewModel.Modules.Count);
            Assert.DoesNotContain(viewModel.Modules, module => module.Id == "xnative.xboxhost");
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
