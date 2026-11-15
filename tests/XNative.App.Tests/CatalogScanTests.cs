using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;
using Xunit;

namespace XNative.App.Tests
{
    public sealed class CatalogScanTests : IDisposable
    {
        private readonly string _root;

        public CatalogScanTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(),
                "xnative-app-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup of the temporary scan root.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup of the temporary scan root.
            }
        }

        [Fact]
        public async Task Build_WithRoot_ScanReturnsExecutableAndManifestGames()
        {
            // A loose executable at the root: the catalog lists it by
            // file name, no manifest required.
            File.WriteAllBytes(Path.Combine(_root, "game.exe"), Array.Empty<byte>());

            // A subfolder game described by game.json. The manifest
            // only counts when its executable actually exists.
            string manifestFolder = Path.Combine(_root, "ManifestGame");
            Directory.CreateDirectory(manifestFolder);
            File.WriteAllBytes(Path.Combine(manifestFolder, "launcher.exe"), Array.Empty<byte>());
            File.WriteAllText(
                Path.Combine(manifestFolder, "game.json"),
                "{\"name\": \"Manifest Game\", \"executable\": \"launcher.exe\"}");

            using ServiceProvider provider = CompositionRoot.Build(_root);
            IGameCatalog catalog = provider.GetRequiredService<IGameCatalog>();

            var games = await catalog.GetGamesAsync();

            Assert.Equal(2, games.Count);
            Assert.Contains(games, game => game.Name == "game");
            Assert.Contains(games, game => game.Name == "Manifest Game");

            var manifestGame = games.Single(game => game.Name == "Manifest Game");
            Assert.Equal(Path.Combine(manifestFolder, "launcher.exe"), manifestGame.ExecutablePath);
        }

        [Fact]
        public async Task Build_WithMissingRoot_ScanReturnsEmpty()
        {
            string missing = Path.Combine(_root, "does-not-exist");

            using ServiceProvider provider = CompositionRoot.Build(missing);
            IGameCatalog catalog = provider.GetRequiredService<IGameCatalog>();

            var games = await catalog.GetGamesAsync();

            Assert.Empty(games);
        }
    }
}
