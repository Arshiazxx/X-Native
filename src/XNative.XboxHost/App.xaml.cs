using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using XNative.XboxHost.Bootstrap;
using XNative.XboxHost.Views;

namespace XNative.XboxHost
{
    /// <summary>
    /// Application head of X-Native for Xbox Developer Mode.
    /// </summary>
    public sealed partial class App : Application
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// </summary>
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
        }

        /// <summary>
        /// The result of the startup sequence, consumed by the first page.
        /// The setter is internal so that
        /// <see cref="Bootstrapper.Reset"/> can clear the cached report
        /// together with the provider it describes; a stale success here
        /// would hand out a disposed provider.
        /// </summary>
        public static StartupReport? Startup { get; internal set; }

        /// <inheritdoc />
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (e == null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);

            // A failed start is not cached: the next activation retries
            // Bootstrapper.Start() instead of reusing the failure for
            // the rest of the process lifetime.
            if (Startup == null || !Startup.IsSuccess)
            {
                Startup = Bootstrapper.Start();
            }

            if (!(Window.Current.Content is Frame rootFrame))
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (!e.PrelaunchActivated)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }

                // Activate only on a real launch: during prelaunch the
                // OS starts the process ahead of time so the real
                // launch is fast, and activating there would surface
                // the window before the user launched anything.
                Window.Current.Activate();
            }
        }

        private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new InvalidOperationException("Failed to load page " + e.SourcePageType.FullName + ".");
        }

        private static void OnSuspending(object sender, SuspendingEventArgs e)
        {
            SuspendingDeferral deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
