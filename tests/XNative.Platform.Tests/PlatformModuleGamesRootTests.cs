using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XNative.Platform.Games;
using Xunit;

namespace XNative.Platform.Tests
{
    /// <summary>
    /// Pins the default game catalog root rule shared by both platform
    /// registrations: the documents folder when the platform resolves
    /// it, otherwise the LocalApplicationData/XNative/Games fallback —
    /// never an empty root. (On hosts without XDG user directories,
    /// e.g. a bare Linux root shell, <see cref="Environment.SpecialFolder.MyDocuments"/>
    /// resolves to <see cref="string.Empty"/>; before the fallback
    /// existed, the catalog silently scanned nothing there.)
    /// </summary>
    public sealed class PlatformModuleGamesRootTests
    {
        [Fact]
        public void AddXNativePlatform_SetsTheDefaultGamesRoot()
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddXNativePlatform()
                .BuildServiceProvider();

            LocalGameCatalogOptions options =
                provider.GetRequiredService<IOptions<LocalGameCatalogOptions>>().Value;

            Assert.False(string.IsNullOrWhiteSpace(options.RootPath));
            Assert.Equal(ExpectedDefaultGamesRoot(), options.RootPath);
        }

        [Fact]
        public void AddXNativePlatformCore_SetsTheDefaultGamesRoot()
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddXNativePlatformCore()
                .BuildServiceProvider();

            LocalGameCatalogOptions options =
                provider.GetRequiredService<IOptions<LocalGameCatalogOptions>>().Value;

            Assert.False(string.IsNullOrWhiteSpace(options.RootPath));
            Assert.Equal(ExpectedDefaultGamesRoot(), options.RootPath);
        }

        private static string ExpectedDefaultGamesRoot()
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrWhiteSpace(documents)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XNative",
                    "Games")
                : documents;
        }
    }
}
