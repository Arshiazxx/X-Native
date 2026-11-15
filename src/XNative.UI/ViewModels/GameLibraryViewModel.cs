using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Abstractions;
using XNative.Core.Games;
using XNative.UI.Mvvm;

namespace XNative.UI.ViewModels
{
    /// <summary>
    /// View model for the game library screen: the launcher surface of the
    /// application. It owns the full catalog result (<see cref="Games"/>),
    /// the filtered and sorted projection the view binds to
    /// (<see cref="VisibleGames"/>), the current selection, and launching
    /// the selected game when a game runner was supplied.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Thread affinity: this view model is not thread-safe and performs
    /// no internal synchronisation: no locks, no dispatcher marshalling,
    /// no thread-safe collections. A host must interact with it from a
    /// single thread, and in a UWP/WinUI host that thread must be the UI
    /// thread that owns the bound views:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <see cref="Games"/> and <see cref="VisibleGames"/> are plain
    /// <see cref="ObservableCollection{T}"/> instances, mutated by
    /// <see cref="ScanGamesAsync"/> and, for <see cref="VisibleGames"/>,
    /// by the <see cref="SearchText"/> and <see cref="SortMode"/>
    /// setters, which clear and refill it synchronously on the assigning
    /// thread.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="ObservableObject.PropertyChanged"/> and the commands'
    /// <see cref="System.Windows.Input.ICommand.CanExecuteChanged"/>
    /// notifications are raised synchronously, inline, on the thread
    /// that performed the change. Nothing posts them to a UI thread on
    /// the caller's behalf.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="ScanGamesAsync"/> and <see cref="LaunchSelectedAsync"/>
    /// never call ConfigureAwait(false): their awaits capture the
    /// <see cref="SynchronizationContext"/> of the calling thread.
    /// Everything before the first await, including clearing
    /// <see cref="Games"/> and setting <see cref="ViewModelBase.IsBusy"/>,
    /// runs synchronously on the calling thread; everything after an
    /// await (filling the collections, updating the status properties,
    /// restoring the selection, resetting the busy flags) runs on the
    /// captured context. Call both methods on the UI thread: called from
    /// a thread without a synchronization context (for example a
    /// thread-pool thread), the post-await mutations land on a
    /// thread-pool thread and fault against a bound UWP/WinUI view.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="ScanGamesCommand"/> is fire-and-forget: executing it
    /// starts a scan and discards the scan task, so progress and outcome
    /// are observable only through <see cref="ViewModelBase.IsBusy"/>
    /// and <see cref="ScanStatus"/> notifications. Await
    /// <see cref="ScanGamesAsync"/> directly when completion matters.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// The re-entry guards (<see cref="ViewModelBase.IsBusy"/> and
    /// <see cref="IsLaunching"/>) are plain check-then-act flags: they
    /// stop overlapping operations started on the same thread, but
    /// concurrent calls from several threads can pass a check together.
    /// Keep every interaction on the one thread.
    /// </para>
    /// </remarks>
    public sealed class GameLibraryViewModel : ViewModelBase
    {
        private readonly IGameCatalog _gameCatalog;
        private readonly IGameRunner? _gameRunner;
        private readonly ObservableCollection<GameViewModel> _games;
        private readonly ObservableCollection<GameViewModel> _visibleGames;
        private string _scanStatus = "Ready";
        private string _launchStatus = string.Empty;
        private string _searchText = string.Empty;
        private SortMode _sortMode = SortMode.NameAscending;
        private GameViewModel? _selectedGame;
        private bool _isLaunching;
        private int _totalGames;

