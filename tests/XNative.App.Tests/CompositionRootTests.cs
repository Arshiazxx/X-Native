using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XNative.Core.Abstractions;
using XNative.Core.Devices;
using XNative.Platform.Games;
using XNative.UI.ViewModels;
using Xunit;

namespace XNative.App.Tests
{
    public sealed class CompositionRootTests
    {
        [Fact]
        public void Build_ResolvesGameCatalog()
        {
            using ServiceProvider provider = CompositionRoot.Build();

            Assert.NotNull(provider.GetService<IGameCatalog>());
        }

        [Fact]
        public void Build_ResolvesGameRunner()
        {
            using ServiceProvider provider = CompositionRoot.Build();

            Assert.NotNull(provider.GetService<IGameRunner>());
        }

        [Fact]
        public void Build_ResolvesFileSystemService()
        {
            using ServiceProvider provider = CompositionRoot.Build();

            Assert.NotNull(provider.GetService<IFileSystemService>());
        }

        [Fact]
        public void Build_ResolvesLogService()
        {
            using ServiceProvider provider = CompositionRoot.Build();

            Assert.NotNull(provider.GetService<ILogService>());
        }

        [Fact]
        public void Build_ResolvesStorageService()
        {
            using ServiceProvider provider = CompositionRoot.Build();

            Assert.NotNull(provider.GetService<IStorageService>());
        }

        [Fact]
        public void Build_ResolvesProcessService()
        {
            using ServiceProvider provider = CompositionRoot.Build();

            Assert.NotNull(provider.GetService<IProcessService>());
        }

        [Fact]
        public void Build_ResolvesDeviceInfoService()
        {
            using ServiceProvider provider = CompositionRoot.Build();

            IDeviceInfoService service = provider.GetRequiredService<IDeviceInfoService>();

            Assert.IsType<DesktopDeviceInfoService>(service);
        }

        [Fact]
        public void Build_ResolvesStartupViewModel()
        {
            using ServiceProvider provider = CompositionRoot.Build();

            Assert.NotNull(provider.GetService<StartupViewModel>());
        }

        [Fact]
        public void Build_ResolvesGameLibraryViewModel()
        {
            using ServiceProvider provider = CompositionRoot.Build();

            Assert.NotNull(provider.GetService<GameLibraryViewModel>());
        }

        [Fact]
        public void Build_WithGamesRoot_ConfiguresCatalogRootPath()
        {
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xnative-root-check");

            using ServiceProvider provider = CompositionRoot.Build(root);

            LocalGameCatalogOptions options = provider.GetRequiredService<IOptions<LocalGameCatalogOptions>>().Value;

            Assert.Equal(root, options.RootPath);
        }

        [Fact]
        public void Build_WithoutGamesRoot_KeepsPlatformDefaultRootPath()
        {
            using ServiceProvider provider = CompositionRoot.Build();

            LocalGameCatalogOptions options = provider.GetRequiredService<IOptions<LocalGameCatalogOptions>>().Value;

            Assert.False(string.IsNullOrWhiteSpace(options.RootPath));
        }

        [Fact]
        public void DesktopDeviceInfoService_ReturnsHonestDeviceInfo()
        {
            var service = new DesktopDeviceInfoService();

            DeviceInfo info = service.GetDeviceInfo();

            Assert.False(string.IsNullOrWhiteSpace(info.DeviceFamily));
            Assert.False(string.IsNullOrWhiteSpace(info.OperatingSystemVersion));
            Assert.False(string.IsNullOrWhiteSpace(info.Architecture));
            Assert.False(string.IsNullOrWhiteSpace(info.Manufacturer));
            Assert.False(string.IsNullOrWhiteSpace(info.Model));
            Assert.Equal(RuntimeInformation.ProcessArchitecture.ToString(), info.Architecture);
            Assert.Equal(Environment.MachineName, info.Model);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Equal("Windows.Desktop", info.DeviceFamily);
            }
            else
            {
                Assert.NotEqual("Windows.Desktop", info.DeviceFamily);
                Assert.Equal(RuntimeInformation.OSDescription, info.DeviceFamily);
            }
        }

        [Fact]
        public void DesktopDeviceInfoService_CachesDeviceInfo()
        {
            var service = new DesktopDeviceInfoService();

            DeviceInfo first = service.GetDeviceInfo();
            DeviceInfo second = service.GetDeviceInfo();

            Assert.Same(first, second);
        }
    }
}
