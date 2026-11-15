using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;
using XNative.Core.Devices;
using XNative.Core.Games;
using XNative.UI.ViewModels;
using Xunit;

namespace XNative.UI.Tests.ViewModels
{
    /// <summary>
    /// Pins the DI lifetimes <see cref="UiModule.AddXNativeUI"/> promises.
    /// <see cref="GameLibraryViewModel"/> is a singleton per provider: a
    /// UWP/WinUI host's pages are not cached, so the library page
    /// resolves the view model again on every navigation, and a
    /// transient registration would discard the scanned library (and the
    /// search text and selection) on each one, forcing a full rescan.
    /// The singleton is per provider, so a host that disposes and
    /// rebuilds its provider — the Xbox bootstrapper's failed-start and
    /// reset paths — still starts from a fresh view model.
    /// <see cref="StartupViewModel"/> deliberately stays transient: its
    /// load is synchronous and cheap and it holds no scan state.
    /// </summary>
    public sealed class GameLibraryLifetimeTests
    {
        private sealed class CountingGameCatalog : IGameCatalog
        {
            private readonly IReadOnlyCollection<GameInfo> _games;

            public CountingGameCatalog(params GameInfo[] games)
            {
                _games = games;
            }

            public int GetGamesCallCount { get; private set; }

            public Task<IReadOnlyCollection<GameInfo>> GetGamesAsync(CancellationToken cancellationToken = default)
            {
                GetGamesCallCount++;
                return Task.FromResult(_games);
            }

            public Task<GameInfo?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_games.FirstOrDefault(game => game.Id == id));
            }

            public Task<GameInfo?> FindByExecutablePathAsync(string executablePath, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_games.FirstOrDefault(game => game.ExecutablePath == executablePath));
            }

            public Task RefreshAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class StubGameRunner : IGameRunner
        {
            public Task<LaunchResult> LaunchAsync(GameInfo game, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(LaunchResult.Ok("Launched"));
            }

            public Task<LaunchResult> LaunchAsync(GameInfo game, LaunchOptions options, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(LaunchResult.Ok("Launched"));
            }
        }

        private sealed class StubDeviceInfoService : IDeviceInfoService
        {
            public DeviceInfo GetDeviceInfo()
            {
                return DeviceInfo.CreateUnknown();
            }
        }

        private static GameInfo MakeGame(string id, string name)
        {
            return new GameInfo(
                id,
                name,
                $@"C:\Games\{id}\game.exe",
                $@"C:\Games\{id}",
                ExecutableType.WindowsExecutable);
        }

        private static ServiceProvider BuildProvider(IGameCatalog catalog)
        {
            // Mirrors how the heads consume the UI module: the catalog
            // and the runner arrive as the singletons their own modules
            // register, and AddXNativeUI contributes the view models.
            return new ServiceCollection()
                .AddSingleton(catalog)
                .AddSingleton<IGameRunner>(new StubGameRunner())
                .AddSingleton<IDeviceInfoService>(new StubDeviceInfoService())
                .AddXNativeUI()
                .BuildServiceProvider();
        }

        [Fact]
        public void ResolvingGameLibraryViewModel_Twice_ReturnsSameInstance()
        {
            using ServiceProvider provider = BuildProvider(new CountingGameCatalog());

            GameLibraryViewModel first = provider.GetRequiredService<GameLibraryViewModel>();
            GameLibraryViewModel second = provider.GetRequiredService<GameLibraryViewModel>();

            Assert.Same(first, second);
        }

        [Fact]
        public async Task SecondResolve_KeepsScannedState_WithoutRescanning()
        {
            // The Xbox MainPage flow: resolve on navigation, scan, the
            // user navigates away and back, the page resolves again.
            // The second resolve must rebind the scanned library —
            // games, search text, and selection intact — and must not
            // trigger a second catalog scan.
            var catalog = new CountingGameCatalog(
                MakeGame("alpha", "Alpha Game"),
                MakeGame("beta", "Beta Game"));
            using ServiceProvider provider = BuildProvider(catalog);

            GameLibraryViewModel first = provider.GetRequiredService<GameLibraryViewModel>();
            await first.ScanGamesAsync();
            first.SearchText = "alpha";
            first.SelectedGame = first.VisibleGames.Single();

            GameLibraryViewModel second = provider.GetRequiredService<GameLibraryViewModel>();

            Assert.Same(first, second);
            Assert.Equal(1, catalog.GetGamesCallCount);
            Assert.Equal(2, second.Games.Count);
            Assert.Equal("alpha", second.SearchText);
            Assert.Single(second.VisibleGames);
            Assert.Equal("alpha", second.SelectedGame?.Id);
            Assert.Equal("Found 2 game(s)", second.ScanStatus);
        }

        [Fact]
        public void NewProvider_YieldsFreshGameLibraryViewModel()
        {
            // Singleton means per provider, not per process: the Xbox
            // bootstrapper disposes a failed provider and builds a new
            // one on retry (and Reset does the same deliberately), and
            // the new provider's view model must start empty.
            var catalog = new CountingGameCatalog(MakeGame("alpha", "Alpha Game"));

            using ServiceProvider firstProvider = BuildProvider(catalog);
            GameLibraryViewModel first = firstProvider.GetRequiredService<GameLibraryViewModel>();

            using ServiceProvider secondProvider = BuildProvider(catalog);
            GameLibraryViewModel second = secondProvider.GetRequiredService<GameLibraryViewModel>();

            Assert.NotSame(first, second);
            Assert.Empty(second.Games);
            Assert.Equal("Ready", second.ScanStatus);
        }

        [Fact]
        public void ResolvingStartupViewModel_Twice_ReturnsDifferentInstances()
        {
            // Deliberate asymmetry, pinned so it is not "harmonised"
            // away: StartupViewModel.Load is synchronous and cheap and
            // the view model holds no scan state, so it stays transient
            // and each navigation re-reads the current module and
            // device state.
            using ServiceProvider provider = BuildProvider(new CountingGameCatalog());

            StartupViewModel first = provider.GetRequiredService<StartupViewModel>();
            StartupViewModel second = provider.GetRequiredService<StartupViewModel>();

            Assert.NotSame(first, second);
        }
    }
}
