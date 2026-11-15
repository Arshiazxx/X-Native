using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;
using XNative.Platform.FileSystem;
using XNative.Platform.Logging;
using XNative.Platform.Processes;
using XNative.Platform.Storage;
using Xunit;

namespace XNative.Platform.Tests
{
    public sealed class PlatformServicesRegistrationTests
    {
        [Fact]
        public void AddXNativePlatform_ResolvesAllPlatformServices()
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddXNativePlatform()
                .BuildServiceProvider();

            Assert.IsType<SystemFileSystemService>(provider.GetRequiredService<IFileSystemService>());
            Assert.IsType<FileLogService>(provider.GetRequiredService<ILogService>());
            Assert.IsType<FileStorageService>(provider.GetRequiredService<IStorageService>());
            Assert.IsType<SystemProcessService>(provider.GetRequiredService<IProcessService>());
        }

        [Fact]
        public void AddXNativePlatform_GameCatalogIsConstructible()
        {
            // Regression test for the P0 bug: IGameCatalog depended on
            // IFileSystemService, which previously had no implementation
            // or registration, so no composition root could build it.
            using ServiceProvider provider = new ServiceCollection()
                .AddXNativePlatform()
                .BuildServiceProvider();

            IGameCatalog catalog = provider.GetRequiredService<IGameCatalog>();

            Assert.NotNull(catalog);
        }
    }
}
