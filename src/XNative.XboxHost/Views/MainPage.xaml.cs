using System;
using System.Globalization;
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using XNative.Core.Abstractions;
using XNative.Core.Games;
using XNative.Platform.Input;
using XNative.UI.ViewModels;
using XNative.XboxHost.Bootstrap;
using XNative.XboxHost.Input;

namespace XNative.XboxHost.Views
{
    /// <summary>
    /// Startup screen. It reports the modules resolved by the container and
    /// the device the application runs on, plus a game library scanner.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private GamepadInputFeed? _inputFeed;
        private bool _launchInFlight;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainPage"/> class.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();
            VersionText.Text = "Version " + ReadPackageVersion();
        }

        /// <summary>
        /// Startup view model backing the page.
        /// </summary>
        public StartupViewModel? ViewModel { get; private set; }

        /// <summary>
        /// Game library view model.
        /// </summary>
        public GameLibraryViewModel? GameLibrary { get; private set; }

        /// <inheritdoc />
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            StartupReport? report = App.Startup;

            if (report == null || !report.IsSuccess)
            {
                ShowError(report?.Error);
                return;
            }

            StartupViewModel viewModel = Bootstrapper.Resolve<StartupViewModel>();

            try
            {
                viewModel.Load();
            }
            catch (Exception exception)
            {
                // Device detection already ran during startup, so a
                // failure here should be unreachable; if one ever
                // slips through anyway, show it instead of letting it
                // escape navigation, where OnNavigationFailed turns it
                // into a process crash.
                ShowError(exception);
                return;
            }

            ViewModel = viewModel;

            GameLibrary = Bootstrapper.Resolve<GameLibraryViewModel>();

            if (_inputFeed == null)
            {
                _inputFeed = new GamepadInputFeed(Bootstrapper.Resolve<ManualInputService>());
            }

            _inputFeed.Start();

            Bindings.Update();
        }

        /// <inheritdoc />
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _inputFeed?.Stop();

            base.OnNavigatedFrom(e);
        }

        private async void OnLaunchButtonClick(object sender, RoutedEventArgs e)
        {
            // Re-entry guard: the view model's LaunchSelectedAsync
            // guards itself with IsLaunching, and this hand-rolled
            // copy needs the equivalent or a double-tap runs two
            // launches concurrently against the singleton runner,
            // their continuations racing to write the status text.
            if (_launchInFlight)
            {
                return;
            }

            GameViewModel? selected = GameLibrary?.SelectedGame;

            if (selected == null)
            {
                LaunchStatusText.Text = "Select a game to launch.";
                return;
            }

            _launchInFlight = true;

            try
            {
                IGameCatalog catalog = Bootstrapper.Resolve<IGameCatalog>();
                GameInfo? game = await catalog.FindByExecutablePathAsync(selected.ExecutablePath);

                if (game == null)
                {
                    LaunchStatusText.Text = "The selected game is no longer in the catalog. Scan again.";
                    return;
                }

                IGameRunner runner = Bootstrapper.Resolve<IGameRunner>();
                LaunchResult result;

                // Mirror the desktop launch path: a game's manifest
                // arguments travel through LaunchOptions instead of
                // being silently dropped.
                if (!string.IsNullOrWhiteSpace(game.LaunchArguments))
                {
                    var options = new LaunchOptions { Arguments = game.LaunchArguments };
                    result = await runner.LaunchAsync(game, options);
                }
                else
                {
                    result = await runner.LaunchAsync(game);
                }

                if (result.Success)
                {
                    LaunchStatusText.Text = string.IsNullOrWhiteSpace(result.Message)
                        ? "Launched " + game.Name + "."
                        : result.Message!;
                }
                else
                {
                    LaunchStatusText.Text = string.IsNullOrWhiteSpace(result.Message)
                        ? "Launch failed for " + game.Name + "."
                        : result.Message!;
                    ErrorText.Text = LaunchStatusText.Text;
                    ErrorText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception exception)
            {
                LaunchStatusText.Text = "Launch failed: " + exception.Message;
                ErrorText.Text = LaunchStatusText.Text;
                ErrorText.Visibility = Visibility.Visible;
            }
            finally
            {
                _launchInFlight = false;
            }
        }

        private void ShowError(Exception? error)
        {
            ErrorText.Text = "Startup failed: " + (error?.Message ?? "the host did not start.");
            ErrorText.Visibility = Visibility.Visible;
        }

        private static string ReadPackageVersion()
        {
            PackageVersion version = Package.Current.Id.Version;

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1}.{2}",
                version.Major,
                version.Minor,
                version.Build);
        }
    }
}
