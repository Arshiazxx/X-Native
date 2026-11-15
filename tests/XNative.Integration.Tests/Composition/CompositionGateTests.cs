using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using XNative.App;
using XNative.Core.Abstractions;
using XNative.UI.ViewModels;
using Xunit;

namespace XNative.Integration.Tests.Composition
{
    /// <summary>
    /// The composition gate: the assertions in this class describe the CORRECT
    /// end state of the wiring, written against
    /// <see cref="CompositionRoot.AddXNative"/> exactly the way the Xbox
    /// Bootstrapper builds its container (ValidateOnBuild + ValidateScopes).
    /// On the base commit (add7d6d) most of them fail — that is the point of
    /// the gate. The QA AUDIT.md records the base failures with their exact
    /// exception messages.
    /// </summary>
    public sealed class CompositionGateTests
    {
        private static readonly string[] ExpectedModuleIds =
        {
            "xnative.core",
            "xnative.platform",
            "xnative.graphics",
            "xnative.wine",
            "xnative.plugins",
            "xnative.ui",
        };

        private static ServiceProvider BuildValidatedProvider()
        {
            return new ServiceCollection()
                .AddXNative()
                .BuildServiceProvider(new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true,
                });
        }

        private static ServiceProvider BuildUnvalidatedProvider()
        {
            return new ServiceCollection()
                .AddXNative()
                .BuildServiceProvider();
        }

        [Fact]
        public void ValidatedContainer_BuildsWithoutError()
        {
            // Mirrors XNative.XboxHost.Bootstrap.Bootstrapper.Start(): with
            // ValidateOnBuild the container must be able to construct every
            // registered singleton at startup. On base this throws an
            // AggregateException rooted in LocalGameCatalog's unregistered
            // IFileSystemService dependency.
            using ServiceProvider provider = BuildValidatedProvider();

            Assert.NotNull(provider);
        }

        [Fact]
        public void ModuleDescriptors_EveryModuleResolves()
        {
            using ServiceProvider provider = BuildUnvalidatedProvider();

            string[] resolvedIds = provider.GetServices<IModuleDescriptor>()
                .Select(module => module.Id)
                .ToArray();

            foreach (string expectedId in ExpectedModuleIds)
            {
                Assert.Contains(expectedId, resolvedIds);
            }
        }

        [Fact]
        public void GameCatalog_Resolves()
        {
            using ServiceProvider provider = BuildUnvalidatedProvider();

            // On base, LocalGameCatalog is registered but its constructor
            // requires IFileSystemService, which nothing registers, so this
            // throws InvalidOperationException: the headline feature (the
            // game library) cannot be constructed in either head.
            Assert.NotNull(provider.GetService<IGameCatalog>());
        }

        [Fact]
        public void GameRunner_Resolves()
        {
            using ServiceProvider provider = BuildUnvalidatedProvider();

            Assert.NotNull(provider.GetService<IGameRunner>());
        }

        [Fact]
        public void FileSystemService_Resolves()
        {
            using ServiceProvider provider = BuildUnvalidatedProvider();

            // docs/architecture.md claims XNative.Platform implements this.
            // On base there is no implementation and no registration at all.
            Assert.NotNull(provider.GetService<IFileSystemService>());
        }

        [Fact]
        public void LogService_Resolves()
        {
            using ServiceProvider provider = BuildUnvalidatedProvider();

            // docs/architecture.md claims XNative.Platform implements this.
            Assert.NotNull(provider.GetService<ILogService>());
        }

        [Fact]
        public void StorageService_Resolves()
        {
            using ServiceProvider provider = BuildUnvalidatedProvider();

            // docs/architecture.md claims XNative.Platform implements this.
            Assert.NotNull(provider.GetService<IStorageService>());
        }

        [Fact]
        public void ProcessService_Resolves()
        {
            using ServiceProvider provider = BuildUnvalidatedProvider();

            // docs/architecture.md claims XNative.Platform implements this.
            Assert.NotNull(provider.GetService<IProcessService>());
        }

        [Fact]
        public void CompatibilityLayer_Resolves()
        {
            using ServiceProvider provider = BuildUnvalidatedProvider();

            // docs/architecture.md claims XNative.Wine implements this.
            Assert.NotNull(provider.GetService<ICompatibilityLayer>());
        }

        [Fact]
        public void GraphicsBackend_Resolves()
        {
            using ServiceProvider provider = BuildUnvalidatedProvider();

            // docs/architecture.md claims XNative.Graphics implements this.
            Assert.NotNull(provider.GetService<IGraphicsBackend>());
        }

        [Fact]
        public void PluginHost_Resolves()
        {
            using ServiceProvider provider = BuildUnvalidatedProvider();

            // docs/architecture.md claims XNative.Plugins implements this.
            Assert.NotNull(provider.GetService<IPluginHost>());
        }

        [Fact]
        public void GameProfileStore_Resolves()
        {
            using ServiceProvider provider = BuildUnvalidatedProvider();

            // IGameProfileStore's contract says it is implemented in
            // XNative.Wine; nothing registers it on base.
            Assert.NotNull(provider.GetService<IGameProfileStore>());
        }

        [Fact]
        public void GameLibraryViewModel_Resolves()
        {
            using ServiceProvider provider = BuildUnvalidatedProvider();

            // The Xbox MainPage resolves this right after startup; if the
            // catalog cannot be constructed, the page crashes instead of
            // showing the library.
            Assert.NotNull(provider.GetService<GameLibraryViewModel>());
        }

        [Fact]
        public void StartupViewModel_ResolvesFromAppComposition()
        {
            using ServiceProvider provider = BuildUnvalidatedProvider();

            // StartupViewModel additionally needs IDeviceInfoService, which
            // only the Xbox head registers. The development head
            // (XNative.App) registers none, so its own UI composition is
            // incomplete: resolving the startup view model here must work
            // once the console head gains a device-info registration.
            Assert.NotNull(provider.GetService<StartupViewModel>());
        }

        [Fact]
        public void ValidatedContainer_EveryGateServiceResolves()
        {
            using ServiceProvider provider = BuildValidatedProvider();

            Type[] gateServices =
            {
                typeof(IGameCatalog),
                typeof(IGameRunner),
                typeof(IFileSystemService),
                typeof(ILogService),
                typeof(IStorageService),
                typeof(IProcessService),
                typeof(ICompatibilityLayer),
                typeof(IGraphicsBackend),
                typeof(IPluginHost),
                typeof(IGameProfileStore),
            };

            var failures = new List<string>();
            foreach (Type serviceType in gateServices)
            {
                if (provider.GetService(serviceType) == null)
                {
                    failures.Add(serviceType.Name + " resolved to null");
                }
            }

            Assert.True(
                failures.Count == 0,
                "Unresolvable gate services: " + string.Join("; ", failures));
        }
    }
}