        /// <summary>
        /// Initializes a new instance of the <see cref="GameLibraryViewModel"/> class
        /// without launch support. <see cref="LaunchSelectedCommand"/> stays
        /// disabled and <see cref="LaunchSelectedAsync"/> is a safe no-op.
        /// </summary>
        /// <param name="gameCatalog">The game catalog service.</param>
        public GameLibraryViewModel(IGameCatalog gameCatalog)
            : this(gameCatalog, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GameLibraryViewModel"/> class.
        /// </summary>
        /// <param name="gameCatalog">The game catalog service.</param>
        /// <param name="gameRunner">
        /// The game runner used to launch the selected game, or <c>null</c>
        /// to disable launching entirely.
        /// </param>
        public GameLibraryViewModel(IGameCatalog gameCatalog, IGameRunner? gameRunner)
        {
            _gameCatalog = gameCatalog ?? throw new ArgumentNullException(nameof(gameCatalog));
            _gameRunner = gameRunner;
            _games = new ObservableCollection<GameViewModel>();
            _visibleGames = new ObservableCollection<GameViewModel>();
            ScanGamesCommand = new RelayCommand(async _ => await ScanGamesAsync(), _ => !IsBusy);
            LaunchSelectedCommand = new AsyncRelayCommand(
                () => LaunchSelectedAsync(),
                () => CanLaunchSelected());
        }

        /// <summary>
        /// Collection of discovered games: the full, unfiltered scan result,
        /// ordered by name ascending. This is the source collection;
        /// <see cref="VisibleGames"/> is what a view should display.
        /// </summary>
        public ObservableCollection<GameViewModel> Games => _games;

        /// <summary>
        /// The games currently visible in the view: <see cref="Games"/>
        /// filtered by <see cref="SearchText"/> and ordered by
        /// <see cref="SortMode"/>. Kept in sync whenever a scan completes,
        /// the search text changes, or the sort mode changes.
        /// </summary>
        public ObservableCollection<GameViewModel> VisibleGames => _visibleGames;

        /// <summary>
        /// Filter text applied to <see cref="VisibleGames"/>. A game is
        /// visible when its name contains the text (case-insensitive
        /// substring match). Empty text shows every game. Assigning
        /// <c>null</c> is treated as assigning empty text.
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                string newValue = value ?? string.Empty;
                if (SetProperty(ref _searchText, newValue))
                {
                    RefreshVisibleGames();
                }
            }
        }

        /// <summary>
        /// Ordering applied to <see cref="VisibleGames"/>. Defaults to
        /// <see cref="ViewModels.SortMode.NameAscending"/>.
        /// </summary>
        public SortMode SortMode
        {
            get => _sortMode;
            set
            {
                if (SetProperty(ref _sortMode, value))
                {
                    RefreshVisibleGames();
                }
            }
        }

