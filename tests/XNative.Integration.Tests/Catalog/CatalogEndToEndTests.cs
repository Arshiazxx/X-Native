using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using XNative.Core.Games;
using XNative.Integration.Tests.Doubles;
using XNative.Platform.Games;
using Xunit;

namespace XNative.Integration.Tests.Catalog
{
    /// <summary>
    /// End-to-end tests of <see cref="LocalGameCatalog"/> over a hand-rolled
    /// in-memory file system: nested folders, a game.json manifest game,
    /// bare .exe games, manifest precedence, FindById round-trips, and the
    /// adversarial cases (path traversal, malformed manifests, unstable ids)
    /// asserted as the CORRECT end state.
    /// </summary>
    public sealed class CatalogEndToEndTests
    {
        private static string Root => Path.Combine("xnative-it", "games");

        private static LocalGameCatalog CreateCatalog(InMemoryFileSystemService fileSystem, string root)
        {
            var options = new LocalGameCatalogOptions
            {
                RootPath = root,
                ExecutablePatterns = new[] { "*.exe" },
                MaxDepth = 3,
            };
            return new LocalGameCatalog(Options.Create(options), fileSystem);
        }

        /// <summary>
        /// Builds the standard tree:
        ///   games/alpha/alpha.exe                      (bare exe)
        ///   games/beta/game.json + beta/beta.exe       (manifest game)
        ///   games/beta/deep/deep.exe                   (nested bare exe)
        ///   games/gamma/game.json + gamma.exe + helper.exe (manifest wins)
        /// </summary>
        private static InMemoryFileSystemService BuildStandardTree()
        {
            string root = Root;
            var fs = new InMemoryFileSystemService();

            fs.AddFile(Path.Combine(root, "alpha", "alpha.exe"));

            fs.AddFile(
                Path.Combine(root, "beta", "game.json"),
                "{\"name\": \"Beta Game\", \"executable\": \"beta.exe\", \"description\": \"A beta\", \"publisher\": \"Beta Pub\", \"version\": \"2.0\"}");
            fs.AddFile(Path.Combine(root, "beta", "beta.exe"));
            fs.AddFile(Path.Combine(root, "beta", "deep", "deep.exe"));

            fs.AddFile(
                Path.Combine(root, "gamma", "game.json"),
                "{\"name\": \"Gamma Game\", \"executable\": \"gamma.exe\"}");
            fs.AddFile(Path.Combine(root, "gamma", "gamma.exe"));
            fs.AddFile(Path.Combine(root, "gamma", "helper.exe"));

            return fs;
        }

        [Fact]
        public async Task Scan_FindsManifestAndBareGamesAcrossNestedFolders()
        {
            LocalGameCatalog catalog = CreateCatalog(BuildStandardTree(), Root);

            var games = await catalog.GetGamesAsync();

            Assert.Equal(4, games.Count);
            Assert.Contains(games, game => game.Name == "alpha");
            Assert.Contains(games, game => game.Name == "Beta Game");
            Assert.Contains(games, game => game.Name == "deep");
            Assert.Contains(games, game => game.Name == "Gamma Game");

            GameInfo beta = games.Single(game => game.Name == "Beta Game");
            Assert.Equal(Path.GetFullPath(Path.Combine(Root, "beta", "beta.exe")), beta.ExecutablePath);
            Assert.Equal(Path.GetFullPath(Path.Combine(Root, "beta")), beta.WorkingDirectory);
            Assert.Equal(ExecutableType.WindowsExecutable, beta.ExecutableType);

            // The gamma folder's bare helper.exe must not appear: a
            // successfully processed manifest takes precedence in its folder.
            Assert.DoesNotContain(games, game => game.Name == "helper");
        }

        [Fact]
        public async Task Scan_ManifestGameCarriesMetadata()
        {
            LocalGameCatalog catalog = CreateCatalog(BuildStandardTree(), Root);

            var games = await catalog.GetGamesAsync();
            GameInfo beta = games.Single(game => game.Name == "Beta Game");

            Assert.NotNull(beta.Metadata);
            Assert.Equal("A beta", beta.Metadata!.Description);
            Assert.Equal("Beta Pub", beta.Metadata.Publisher);
            Assert.Equal("2.0", beta.Metadata.Version);
        }

