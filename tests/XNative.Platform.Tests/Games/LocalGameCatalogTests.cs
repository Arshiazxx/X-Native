using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using XNative.Core.Abstractions;
using XNative.Core.Games;
using XNative.Platform.Games;
using Xunit;

namespace XNative.Platform.Tests.Games
{
    public sealed class LocalGameCatalogTests
    {
        private readonly Mock<IFileSystemService> _mockFileSystem;
        private readonly LocalGameCatalogOptions _options;
        private readonly LocalGameCatalog _catalog;
        private readonly string _testRoot;

        public LocalGameCatalogTests()
        {
            _mockFileSystem = new Mock<IFileSystemService>();
            _options = new LocalGameCatalogOptions
            {
                RootPath = @"C:\TestGames",
                ExecutablePatterns = new[] { "*.exe" },
                MaxDepth = 3
            };
            _testRoot = _options.RootPath;
            _catalog = new LocalGameCatalog(Options.Create(_options), _mockFileSystem.Object);
        }

        [Fact]
        public async Task GetGamesAsync_ReturnsEmpty_WhenRootDoesNotExist()
        {
            _mockFileSystem.Setup(fs => fs.DirectoryExists(_testRoot)).Returns(false);

            var games = await _catalog.GetGamesAsync();

            Assert.Empty(games);
        }

        [Fact]
        public async Task GetGamesAsync_DetectsExeFiles()
        {
            var exePath = Path.Combine(_testRoot, "Game1", "game1.exe");
            var dirPath = Path.GetDirectoryName(exePath)!;

            _mockFileSystem.Setup(fs => fs.DirectoryExists(_testRoot)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetDirectories(_testRoot)).Returns(new[] { dirPath });
            _mockFileSystem.Setup(fs => fs.GetDirectories(dirPath)).Returns(Array.Empty<string>());
            _mockFileSystem.Setup(fs => fs.GetFiles(dirPath, "*.exe")).Returns(new[] { exePath });
            _mockFileSystem.Setup(fs => fs.FileExists(exePath)).Returns(true);

            var games = await _catalog.GetGamesAsync();

            Assert.Single(games);
            var game = games.First();
            Assert.Equal("game1", game.Name);
            Assert.Equal(exePath, game.ExecutablePath);
            Assert.Equal(dirPath, game.WorkingDirectory);
            Assert.Equal(ExecutableType.WindowsExecutable, game.ExecutableType);
        }

