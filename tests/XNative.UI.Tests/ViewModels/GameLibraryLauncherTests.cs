using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Abstractions;
using XNative.Core.Games;
using XNative.UI.ViewModels;
using Xunit;

namespace XNative.UI.Tests.ViewModels
{
    public sealed class GameLibraryLauncherTests
    {
        private sealed class FakeGameCatalog : IGameCatalog
        {
            public IReadOnlyCollection<GameInfo> Games { get; set; } = new List<GameInfo>();

            public Task<IReadOnlyCollection<GameInfo>> GetGamesAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Games);
            }

            public Task<GameInfo?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<GameInfo?>(Games.FirstOrDefault(game => game.Id == id));
            }

            public Task<GameInfo?> FindByExecutablePathAsync(string executablePath, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<GameInfo?>(Games.FirstOrDefault(game => game.ExecutablePath == executablePath));
            }

            public Task RefreshAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeGameRunner : IGameRunner
        {
            public LaunchResult NextResult { get; set; } = LaunchResult.Ok("Launched");

            public int LaunchCallCount { get; private set; }

            public GameInfo? LastGame { get; private set; }

            public LaunchOptions? LastOptions { get; private set; }

            public bool LastCallHadOptions { get; private set; }

            public Task<LaunchResult> LaunchAsync(GameInfo game, CancellationToken cancellationToken = default)
            {
                LaunchCallCount++;
                LastGame = game;
                LastOptions = null;
                LastCallHadOptions = false;
                return Task.FromResult(NextResult);
            }

            public Task<LaunchResult> LaunchAsync(GameInfo game, LaunchOptions options, CancellationToken cancellationToken = default)
            {
                LaunchCallCount++;
                LastGame = game;
                LastOptions = options;
                LastCallHadOptions = true;
                return Task.FromResult(NextResult);
            }
        }

        private static GameInfo MakeGame(string id, string name, string? launchArguments = null)
        {
            return new GameInfo(
                id,
                name,
                $@"C:\Games\{id}\game.exe",
                $@"C:\Games\{id}",
                ExecutableType.WindowsExecutable,
                metadata: null,
                architecture: default,
                launchArguments: launchArguments);
        }

        private static FakeGameCatalog CatalogWith(params GameInfo[] games)
        {
            return new FakeGameCatalog { Games = new List<GameInfo>(games) };
        }

        private static async Task<GameLibraryViewModel> ScannedViewModel(
            FakeGameCatalog catalog, FakeGameRunner? runner = null)
        {
            GameLibraryViewModel viewModel = runner is null
                ? new GameLibraryViewModel(catalog)
                : new GameLibraryViewModel(catalog, runner);
            await viewModel.ScanGamesAsync();
            return viewModel;
        }

        [Fact]
        public void InitialState_HasEmptySearchAscendingSortAndNoSelection()
        {
            var viewModel = new GameLibraryViewModel(CatalogWith());

            Assert.Equal(string.Empty, viewModel.SearchText);
            Assert.Equal(SortMode.NameAscending, viewModel.SortMode);
            Assert.Empty(viewModel.VisibleGames);
            Assert.Null(viewModel.SelectedGame);
            Assert.Equal(string.Empty, viewModel.LaunchStatus);
            Assert.False(viewModel.IsLaunching);
            Assert.False(viewModel.CanLaunch);
        }

        [Fact]
        public async Task SearchText_FiltersByNameCaseInsensitiveSubstring()
        {
            FakeGameCatalog catalog = CatalogWith(
                MakeGame("id1", "Alpha Quest"),
                MakeGame("id2", "beta quest"),
                MakeGame("id3", "Gamma"));
            GameLibraryViewModel viewModel = await ScannedViewModel(catalog);

            viewModel.SearchText = "QUEST";

            Assert.Equal(
                new[] { "Alpha Quest", "beta quest" },
                viewModel.VisibleGames.Select(game => game.Name));
            Assert.Equal(3, viewModel.Games.Count);
        }

        [Fact]
        public async Task SearchText_ClearingRestoresAllGames()
        {
            FakeGameCatalog catalog = CatalogWith(
                MakeGame("id1", "Alpha Quest"),
                MakeGame("id2", "Gamma"));
            GameLibraryViewModel viewModel = await ScannedViewModel(catalog);

            viewModel.SearchText = "quest";
            Assert.Single(viewModel.VisibleGames);

            viewModel.SearchText = string.Empty;

            Assert.Equal(2, viewModel.VisibleGames.Count);
        }

        [Fact]
        public async Task SearchText_WithoutMatch_ShowsNoGames()
        {
            FakeGameCatalog catalog = CatalogWith(MakeGame("id1", "Alpha Quest"));
            GameLibraryViewModel viewModel = await ScannedViewModel(catalog);

            viewModel.SearchText = "zelda";

            Assert.Empty(viewModel.VisibleGames);
            Assert.Single(viewModel.Games);
        }

        [Fact]
        public async Task SortMode_NameDescending_ReordersVisibleGamesOnly()
        {
            FakeGameCatalog catalog = CatalogWith(
                MakeGame("id1", "Alpha Quest"),
                MakeGame("id2", "beta quest"),
                MakeGame("id3", "Gamma"));
            GameLibraryViewModel viewModel = await ScannedViewModel(catalog);

            viewModel.SortMode = SortMode.NameDescending;

            Assert.Equal(
                new[] { "Gamma", "beta quest", "Alpha Quest" },
                viewModel.VisibleGames.Select(game => game.Name));
            Assert.Equal(
                new[] { "Alpha Quest", "beta quest", "Gamma" },
                viewModel.Games.Select(game => game.Name));
        }

        [Fact]
        public async Task ScanGamesAsync_PreservesSelection_WhenGameIdSurvivesRescan()
        {
            FakeGameCatalog catalog = CatalogWith(
                MakeGame("id1", "Alpha Quest"),
                MakeGame("id2", "Gamma"));
            GameLibraryViewModel viewModel = await ScannedViewModel(catalog);
            viewModel.SelectedGame = viewModel.Games.Single(game => game.Id == "id2");

            // The rescan returns fresh instances with the same ids.
            catalog.Games = new List<GameInfo>
            {
                MakeGame("id1", "Alpha Quest"),
                MakeGame("id2", "Gamma"),
            };
            await viewModel.ScanGamesAsync();

            Assert.NotNull(viewModel.SelectedGame);
            Assert.Equal("id2", viewModel.SelectedGame!.Id);
            Assert.Same(
                viewModel.Games.Single(game => game.Id == "id2"),
                viewModel.SelectedGame);
        }

        [Fact]
        public async Task ScanGamesAsync_ClearsSelection_WhenGameIdIsGone()
        {
            FakeGameCatalog catalog = CatalogWith(
                MakeGame("id1", "Alpha Quest"),
                MakeGame("id2", "Gamma"));
            GameLibraryViewModel viewModel = await ScannedViewModel(catalog);
            viewModel.SelectedGame = viewModel.Games.Single(game => game.Id == "id2");

            catalog.Games = new List<GameInfo> { MakeGame("id1", "Alpha Quest") };
            await viewModel.ScanGamesAsync();

            Assert.Null(viewModel.SelectedGame);
        }

        [Fact]
        public async Task LaunchSelectedAsync_Success_SetsLaunchStatusFromResult()
        {
            var runner = new FakeGameRunner { NextResult = LaunchResult.Ok("Launched Alpha Quest", 4242) };
            FakeGameCatalog catalog = CatalogWith(MakeGame("id1", "Alpha Quest"));
            GameLibraryViewModel viewModel = await ScannedViewModel(catalog, runner);
            viewModel.SelectedGame = viewModel.Games[0];

            await viewModel.LaunchSelectedAsync();

            Assert.Equal("Launched Alpha Quest", viewModel.LaunchStatus);
            Assert.False(viewModel.IsLaunching);
            Assert.Equal(1, runner.LaunchCallCount);
            Assert.Equal("id1", runner.LastGame!.Id);
        }

        [Fact]
        public async Task LaunchSelectedAsync_Failure_SetsLaunchStatusFromResult()
        {
            var runner = new FakeGameRunner
            {
                NextResult = LaunchResult.Fail("Backend exploded", LaunchFailureReason.BackendNotImplemented),
            };
            FakeGameCatalog catalog = CatalogWith(MakeGame("id1", "Alpha Quest"));
            GameLibraryViewModel viewModel = await ScannedViewModel(catalog, runner);
            viewModel.SelectedGame = viewModel.Games[0];

            await viewModel.LaunchSelectedAsync();

            Assert.Equal("Backend exploded", viewModel.LaunchStatus);
            Assert.False(viewModel.IsLaunching);
            Assert.Equal(1, runner.LaunchCallCount);
        }

        [Fact]
        public async Task LaunchSelectedAsync_PassesLaunchArgumentsThroughLaunchOptions()
        {
            var runner = new FakeGameRunner();
            FakeGameCatalog catalog = CatalogWith(MakeGame("id1", "Alpha Quest", "-dx11 -windowed"));
            GameLibraryViewModel viewModel = await ScannedViewModel(catalog, runner);
            viewModel.SelectedGame = viewModel.Games[0];

            await viewModel.LaunchSelectedAsync();

            Assert.True(runner.LastCallHadOptions);
            Assert.NotNull(runner.LastOptions);
            Assert.Equal("-dx11 -windowed", runner.LastOptions!.Arguments);
        }

        [Fact]
        public async Task LaunchSelectedAsync_WithoutLaunchArguments_UsesDefaultOverload()
        {
            var runner = new FakeGameRunner();
            FakeGameCatalog catalog = CatalogWith(MakeGame("id1", "Alpha Quest"));
            GameLibraryViewModel viewModel = await ScannedViewModel(catalog, runner);
            viewModel.SelectedGame = viewModel.Games[0];

            await viewModel.LaunchSelectedAsync();

            Assert.Equal(1, runner.LaunchCallCount);
            Assert.False(runner.LastCallHadOptions);
        }

        [Fact]
        public async Task LaunchSelectedAsync_WithoutSelection_IsSafeNoOp()
        {
            var runner = new FakeGameRunner();
            FakeGameCatalog catalog = CatalogWith(MakeGame("id1", "Alpha Quest"));
            GameLibraryViewModel viewModel = await ScannedViewModel(catalog, runner);

            await viewModel.LaunchSelectedAsync();

            Assert.Equal(0, runner.LaunchCallCount);
            Assert.Equal(string.Empty, viewModel.LaunchStatus);
            Assert.False(viewModel.LaunchSelectedCommand.CanExecute(null));
        }

        [Fact]
        public async Task LaunchSelectedAsync_WithoutRunner_IsSafeNoOpAndCommandDisabled()
        {
            FakeGameCatalog catalog = CatalogWith(MakeGame("id1", "Alpha Quest"));
            GameLibraryViewModel viewModel = await ScannedViewModel(catalog);
            viewModel.SelectedGame = viewModel.Games[0];

            Assert.False(viewModel.CanLaunch);
            Assert.False(viewModel.LaunchSelectedCommand.CanExecute(null));

            await viewModel.LaunchSelectedAsync();

            Assert.Equal(string.Empty, viewModel.LaunchStatus);
            Assert.False(viewModel.IsLaunching);
        }

        [Fact]
        public async Task LaunchSelectedCommand_CanExecute_ReflectsSelection()
        {
            var runner = new FakeGameRunner();
            FakeGameCatalog catalog = CatalogWith(MakeGame("id1", "Alpha Quest"));
            GameLibraryViewModel viewModel = await ScannedViewModel(catalog, runner);

            Assert.True(viewModel.CanLaunch);
            Assert.False(viewModel.LaunchSelectedCommand.CanExecute(null));

            viewModel.SelectedGame = viewModel.Games[0];

            Assert.True(viewModel.LaunchSelectedCommand.CanExecute(null));
        }

        [Fact]
        public async Task LaunchSelectedCommand_ExecuteAsync_LaunchesSelectedGame()
        {
            var runner = new FakeGameRunner { NextResult = LaunchResult.Ok("Started") };
            FakeGameCatalog catalog = CatalogWith(MakeGame("id1", "Alpha Quest"));
            GameLibraryViewModel viewModel = await ScannedViewModel(catalog, runner);
            viewModel.SelectedGame = viewModel.Games[0];

            await viewModel.LaunchSelectedCommand.ExecuteAsync();

            Assert.Equal(1, runner.LaunchCallCount);
            Assert.Equal("Started", viewModel.LaunchStatus);
        }
    }
}