        /// <summary>
        /// The currently selected game, or <c>null</c> when nothing is
        /// selected. A rescan preserves the selection when a game with the
        /// same <see cref="GameViewModel.Id"/> is still present; otherwise
        /// the selection is cleared.
        /// </summary>
        public GameViewModel? SelectedGame
        {
            get => _selectedGame;
            set
            {
                if (SetProperty(ref _selectedGame, value))
                {
                    LaunchSelectedCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Indicates whether launching is available at all, i.e. a game
        /// runner was supplied to the constructor.
        /// </summary>
        public bool CanLaunch => _gameRunner != null;

        /// <summary>
        /// Indicates that a launch is currently in flight. Deliberately
        /// separate from <see cref="ViewModelBase.IsBusy"/>, which tracks
        /// scans, so a launch and a scan never fight over one flag.
        /// </summary>
        public bool IsLaunching
        {
            get => _isLaunching;
            private set
            {
                if (SetProperty(ref _isLaunching, value))
                {
                    LaunchSelectedCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Outcome of the most recent launch attempt: the message carried
        /// by the runner's result, for both success and failure. Empty
        /// until the first launch attempt completes.
        /// </summary>
        public string LaunchStatus
        {
            get => _launchStatus;
            private set => SetProperty(ref _launchStatus, value);
        }

        /// <summary>
        /// Current scan status text.
        /// </summary>
        public string ScanStatus
        {
            get => _scanStatus;
            private set => SetProperty(ref _scanStatus, value);
        }

        /// <summary>
        /// Total number of games found.
        /// </summary>
        public int TotalGames
        {
            get => _totalGames;
            private set
            {
                if (SetProperty(ref _totalGames, value))
                {
                    OnPropertyChanged(nameof(GamesFoundText));
                }
            }
        }

        /// <summary>
        /// Text showing games found count.
        /// </summary>
        public string GamesFoundText => TotalGames > 0
            ? $"{TotalGames} game{(TotalGames == 1 ? "" : "s")} found"
            : "No games found";

        /// <summary>
        /// Command to trigger a game scan.
        /// </summary>
        public RelayCommand ScanGamesCommand { get; }

        /// <summary>
        /// Command to launch <see cref="SelectedGame"/>. Enabled only when
        /// a game runner was supplied, a game is selected, and no launch
        /// is already running.
        /// </summary>
        public AsyncRelayCommand LaunchSelectedCommand { get; }

        /// <summary>
        /// Scans for games and updates the collections. The selection is
        /// preserved across the rescan when the selected game's
        /// <see cref="GameViewModel.Id"/> survives it.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the scan operation.</returns>
        public async Task ScanGamesAsync(CancellationToken cancellationToken = default)
        {
            if (IsBusy)
            {
                return;
            }

            string? selectedGameId = _selectedGame?.Id;

            IsBusy = true;
            ScanStatus = "Scanning...";
            _games.Clear();
            TotalGames = 0;
            SelectedGame = null;
            RefreshVisibleGames();

            try
            {
                var games = await _gameCatalog.GetGamesAsync(cancellationToken);

                foreach (var game in games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
                {
                    _games.Add(new GameViewModel(game));
                }

                TotalGames = _games.Count;
                ScanStatus = TotalGames > 0 ? $"Found {TotalGames} game(s)" : "No games found";
                RefreshVisibleGames();

                if (selectedGameId != null)
                {
                    SelectedGame = _games.FirstOrDefault(
                        game => string.Equals(game.Id, selectedGameId, StringComparison.Ordinal));
                }
            }
            catch (OperationCanceledException)
            {
                ScanStatus = "Scan cancelled";
            }
            catch (Exception ex)
            {
                ScanStatus = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                ScanGamesCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Launches <see cref="SelectedGame"/> through the game runner.
        /// Safely does nothing when no game is selected, when no runner was
        /// supplied, or when a launch is already in flight. On completion
        /// <see cref="LaunchStatus"/> carries the result message for both
        /// success and failure. When the game defines
        /// <see cref="GameInfo.LaunchArguments"/>, they are passed to the
        /// runner through <see cref="LaunchOptions.Arguments"/>.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the launch operation.</returns>
        public async Task LaunchSelectedAsync(CancellationToken cancellationToken = default)
        {
            GameViewModel? selected = _selectedGame;
            IGameRunner? runner = _gameRunner;

            if (selected is null || runner is null || IsLaunching)
            {
                return;
            }

            IsLaunching = true;

            try
            {
                GameInfo game = selected.GameInfo;
                LaunchResult result;

                if (!string.IsNullOrWhiteSpace(game.LaunchArguments))
                {
                    var options = new LaunchOptions { Arguments = game.LaunchArguments };
                    result = await runner.LaunchAsync(game, options, cancellationToken);
                }
                else
                {
                    result = await runner.LaunchAsync(game, cancellationToken);
                }

                LaunchStatus = result.Message
                    ?? (result.Success ? "Launched" : "Launch failed");
            }
            catch (OperationCanceledException)
            {
                LaunchStatus = "Launch cancelled";
            }
            catch (Exception ex)
            {
                LaunchStatus = $"Error: {ex.Message}";
            }
            finally
            {
                IsLaunching = false;
            }
        }

        private bool CanLaunchSelected()
        {
            return _gameRunner != null && _selectedGame != null && !_isLaunching;
        }

        private void RefreshVisibleGames()
        {
            IEnumerable<GameViewModel> query = _games;

            if (_searchText.Length > 0)
            {
                string searchText = _searchText;
                query = query.Where(
                    game => game.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            query = _sortMode == SortMode.NameDescending
                ? query.OrderByDescending(game => game.Name, StringComparer.OrdinalIgnoreCase)
                : query.OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase);

            _visibleGames.Clear();

            foreach (GameViewModel game in query)
            {
                _visibleGames.Add(game);
            }
        }
    }
}
