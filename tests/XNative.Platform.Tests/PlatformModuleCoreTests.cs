using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XNative.Core.Abstractions;
using XNative.Platform.FileSystem;
using XNative.Platform.Games;
using XNative.Platform.Logging;
using XNative.Platform.Processes;
using XNative.Platform.Storage;
using Xunit;

namespace XNative.Platform.Tests
{
    /// <summary>
    /// Pins the two platform registration shapes: the full desktop
    /// registration (<see cref="PlatformModule.AddXNativePlatform"/>)
    /// and the sandbox-safe core registration
    /// (<see cref="PlatformModule.AddXNativePlatformCore"/>), which
    /// must provide every platform service except
    /// <see cref="IProcessService"/> so a UWP head can supply its own
    /// honest implementation instead of composing
    /// <see cref="SystemProcessService"/>.
    /// </summary>
    public sealed class PlatformModuleCoreTests
    {
        [Fact]
        public void AddXNativePlatformCore_RegistersNoProcessService()
        {
            IServiceCollection services = new ServiceCollection()
                .AddXNativePlatformCore();

            Assert.DoesNotContain(
                services,
                descriptor => descriptor.ServiceType == typeof(IProcessService));
        }

        [Fact]
        public void AddXNativePlatform_RegistersTheDesktopProcessService()
        {
            IServiceCollection services = new ServiceCollection()
                .AddXNativePlatform();

            Assert.Contains(
                services,
                descriptor => descriptor.ServiceType == typeof(IProcessService)
                    && descriptor.ImplementationType == typeof(SystemProcessService));
        }

        [Fact]
        public void AddXNativePlatformCore_ResolvesEveryOtherPlatformService()
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddXNativePlatformCore()
                .BuildServiceProvider();

            Assert.IsType<SystemFileSystemService>(provider.GetRequiredService<IFileSystemService>());
            Assert.IsType<FileLogService>(provider.GetRequiredService<ILogService>());
            Assert.IsType<FileStorageService>(provider.GetRequiredService<IStorageService>());
            Assert.NotNull(provider.GetRequiredService<IGameCatalog>());
        }

        [Fact]
        public void AddXNativePlatformCore_ProcessServiceDoesNotResolve()
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddXNativePlatformCore()
                .BuildServiceProvider();

            Assert.Null(provider.GetService<IProcessService>());
        }

        [Fact]
        public void AddXNativePlatformCore_RegistersTheModuleDescriptor()
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddXNativePlatformCore()
                .BuildServiceProvider();

            IModuleDescriptor descriptor = provider.GetServices<IModuleDescriptor>().Single();

            Assert.Equal("xnative.platform", descriptor.Id);
        }

        [Fact]
        public void AddXNativePlatformCore_KeepsTheCatalogDefaultRoot()
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddXNativePlatformCore()
                .BuildServiceProvider();

            LocalGameCatalogOptions options =
                provider.GetRequiredService<IOptions<LocalGameCatalogOptions>>().Value;

            // The default root is the documents folder whenever the
            // platform resolves it; on hosts where it resolves empty
            // (e.g. Linux without XDG user directories) the module falls
            // back to LocalApplicationData/XNative/Games so the catalog
            // never ends up with an empty root.
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string expected = string.IsNullOrWhiteSpace(documents)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XNative",
                    "Games")
                : documents;

            Assert.False(string.IsNullOrWhiteSpace(options.RootPath));
            Assert.Equal(expected, options.RootPath);
        }

        [Fact]
        public void AddXNativePlatformCore_RejectsNullServices()
        {
            Assert.Throws<ArgumentNullException>(
                () => ((IServiceCollection)null!).AddXNativePlatformCore());
        }
    }
}