        [Fact]
        public async Task FindById_RoundTripsEveryScannedGame()
        {
            LocalGameCatalog catalog = CreateCatalog(BuildStandardTree(), Root);

            var games = await catalog.GetGamesAsync();
            Assert.NotEmpty(games);

            foreach (GameInfo game in games)
            {
                GameInfo? found = await catalog.FindByIdAsync(game.Id);
                Assert.NotNull(found);
                Assert.Equal(game.ExecutablePath, found!.ExecutablePath);
                Assert.Equal(game.Name, found.Name);
            }
        }

        [Fact]
        public async Task FindByExecutablePath_RoundTripsScannedGame()
        {
            LocalGameCatalog catalog = CreateCatalog(BuildStandardTree(), Root);

            var games = await catalog.GetGamesAsync();
            GameInfo alpha = games.Single(game => game.Name == "alpha");

            GameInfo? found = await catalog.FindByExecutablePathAsync(alpha.ExecutablePath);

            Assert.NotNull(found);
            Assert.Equal(alpha.Id, found!.Id);
        }

        [Fact]
        public async Task Refresh_PicksUpGamesAddedAfterFirstScan()
        {
            InMemoryFileSystemService fs = BuildStandardTree();
            LocalGameCatalog catalog = CreateCatalog(fs, Root);

            Assert.Equal(4, (await catalog.GetGamesAsync()).Count);

            fs.AddFile(Path.Combine(Root, "delta", "delta.exe"));
            await catalog.RefreshAsync();

            var games = await catalog.GetGamesAsync();
            Assert.Equal(5, games.Count);
            Assert.Contains(games, game => game.Name == "delta");
        }

        [Fact]
        public async Task TwoCatalogInstancesOverSameTree_ProduceIdenticalIds()
        {
            // In-process stability probe. Both instances live in the same
            // process, so this test alone could not catch an id scheme
            // derived from string.GetHashCode: the randomized per-process
            // seed is shared here, so such ids would agree in-process yet
            // change between processes. That cross-process case is what
            // GameId_IsNotDerivedFromRandomizedStringGetHashCode pins.
            LocalGameCatalog first = CreateCatalog(BuildStandardTree(), Root);
            LocalGameCatalog second = CreateCatalog(BuildStandardTree(), Root);

            var firstIds = (await first.GetGamesAsync())
                .ToDictionary(game => game.ExecutablePath, game => game.Id);
            var secondIds = (await second.GetGamesAsync())
                .ToDictionary(game => game.ExecutablePath, game => game.Id);

            Assert.Equal(firstIds.Count, secondIds.Count);
            foreach (var pair in firstIds)
            {
                Assert.Equal(pair.Value, secondIds[pair.Key]);
            }
        }

        [Fact]
        public async Task GameId_IsNotDerivedFromRandomizedStringGetHashCode()
        {
            // Gate for the id-stability defect: if LocalGameCatalog.GenerateId
            // built the id from exePath.GetHashCode(), the id would be
            // unstable — in .NET, string.GetHashCode() is randomized per
            // process — so an id that matches that formula in this process
            // is proof the unstable algorithm is in use: saved ids, profiles
            // and shortcuts keyed by game id would break on every restart.
            // A stable algorithm (e.g. a content hash of the path) will not
            // match this formula.
            LocalGameCatalog catalog = CreateCatalog(BuildStandardTree(), Root);

            var games = await catalog.GetGamesAsync();
            GameInfo alpha = games.Single(game => game.Name == "alpha");

            string legacyId = "alpha-" + alpha.ExecutablePath.GetHashCode().ToString("x8");

            Assert.NotEqual(legacyId, alpha.Id);
        }

        [Fact]
        public async Task ManifestExecutableEscapingRootWithDotDot_IsNotCatalogued()
        {
            // Path traversal gate: a game.json dropped anywhere in the tree
            // must not be able to surface an executable from outside the
            // catalog root. Without the catalog's traversal guard,
            // Path.Combine(directory, executable) would happily resolve
            // "../.." and the outside exe would be catalogued under the
            // manifest's (attacker-chosen) display name.
            string root = Root;
            string outsideExe = Path.GetFullPath(Path.Combine("xnative-it", "outside.exe"));

            var fs = new InMemoryFileSystemService();
            fs.AddFile(Path.Combine(root, "alpha", "alpha.exe"));
            fs.AddFile(outsideExe);
            fs.AddFile(
                Path.Combine(root, "evil", "game.json"),
                "{\"name\": \"Evil Game\", \"executable\": \"../../outside.exe\"}");
            fs.AddDirectory(Path.Combine(root, "evil"));

            LocalGameCatalog catalog = CreateCatalog(fs, root);
            var games = await catalog.GetGamesAsync();

            Assert.DoesNotContain(games, game => game.Name == "Evil Game");
            Assert.DoesNotContain(games, game => string.Equals(game.ExecutablePath, outsideExe, StringComparison.Ordinal));
        }

