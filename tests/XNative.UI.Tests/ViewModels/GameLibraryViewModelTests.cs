using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using XNative.Core.Abstractions;
using XNative.Core.Games;
using XNative.UI.ViewModels;
using Xunit;

namespace XNative.UI.Tests.ViewModels
{
    public sealed class GameViewModelTests
    {
        [Fact]
        public void GameViewModel_CreatesSuccessfully()
        {
            var gameInfo = new GameInfo(
                "test-id",
                "Test Game",
                @"C:\Games\Test\game.exe",
                @"C:\Games\Test",
                ExecutableType.WindowsExecutable);

            var viewModel = new GameViewModel(gameInfo);

            Assert.Equal("Test Game", viewModel.Name);
            Assert.Equal(@"C:\Games\Test\game.exe", viewModel.ExecutablePath);
            Assert.Equal("wine (research)", viewModel.Runner);
            Assert.Equal(ExecutableType.WindowsExecutable, viewModel.ExecutableType);
            Assert.Equal("Test Game (wine (research))", viewModel.DisplayText);
            Assert.Contains("Path: C:\\Games\\Test\\game.exe", viewModel.DetailText);
            Assert.Contains("Runner: wine (research)", viewModel.DetailText);
        }

        [Fact]
        public void GameViewModel_UwpApplication_ReturnsUwpRunner()
        {
            var gameInfo = new GameInfo(
                "uwp-id",
                "UWP Game",
                @"C:\Games\UWP\app.exe",
                @"C:\Games\UWP",
                ExecutableType.UwpApplication);

            var viewModel = new GameViewModel(gameInfo);

            Assert.Equal("uwp", viewModel.Runner);
        }

        [Fact]
        public void GameViewModel_UnknownType_ReturnsUnknownRunner()
        {
            var gameInfo = new GameInfo(
                "unknown-id",
                "Unknown Game",
                @"C:\Games\Unknown\app.exe",
                @"C:\Games\Unknown",
                ExecutableType.Unknown);

            var viewModel = new GameViewModel(gameInfo);

            Assert.Equal("unknown", viewModel.Runner);
        }

        [Fact]
        public void GameViewModel_ThrowsOnNullGameInfo()
        {
            Assert.Throws<ArgumentNullException>(() => new GameViewModel(null!));
        }
    }

    public sealed class GameLibraryViewModelTests
    {
        private readonly Mock<IGameCatalog> _mockCatalog;
        private readonly GameLibraryViewModel _viewModel;

        public GameLibraryViewModelTests()
        {
            _mockCatalog = new Mock<IGameCatalog>();
            _viewModel = new GameLibraryViewModel(_mockCatalog.Object);
        }

        [Fact]
        public void GameLibraryViewModel_InitialState_IsCorrect()
        {
            Assert.Empty(_viewModel.Games);
            Assert.Equal("Ready", _viewModel.ScanStatus);
            Assert.Equal(0, _viewModel.TotalGames);
            Assert.Equal("No games found", _viewModel.GamesFoundText);
            Assert.False(_viewModel.IsBusy);
        }

        [Fact]
        public async Task ScanGamesAsync_LoadsGames_WhenCatalogReturnsGames()
        {
            var game1 = new GameInfo("id1", "Game A", @"C:\Games\A\a.exe", @"C:\Games\A", ExecutableType.WindowsExecutable);
            var game2 = new GameInfo("id2", "Game B", @"C:\Games\B\b.exe", @"C:\Games\B", ExecutableType.UwpApplication);

            _mockCatalog.Setup(c => c.GetGamesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<GameInfo>)new List<GameInfo> { game1, game2 });

            await _viewModel.ScanGamesAsync();

            Assert.Equal(2, _viewModel.Games.Count);
            Assert.Equal(2, _viewModel.TotalGames);
            Assert.Equal("2 games found", _viewModel.GamesFoundText);
            Assert.Equal("Game A", _viewModel.Games[0].Name);
            Assert.Equal("Game B", _viewModel.Games[1].Name);
            Assert.Equal("wine (research)", _viewModel.Games[0].Runner);
            Assert.Equal("uwp", _viewModel.Games[1].Runner);
            Assert.False(_viewModel.IsBusy);
        }

