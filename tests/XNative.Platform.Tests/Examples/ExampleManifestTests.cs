using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using XNative.Core.Devices;
using XNative.Core.Games;
using XNative.Platform.FileSystem;
using XNative.Platform.Games;
using Xunit;

namespace XNative.Platform.Tests.Examples
{
    /// <summary>
    /// Pins the worked game examples under <c>examples/games</c> against
    /// the real <see cref="LocalGameCatalog"/> running over the real
    /// <see cref="SystemFileSystemService"/>. The examples are the
    /// documentation users copy first; if the manifest format or the
    /// catalog's reading of it ever drifts, these tests fail instead of
    /// the examples silently rotting.
    /// </summary>
    public sealed class ExampleManifestTests : IDisposable
    {
        private readonly string _gamesRoot;

        public ExampleManifestTests()
        {
            var examplesGames = Path.Combine(FindRepositoryRoot(), "examples", "games");
            _gamesRoot = Path.Combine(
                Path.GetTempPath(),
                "XNative-ExampleManifestTests-" + Guid.NewGuid().ToString("N"));
            CopyDirectory(examplesGames, _gamesRoot);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_gamesRoot))
                {
                    Directory.Delete(_gamesRoot, recursive: true);
                }
            }
            catch (IOException)
            {
                // Best effort only: a leftover temp directory must not
                // fail an otherwise green test run.
            }
            catch (UnauthorizedAccessException)
            {
                // Same as above.
            }
        }

        [Fact]
        public async Task Catalogue_ContainsAllThreeExampleGames()
        {
            var games = await ScanExamplesAsync();

            Assert.Equal(3, games.Count);
            Assert.Contains(games, g => g.Name == "Full Featured Game");
            Assert.Contains(games, g => g.Name == "MinimalGame");
            Assert.Contains(games, g => g.Name == "BareGame");
        }

        [Fact]
        public async Task FullFeaturedGame_CarriesEveryManifestField()
        {
            var games = await ScanExamplesAsync();

            var game = Assert.Single(games, g => g.Name == "Full Featured Game");
            Assert.Equal(
                Path.Combine(_gamesRoot, "FullFeaturedGame", "game", "FullFeaturedGame.exe"),
                game.ExecutablePath);
            Assert.Equal(
                Path.Combine(_gamesRoot, "FullFeaturedGame", "game"),
                game.WorkingDirectory);
            Assert.Equal("-windowed -skipintro", game.LaunchArguments);

            Assert.NotNull(game.Metadata);
            Assert.Equal(
                "A full v2 manifest example that uses every field the catalog reads.",
                game.Metadata!.Description);
            Assert.Equal("Example Studio", game.Metadata.Publisher);
            Assert.Equal("1.2.3", game.Metadata.Version);

            var expectedIconPath = Path.Combine(_gamesRoot, "FullFeaturedGame", "icon.png");
            Assert.Equal(expectedIconPath, game.Metadata.IconPath);
            Assert.True(File.Exists(game.Metadata.IconPath), "The recorded icon path should point at the example icon.");
        }

        [Fact]
        public async Task MinimalGame_UsesFilenameDerivedNameAndDefaults()
        {
            var games = await ScanExamplesAsync();

            var game = Assert.Single(games, g => g.Name == "MinimalGame");
            Assert.Equal(
                Path.Combine(_gamesRoot, "MinimalGame", "MinimalGame.exe"),
                game.ExecutablePath);
            Assert.Null(game.LaunchArguments);
            Assert.Null(game.Metadata?.Description);
            Assert.Null(game.Metadata?.Publisher);
            Assert.Null(game.Metadata?.Version);
            Assert.Null(game.Metadata?.IconPath);
        }

        [Fact]
        public async Task BareGame_IsCataloguedFromBareExecutableScan()
        {
            var games = await ScanExamplesAsync();

            var game = Assert.Single(games, g => g.Name == "BareGame");
            Assert.Equal(
                Path.Combine(_gamesRoot, "BareGame", "BareGame.exe"),
                game.ExecutablePath);
            Assert.Null(game.Metadata);
            Assert.Null(game.LaunchArguments);
        }

        [Fact]
        public async Task PlaceholderExecutables_FallBackToUnknownArchitecture()
        {
            var games = await ScanExamplesAsync();

            // The example executables are plain-text placeholders, not PE
            // images. The catalog's honest fallback keeps them listed as
            // Windows executables with an unknown architecture instead
            // of dropping them or inventing an architecture.
            Assert.Equal(3, games.Count);
            foreach (var game in games)
            {
                Assert.Equal(ProcessorArchitecture.Unknown, game.Architecture);
                Assert.Equal(ExecutableType.WindowsExecutable, game.ExecutableType);
            }
        }

        private async Task<IReadOnlyCollection<GameInfo>> ScanExamplesAsync()
        {
            var options = Options.Create(new LocalGameCatalogOptions { RootPath = _gamesRoot });
            using (var catalog = new LocalGameCatalog(options, new SystemFileSystemService()))
            {
                return await catalog.GetGamesAsync();
            }
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "examples")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                "Could not locate the repository root: no ancestor of '" + AppContext.BaseDirectory +
                "' contains an 'examples' directory. The example tests must run from a build output " +
                "inside the X-Native repository.");
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)));
            }

            foreach (var subDirectory in Directory.GetDirectories(sourceDirectory))
            {
                CopyDirectory(
                    subDirectory,
                    Path.Combine(destinationDirectory, Path.GetFileName(subDirectory)));
            }
        }
    }
}