        [Fact]
        public async Task GetGamesAsync_DetectsGamesFromManifest()
        {
            var manifestPath = Path.Combine(_testRoot, "Game2", "game.json");
            var exePath = Path.Combine(_testRoot, "Game2", "launcher.exe");
            var dirPath = Path.GetDirectoryName(manifestPath)!;

            var manifestJson = """{"name": "Manifest Game", "executable": "launcher.exe"}""";

            _mockFileSystem.Setup(fs => fs.DirectoryExists(_testRoot)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetDirectories(_testRoot)).Returns(new[] { dirPath });
            _mockFileSystem.Setup(fs => fs.GetDirectories(dirPath)).Returns(Array.Empty<string>());
            _mockFileSystem.Setup(fs => fs.FileExists(manifestPath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(manifestPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(manifestJson);
            _mockFileSystem.Setup(fs => fs.FileExists(exePath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetFiles(dirPath, "*.exe")).Returns(Array.Empty<string>());

            var games = await _catalog.GetGamesAsync();

            Assert.Single(games);
            var game = games.First();
            Assert.Equal("Manifest Game", game.Name);
            Assert.Equal(exePath, game.ExecutablePath);
            Assert.Equal(dirPath, game.WorkingDirectory);
        }

        [Fact]
        public async Task GetGamesAsync_ManifestTakesPrecedenceOverExe()
        {
            var manifestPath = Path.Combine(_testRoot, "Game3", "game.json");
            var exePath = Path.Combine(_testRoot, "Game3", "game3.exe");
            var dirPath = Path.GetDirectoryName(manifestPath)!;

            var manifestJson = """{"name": "Manifest Game", "executable": "launcher.exe"}""";

            _mockFileSystem.Setup(fs => fs.DirectoryExists(_testRoot)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetDirectories(_testRoot)).Returns(new[] { dirPath });
            _mockFileSystem.Setup(fs => fs.GetDirectories(dirPath)).Returns(Array.Empty<string>());
            _mockFileSystem.Setup(fs => fs.FileExists(manifestPath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(manifestPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(manifestJson);
            _mockFileSystem.Setup(fs => fs.FileExists(exePath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.FileExists(Path.Combine(dirPath, "launcher.exe"))).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetFiles(dirPath, "*.exe")).Returns(new[] { exePath });

            var games = await _catalog.GetGamesAsync();

            Assert.Single(games);
            Assert.Equal("Manifest Game", games.First().Name);
        }

        [Fact]
        public async Task FindByIdAsync_ReturnsGame_WhenExists()
        {
            var exePath = Path.Combine(_testRoot, "Game1", "game1.exe");
            var dirPath = Path.GetDirectoryName(exePath)!;

            _mockFileSystem.Setup(fs => fs.DirectoryExists(_testRoot)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetDirectories(_testRoot)).Returns(new[] { dirPath });
            _mockFileSystem.Setup(fs => fs.GetDirectories(dirPath)).Returns(Array.Empty<string>());
            _mockFileSystem.Setup(fs => fs.GetFiles(dirPath, "*.exe")).Returns(new[] { exePath });
            _mockFileSystem.Setup(fs => fs.FileExists(exePath)).Returns(true);

            var games = await _catalog.GetGamesAsync();

            var found = await _catalog.FindByIdAsync(games.First().Id);

            Assert.NotNull(found);
            Assert.Equal(exePath, found.ExecutablePath);
        }

        [Fact]
        public async Task FindByIdAsync_ReturnsNull_WhenNotExists()
        {
            _mockFileSystem.Setup(fs => fs.DirectoryExists(_testRoot)).Returns(false);

            var found = await _catalog.FindByIdAsync("nonexistent");

            Assert.Null(found);
        }

        [Fact]
        public async Task FindByExecutablePathAsync_ReturnsGame_WhenExists()
        {
            var exePath = Path.Combine(_testRoot, "Game1", "game1.exe");
            var dirPath = Path.GetDirectoryName(exePath)!;

            _mockFileSystem.Setup(fs => fs.DirectoryExists(_testRoot)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetDirectories(_testRoot)).Returns(new[] { dirPath });
            _mockFileSystem.Setup(fs => fs.GetDirectories(dirPath)).Returns(Array.Empty<string>());
            _mockFileSystem.Setup(fs => fs.GetFiles(dirPath, "*.exe")).Returns(new[] { exePath });
            _mockFileSystem.Setup(fs => fs.FileExists(exePath)).Returns(true);

            await _catalog.GetGamesAsync();

            var found = await _catalog.FindByExecutablePathAsync(exePath);

            Assert.NotNull(found);
            Assert.Equal(exePath, found.ExecutablePath);
        }

        [Fact]
        public async Task RefreshAsync_UpdatesCache()
        {
            var exePath = Path.Combine(_testRoot, "Game1", "game1.exe");
            var dirPath = Path.GetDirectoryName(exePath)!;

            _mockFileSystem.Setup(fs => fs.DirectoryExists(_testRoot)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetDirectories(_testRoot)).Returns(new[] { dirPath });
            _mockFileSystem.Setup(fs => fs.GetDirectories(dirPath)).Returns(Array.Empty<string>());
            _mockFileSystem.Setup(fs => fs.GetFiles(dirPath, "*.exe")).Returns(new[] { exePath });
            _mockFileSystem.Setup(fs => fs.FileExists(exePath)).Returns(true);

            await _catalog.RefreshAsync();
            var firstScan = await _catalog.GetGamesAsync();
            Assert.Single(firstScan);

            // Add another game
            var exePath2 = Path.Combine(_testRoot, "Game2", "game2.exe");
            var dirPath2 = Path.GetDirectoryName(exePath2)!;

            _mockFileSystem.Setup(fs => fs.GetDirectories(_testRoot)).Returns(new[] { dirPath, dirPath2 });
            _mockFileSystem.Setup(fs => fs.GetFiles(dirPath2, "*.exe")).Returns(new[] { exePath2 });
            _mockFileSystem.Setup(fs => fs.FileExists(exePath2)).Returns(true);

            await _catalog.RefreshAsync();
            var secondScan = await _catalog.GetGamesAsync();
            Assert.Equal(2, secondScan.Count);
        }

        [Fact]
        public async Task GetGamesAsync_IdIsStable_AcrossCatalogInstances()
        {
            var exePath = Path.Combine(_testRoot, "Game1", "game1.exe");
            var dirPath = Path.GetDirectoryName(exePath)!;

            var secondMock = new Mock<IFileSystemService>();
            var secondCatalog = new LocalGameCatalog(
                Options.Create(new LocalGameCatalogOptions
                {
                    RootPath = _testRoot,
                    ExecutablePatterns = new[] { "*.exe" },
                    MaxDepth = 3
                }),
                secondMock.Object);

            foreach (var fs in new[] { _mockFileSystem, secondMock })
            {
                fs.Setup(f => f.DirectoryExists(_testRoot)).Returns(true);
                fs.Setup(f => f.GetDirectories(_testRoot)).Returns(new[] { dirPath });
                fs.Setup(f => f.GetDirectories(dirPath)).Returns(Array.Empty<string>());
                fs.Setup(f => f.GetFiles(_testRoot, "*.exe")).Returns(Array.Empty<string>());
                fs.Setup(f => f.GetFiles(dirPath, "*.exe")).Returns(new[] { exePath });
                fs.Setup(f => f.FileExists(exePath)).Returns(true);
            }

            var first = (await _catalog.GetGamesAsync()).Single();
            var second = (await secondCatalog.GetGamesAsync()).Single();

            Assert.Equal(first.Id, second.Id);

            // Expected vector: FNV-1a 64-bit over the UTF-8 bytes of the
            // normalized full path, computed independently here.
            var expectedHash = ComputeFnv1a64Hex(Path.GetFullPath(exePath));
            Assert.Equal($"game1-{expectedHash}", first.Id);

            var suffix = first.Id.Substring(first.Id.LastIndexOf('-') + 1);
            Assert.Equal(16, suffix.Length);
            Assert.All(suffix, c => Assert.True(Uri.IsHexDigit(c)));
        }

        [Fact]
        public async Task GetGamesAsync_RejectsManifest_WithExecutableOutsideDirectory()
        {
            var dirPath = Path.Combine(_testRoot, "GameEvil");
            var manifestPath = Path.Combine(dirPath, "game.json");
            var evilPath = Path.Combine(dirPath, "../evil.exe");
            var manifestJson = """{"name": "Evil Game", "executable": "../evil.exe"}""";

            _mockFileSystem.Setup(fs => fs.DirectoryExists(_testRoot)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetDirectories(_testRoot)).Returns(new[] { dirPath });
            _mockFileSystem.Setup(fs => fs.GetDirectories(dirPath)).Returns(Array.Empty<string>());
            _mockFileSystem.Setup(fs => fs.FileExists(manifestPath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(manifestPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(manifestJson);
            // The escaping executable exists; rejection must come from the guard.
            _mockFileSystem.Setup(fs => fs.FileExists(evilPath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetFiles(dirPath, "*.exe")).Returns(Array.Empty<string>());

            var games = await _catalog.GetGamesAsync();

            Assert.Empty(games);
        }

        [Fact]
        public async Task GetGamesAsync_RejectsManifest_WithWorkingDirectoryOutsideDirectory()
        {
            var dirPath = Path.Combine(_testRoot, "GameWd");
            var manifestPath = Path.Combine(dirPath, "game.json");
            var exePath = Path.Combine(dirPath, "game.exe");
            var manifestJson = """{"name": "WD Game", "executable": "game.exe", "workingDirectory": "../outside"}""";

            _mockFileSystem.Setup(fs => fs.DirectoryExists(_testRoot)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetDirectories(_testRoot)).Returns(new[] { dirPath });
            _mockFileSystem.Setup(fs => fs.GetDirectories(dirPath)).Returns(Array.Empty<string>());
            _mockFileSystem.Setup(fs => fs.FileExists(manifestPath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(manifestPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(manifestJson);
            _mockFileSystem.Setup(fs => fs.FileExists(exePath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetFiles(dirPath, "*.exe")).Returns(Array.Empty<string>());

            var games = await _catalog.GetGamesAsync();

            Assert.Empty(games);
        }

        [Fact]
        public async Task GetGamesAsync_ManifestV2_ArgumentsAndIconFlowThrough()
        {
            var dirPath = Path.Combine(_testRoot, "GameV2");
            var manifestPath = Path.Combine(dirPath, "game.json");
            var exePath = Path.Combine(dirPath, "game.exe");
            var iconPath = Path.Combine(dirPath, "assets/icon.png");
            var manifestJson = """{"name": "V2 Game", "executable": "game.exe", "arguments": "--windowed --level 3", "icon": "assets/icon.png"}""";

            _mockFileSystem.Setup(fs => fs.DirectoryExists(_testRoot)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetDirectories(_testRoot)).Returns(new[] { dirPath });
            _mockFileSystem.Setup(fs => fs.GetDirectories(dirPath)).Returns(Array.Empty<string>());
            _mockFileSystem.Setup(fs => fs.FileExists(manifestPath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(manifestPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(manifestJson);
            _mockFileSystem.Setup(fs => fs.FileExists(exePath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.FileExists(iconPath)).Returns(true);

            var games = await _catalog.GetGamesAsync();

            var game = Assert.Single(games);
            Assert.Equal("--windowed --level 3", game.LaunchArguments);
            Assert.NotNull(game.Metadata);
            Assert.Equal(iconPath, game.Metadata.IconPath);
        }

        [Fact]
        public async Task GetGamesAsync_ManifestV2_MissingIconIsNotRecorded()
        {
            var dirPath = Path.Combine(_testRoot, "GameV2MissingIcon");
            var manifestPath = Path.Combine(dirPath, "game.json");
            var exePath = Path.Combine(dirPath, "game.exe");
            var manifestJson = """{"name": "No Icon Game", "executable": "game.exe", "arguments": "--quiet", "icon": "missing.png"}""";

            _mockFileSystem.Setup(fs => fs.DirectoryExists(_testRoot)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetDirectories(_testRoot)).Returns(new[] { dirPath });
            _mockFileSystem.Setup(fs => fs.GetDirectories(dirPath)).Returns(Array.Empty<string>());
            _mockFileSystem.Setup(fs => fs.FileExists(manifestPath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(manifestPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(manifestJson);
            _mockFileSystem.Setup(fs => fs.FileExists(exePath)).Returns(true);
            // Icon file deliberately not set up: FileExists returns false.

            var games = await _catalog.GetGamesAsync();

            var game = Assert.Single(games);
            Assert.Equal("--quiet", game.LaunchArguments);
            Assert.NotNull(game.Metadata);
            Assert.Null(game.Metadata.IconPath);
        }

        [Fact]
        public async Task GetGamesAsync_ConcurrentRefreshAndReads_AreConsistent()
        {
            var exePath = Path.Combine(_testRoot, "Game1", "game1.exe");
            var dirPath = Path.GetDirectoryName(exePath)!;

            _mockFileSystem.Setup(fs => fs.DirectoryExists(_testRoot)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetDirectories(_testRoot)).Returns(new[] { dirPath });
            _mockFileSystem.Setup(fs => fs.GetDirectories(dirPath)).Returns(Array.Empty<string>());
            _mockFileSystem.Setup(fs => fs.GetFiles(_testRoot, "*.exe")).Returns(Array.Empty<string>());
            _mockFileSystem.Setup(fs => fs.GetFiles(dirPath, "*.exe")).Returns(new[] { exePath });
            _mockFileSystem.Setup(fs => fs.FileExists(exePath)).Returns(true);

            var snapshots = new ConcurrentBag<IReadOnlyCollection<GameInfo>>();
            var tasks = new List<Task>();
            for (var i = 0; i < 50; i++)
            {
                if (i % 2 == 0)
                {
                    tasks.Add(Task.Run(() => _catalog.RefreshAsync()));
                }
                else
                {
                    tasks.Add(Task.Run(async () => snapshots.Add(await _catalog.GetGamesAsync())));
                }
            }

            await Task.WhenAll(tasks);

            Assert.Equal(25, snapshots.Count);
            Assert.All(snapshots, snapshot =>
            {
                var game = Assert.Single(snapshot);
                Assert.Equal(exePath, game.ExecutablePath);
            });
            Assert.Single(snapshots.Select(s => s.Single().Id).Distinct());
        }

        [Fact]
        public async Task GetGamesAsync_WhitespaceRoot_ReturnsEmpty()
        {
            var fs = new Mock<IFileSystemService>();
            fs.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
            var catalog = new LocalGameCatalog(
                Options.Create(new LocalGameCatalogOptions { RootPath = "   " }),
                fs.Object);

            var games = await catalog.GetGamesAsync();

            Assert.Empty(games);
            fs.Verify(f => f.DirectoryExists(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetGamesAsync_DuplicateExecutableAcrossPatterns_AddedOnce()
        {
            var exePath = Path.Combine(_testRoot, "Game1", "game1.exe");
            var dirPath = Path.GetDirectoryName(exePath)!;

            var fs = new Mock<IFileSystemService>();
            var catalog = new LocalGameCatalog(
                Options.Create(new LocalGameCatalogOptions
                {
                    RootPath = _testRoot,
                    ExecutablePatterns = new[] { "*.exe", "game*.exe" },
                    MaxDepth = 3
                }),
                fs.Object);

            fs.Setup(f => f.DirectoryExists(_testRoot)).Returns(true);
            fs.Setup(f => f.GetDirectories(_testRoot)).Returns(new[] { dirPath });
            fs.Setup(f => f.GetDirectories(dirPath)).Returns(Array.Empty<string>());
            fs.Setup(f => f.GetFiles(dirPath, "*.exe")).Returns(new[] { exePath });
            fs.Setup(f => f.GetFiles(dirPath, "game*.exe")).Returns(new[] { exePath });
            fs.Setup(f => f.FileExists(exePath)).Returns(true);

            var games = await catalog.GetGamesAsync();

            Assert.Single(games);
        }

        [Fact]
        public async Task GetGamesAsync_MaxDepthBoundary()
        {
            // Tree: root (depth 0) -> Child (depth 1) -> GrandChild (depth 2).
            var childDir = Path.Combine(_testRoot, "Child");
            var grandDir = Path.Combine(childDir, "GrandChild");
            var childExe = Path.Combine(childDir, "child.exe");
            var grandExe = Path.Combine(grandDir, "grand.exe");

            void SetupTree(Mock<IFileSystemService> fs)
            {
                fs.Setup(f => f.DirectoryExists(_testRoot)).Returns(true);
                fs.Setup(f => f.GetDirectories(_testRoot)).Returns(new[] { childDir });
                fs.Setup(f => f.GetDirectories(childDir)).Returns(new[] { grandDir });
                fs.Setup(f => f.GetDirectories(grandDir)).Returns(Array.Empty<string>());
                fs.Setup(f => f.GetFiles(_testRoot, "*.exe")).Returns(Array.Empty<string>());
                fs.Setup(f => f.GetFiles(childDir, "*.exe")).Returns(new[] { childExe });
                fs.Setup(f => f.GetFiles(grandDir, "*.exe")).Returns(new[] { grandExe });
            }

            var depthOneMock = new Mock<IFileSystemService>();
            SetupTree(depthOneMock);
            var depthOneCatalog = new LocalGameCatalog(
                Options.Create(new LocalGameCatalogOptions
                {
                    RootPath = _testRoot,
                    ExecutablePatterns = new[] { "*.exe" },
                    MaxDepth = 1
                }),
                depthOneMock.Object);

            var depthOneGames = await depthOneCatalog.GetGamesAsync();
            var only = Assert.Single(depthOneGames);
            Assert.Equal(childExe, only.ExecutablePath);

            var depthTwoMock = new Mock<IFileSystemService>();
            SetupTree(depthTwoMock);
            var depthTwoCatalog = new LocalGameCatalog(
                Options.Create(new LocalGameCatalogOptions
                {
                    RootPath = _testRoot,
                    ExecutablePatterns = new[] { "*.exe" },
                    MaxDepth = 2
                }),
                depthTwoMock.Object);

            var depthTwoGames = await depthTwoCatalog.GetGamesAsync();
            Assert.Equal(2, depthTwoGames.Count);
        }

        [Fact]
        public async Task GetGamesAsync_SubdirectoryVanishingMidScan_IsSkipped()
        {
            var badDir = Path.Combine(_testRoot, "Bad");
            var goodDir = Path.Combine(_testRoot, "Good");
            var goodExe = Path.Combine(goodDir, "good.exe");

            _mockFileSystem.Setup(fs => fs.DirectoryExists(_testRoot)).Returns(true);
            _mockFileSystem.Setup(fs => fs.GetDirectories(_testRoot)).Returns(new[] { badDir, goodDir });
            _mockFileSystem.Setup(fs => fs.GetDirectories(badDir)).Returns(Array.Empty<string>());
            _mockFileSystem.Setup(fs => fs.GetDirectories(goodDir)).Returns(Array.Empty<string>());
            _mockFileSystem.Setup(fs => fs.GetFiles(badDir, "*.exe"))
                .Throws(new DirectoryNotFoundException("vanished"));
            _mockFileSystem.Setup(fs => fs.GetFiles(goodDir, "*.exe")).Returns(new[] { goodExe });
            _mockFileSystem.Setup(fs => fs.FileExists(goodExe)).Returns(true);

            var games = await _catalog.GetGamesAsync();

            var game = Assert.Single(games);
            Assert.Equal(goodExe, game.ExecutablePath);
        }

        private static string ComputeFnv1a64Hex(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var hash = 14695981039346656037UL;
            unchecked
            {
                foreach (var b in bytes)
                {
                    hash ^= b;
                    hash *= 1099511628211UL;
                }
            }

            return hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
        }

        [Fact]
        public void GameManifest_DeserializesFromJson()
        {
            var json = """{"name": "Test Game", "executable": "game.exe", "workingDirectory": ".", "description": "A test", "publisher": "TestPub", "version": "1.0"}""";
            var manifest = System.Text.Json.JsonSerializer.Deserialize<GameManifest>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(manifest);
            Assert.Equal("Test Game", manifest.Name);
            Assert.Equal("game.exe", manifest.Executable);
            Assert.Equal(".", manifest.WorkingDirectory);
            Assert.Equal("A test", manifest.Description);
            Assert.Equal("TestPub", manifest.Publisher);
            Assert.Equal("1.0", manifest.Version);
        }
    }
}