        [Fact]
        public async Task ManifestExecutableWithAbsolutePath_IsNotCatalogued()
        {
            // Same traversal, second form: Path.Combine discards the manifest
            // directory entirely when the executable value is rooted, so an
            // absolute path in game.json points anywhere on the device.
            string root = Root;
            string outsideExe = Path.GetFullPath(Path.Combine("xnative-it", "outside.exe"));

            var fs = new InMemoryFileSystemService();
            fs.AddFile(Path.Combine(root, "alpha", "alpha.exe"));
            fs.AddFile(outsideExe);
            fs.AddFile(
                Path.Combine(root, "evil", "game.json"),
                "{\"name\": \"Abs Game\", \"executable\": \"" + outsideExe.Replace("\\", "\\\\") + "\"}");
            fs.AddDirectory(Path.Combine(root, "evil"));

            LocalGameCatalog catalog = CreateCatalog(fs, root);
            var games = await catalog.GetGamesAsync();

            Assert.DoesNotContain(games, game => game.Name == "Abs Game");
            Assert.DoesNotContain(games, game => string.Equals(game.ExecutablePath, outsideExe, StringComparison.Ordinal));
        }

        [Fact]
        public async Task SingleMalformedManifest_DoesNotAbortWholeScan()
        {
            // A manifest whose name is whitespace makes GameInfo's
            // constructor throw ArgumentException. LocalGameCatalog must
            // absorb that per-manifest failure — if it did not, one bad
            // game.json would abort the entire library scan. The gate: the
            // scan completes and the healthy games are still listed.
            string root = Root;
            var fs = new InMemoryFileSystemService();
            fs.AddFile(Path.Combine(root, "alpha", "alpha.exe"));
            fs.AddFile(
                Path.Combine(root, "bad", "game.json"),
                "{\"name\": \"   \", \"executable\": \"bad.exe\"}");
            fs.AddFile(Path.Combine(root, "bad", "bad.exe"));

            LocalGameCatalog catalog = CreateCatalog(fs, root);
            var games = await catalog.GetGamesAsync();

            Assert.Contains(games, game => game.Name == "alpha");
        }

        [Fact]
        public async Task MissingRoot_ReturnsEmptyCatalog()
        {
            var fs = new InMemoryFileSystemService();
            LocalGameCatalog catalog = CreateCatalog(fs, Root);

            var games = await catalog.GetGamesAsync();

            Assert.Empty(games);
            Assert.Null(await catalog.FindByIdAsync("anything"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Scan_BlankPatternInPatternSet_ContributesNoGames(string blankPattern)
        {
            // The real SystemFileSystemService fails closed on a blank
            // pattern — it matches nothing and is never widened to
            // "*.*" (see IFileSystemService.GetFiles) — and the
            // in-memory double must behave identically, or the gate
            // tests above could pass against behaviour the real
            // service does not have. The folder holds a .txt file that
            // a match-all blank pattern WOULD surface as a game, so
            // this test fails if the widening ever returns.
            string root = Root;
            var fs = new InMemoryFileSystemService();
            string gameDir = Path.Combine(root, "blanky");
            fs.AddFile(Path.Combine(gameDir, "blanky.exe"));
            fs.AddFile(Path.Combine(gameDir, "notes.txt"));

            // Direct pin on the double: the blank pattern alone finds
            // nothing, while the real pattern still finds the .exe.
            Assert.Empty(fs.GetFiles(gameDir, blankPattern));
            Assert.Single(fs.GetFiles(gameDir, "*.exe"));

            var options = new LocalGameCatalogOptions
            {
                RootPath = root,
                ExecutablePatterns = new[] { blankPattern, "*.exe" },
                MaxDepth = 3,
            };
            var catalog = new LocalGameCatalog(Options.Create(options), fs);

            var games = await catalog.GetGamesAsync();

            GameInfo only = Assert.Single(games);
            Assert.Equal("blanky", only.Name);
        }
    }
}
