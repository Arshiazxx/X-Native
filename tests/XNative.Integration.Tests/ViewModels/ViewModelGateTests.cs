using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core;
using XNative.Core.Abstractions;
using XNative.Core.Devices;
using XNative.Integration.Tests.Doubles;
using XNative.UI.Mvvm;
using XNative.UI.ViewModels;
using Xunit;

namespace XNative.Integration.Tests.ViewModels
{
    /// <summary>
    /// Behaviour gates for the view-model layer: error and cancellation
    /// surfacing in <see cref="GameLibraryViewModel"/>, the missing
    /// "xnative.ui" module in <see cref="StartupViewModel"/>, and the
    /// <see cref="RelayCommand"/> execute guard.
    /// </summary>
    public sealed class ViewModelGateTests
    {
        // ---------- GameLibraryViewModel error / cancellation surfacing ----------

        [Fact]
        public async Task GameLibrary_CatalogThrowsInvalidOperation_ScanStatusSurfacesErrorAndBusyResets()
        {
            var viewModel = new GameLibraryViewModel(
                StubGameCatalog.Throwing(new InvalidOperationException("catalog exploded")));

            await viewModel.ScanGamesAsync();

            Assert.Contains("Error", viewModel.ScanStatus);
            Assert.Contains("catalog exploded", viewModel.ScanStatus);
            Assert.False(viewModel.IsBusy);
            Assert.Empty(viewModel.Games);
            Assert.Equal(0, viewModel.TotalGames);
        }

        [Fact]
        public async Task GameLibrary_CatalogThrowsUnexpectedException_ScanStatusSurfacesErrorAndBusyResets()
        {
            var viewModel = new GameLibraryViewModel(
                StubGameCatalog.Throwing(new IOException("disk gone")));

            await viewModel.ScanGamesAsync();

            Assert.Contains("Error", viewModel.ScanStatus);
            Assert.False(viewModel.IsBusy);
        }

        [Fact]
        public async Task GameLibrary_CatalogCancels_ScanStatusShowsCancelledAndBusyResets()
        {
            var viewModel = new GameLibraryViewModel(
                StubGameCatalog.Throwing(new OperationCanceledException()));

            await viewModel.ScanGamesAsync();

            Assert.Equal("Scan cancelled", viewModel.ScanStatus);
            Assert.False(viewModel.IsBusy);
        }

        [Fact]
        public async Task GameLibrary_CatalogCancelsWithTaskCanceled_ScanStatusShowsCancelledAndBusyResets()
        {
            // TaskCanceledException is what an actually-cancelled await
            // surfaces as; it derives from OperationCanceledException.
            var viewModel = new GameLibraryViewModel(
                StubGameCatalog.Throwing(new TaskCanceledException()));

            await viewModel.ScanGamesAsync();

            Assert.Equal("Scan cancelled", viewModel.ScanStatus);
            Assert.False(viewModel.IsBusy);
        }

        [Fact]
        public async Task GameLibrary_PreCancelledToken_ScanEndsCancelledAndNotBusy()
        {
            var viewModel = new GameLibraryViewModel(new StubGameCatalog(token =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyCollection<XNative.Core.Games.GameInfo>>(Array.Empty<XNative.Core.Games.GameInfo>());
            }));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await viewModel.ScanGamesAsync(cts.Token);

            Assert.Equal("Scan cancelled", viewModel.ScanStatus);
            Assert.False(viewModel.IsBusy);
        }

        [Fact]
        public void GameLibrary_NullCatalog_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GameLibraryViewModel(null!));
        }

        // ---------- StartupViewModel module list ----------

        private sealed class StubModule : IModuleDescriptor
        {
            public StubModule(string id, string displayName)
            {
                Id = id;
                DisplayName = displayName;
            }

            public string Id { get; }
            public string DisplayName { get; }
        }

        private sealed class StubDeviceInfoService : IDeviceInfoService
        {
            public DeviceInfo GetDeviceInfo() => DeviceInfo.CreateUnknown();
        }

        [Fact]
        public void Startup_Load_ReportsUiModuleWhenDescriptorIsPresent()
        {
            // Gate for the missing-module defect: UiModule registers a
            // descriptor with id "xnative.ui", but StartupViewModel's
            // ExpectedModuleOrder on base does not contain it, so the UI
            // module is never reported on the startup screen — and if it
            // failed to register, nobody would notice. The end state lists
            // every registered module, including "xnative.ui".
            var modules = new IModuleDescriptor[]
            {
                new CoreModuleDescriptor(),
                new StubModule("xnative.platform", "Platform"),
                new StubModule("xnative.graphics", "Graphics"),
                new StubModule("xnative.plugins", "Plugins"),
                new StubModule("xnative.wine", "Wine"),
                new StubModule("xnative.ui", "UI"),
            };

            var viewModel = new StartupViewModel(modules, new StubDeviceInfoService());
            viewModel.Load();

            ModuleStatusViewModel ui = viewModel.Modules.SingleOrDefault(module => module.Id == "xnative.ui")!;
            Assert.NotNull(ui);
            Assert.True(ui.IsLoaded);
            Assert.False(viewModel.IsBusy);
        }

        [Fact]
        public void Startup_NullArguments_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new StartupViewModel(null!, new StubDeviceInfoService()));
            Assert.Throws<ArgumentNullException>(() => new StartupViewModel(Array.Empty<IModuleDescriptor>(), null!));
        }

        // ---------- RelayCommand ----------

        [Fact]
        public void RelayCommand_Execute_WhenCanExecuteIsFalse_MustNotRunDelegate()
        {
            // Gate: ICommand consumers (buttons) are not the only callers —
            // Execute can be invoked programmatically, and on base it runs
            // the delegate unconditionally, bypassing the CanExecute guard
            // (for the scan command, that guard is the IsBusy check).
            bool ran = false;
            var command = new RelayCommand(_ => ran = true, _ => false);

            Assert.False(command.CanExecute(null));
            command.Execute(null);

            Assert.False(ran);
        }

        [Fact]
        public void RelayCommand_NullExecute_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RelayCommand(null!));
        }

        [Fact]
        public void RelayCommand_Execute_WhenCanExecuteIsTrue_RunsDelegate()
        {
            bool ran = false;
            var command = new RelayCommand(_ => ran = true, _ => true);

            command.Execute(null);

            Assert.True(ran);
        }
    }
}