        [Fact]
        public async Task ScanGamesAsync_ShowsNoGames_WhenCatalogReturnsEmpty()
        {
            _mockCatalog.Setup(c => c.GetGamesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<GameInfo>)new List<GameInfo>());

            await _viewModel.ScanGamesAsync();

            Assert.Empty(_viewModel.Games);
            Assert.Equal(0, _viewModel.TotalGames);
            Assert.Equal("No games found", _viewModel.GamesFoundText);
        }

        [Fact]
        public async Task ScanGamesAsync_UpdatesStatus_WhenExceptionThrown()
        {
            _mockCatalog.Setup(c => c.GetGamesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Catalog failed"));

            await _viewModel.ScanGamesAsync();

            Assert.Contains("Error: Catalog failed", _viewModel.ScanStatus);
            Assert.False(_viewModel.IsBusy);
        }

        [Fact]
        public async Task ScanGamesAsync_RespectsCancellationToken()
        {
            var cts = new CancellationTokenSource();
            _mockCatalog.Setup(c => c.GetGamesAsync(cts.Token))
                .ThrowsAsync(new OperationCanceledException());

            await _viewModel.ScanGamesAsync(cts.Token);

            Assert.Equal("Scan cancelled", _viewModel.ScanStatus);
            Assert.False(_viewModel.IsBusy);
        }

        [Fact]
        public async Task ScanGamesAsync_DoesNotRunTwiceConcurrently()
        {
            var tcs = new TaskCompletionSource<IReadOnlyCollection<GameInfo>>();
            _mockCatalog.Setup(c => c.GetGamesAsync(It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            var firstScan = _viewModel.ScanGamesAsync();
            var secondScan = _viewModel.ScanGamesAsync();

            Assert.True(_viewModel.IsBusy);

            tcs.SetResult(new List<GameInfo>());
            await Task.WhenAll(firstScan, secondScan);

            Assert.False(_viewModel.IsBusy);
            _mockCatalog.Verify(c => c.GetGamesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ScanGamesAsync_GamesAreSortedByName()
        {
            var game1 = new GameInfo("id1", "Zebra Game", @"C:\Games\Z\z.exe", @"C:\Games\Z", ExecutableType.WindowsExecutable);
            var game2 = new GameInfo("id2", "Alpha Game", @"C:\Games\A\a.exe", @"C:\Games\A", ExecutableType.WindowsExecutable);
            var game3 = new GameInfo("id3", "Beta Game", @"C:\Games\B\b.exe", @"C:\Games\B", ExecutableType.WindowsExecutable);

            _mockCatalog.Setup(c => c.GetGamesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<GameInfo>)new List<GameInfo> { game1, game2, game3 });

            await _viewModel.ScanGamesAsync();

            Assert.Equal("Alpha Game", _viewModel.Games[0].Name);
            Assert.Equal("Beta Game", _viewModel.Games[1].Name);
            Assert.Equal("Zebra Game", _viewModel.Games[2].Name);
        }

        [Fact]
        public void ScanGamesCommand_CanExecute_WhenNotBusy()
        {
            Assert.True(_viewModel.ScanGamesCommand.CanExecute(null));
        }

        [Fact]
        public async Task ScanGamesCommand_CannotExecute_WhenBusy()
        {
            var tcs = new TaskCompletionSource<IReadOnlyCollection<GameInfo>>();
            _mockCatalog.Setup(c => c.GetGamesAsync(It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            var scanTask = _viewModel.ScanGamesAsync();

            Assert.False(_viewModel.ScanGamesCommand.CanExecute(null));

            tcs.SetResult(new List<GameInfo>());
            await scanTask;

            Assert.True(_viewModel.ScanGamesCommand.CanExecute(null));
        }
    }
}