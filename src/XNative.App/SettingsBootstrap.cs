using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;
using XNative.Core.Settings;
using XNative.Platform;
using XNative.Platform.Settings;

namespace XNative.App
{
    /// <summary>
    /// Identifies which source supplied the effective games root path
    /// after the precedence rules of
    /// <see cref="SettingsBootstrap.ResolveGamesRoot(AppSettings, string)"/>
    /// are applied.
    /// </summary>
    public enum GamesRootSource
    {
        /// <summary>An explicit path argument on the command line won.</summary>
        CommandLine = 0,

        /// <summary>The persisted settings supplied the path.</summary>
        Settings = 1,

        /// <summary>Neither source supplied a path; the platform module default applies.</summary>
        PlatformDefault = 2,
    }

    /// <summary>
    /// The outcome of resolving the games root path: the winning path
    /// and the source that supplied it.
    /// </summary>
    public sealed class GamesRootResolution
    {
        internal GamesRootResolution(string? path, GamesRootSource source)
        {
            Path = path;
            Source = source;
        }

        /// <summary>
        /// Gets the winning games root path, or <c>null</c> when
        /// <see cref="Source"/> is
        /// <see cref="GamesRootSource.PlatformDefault"/> (the platform
        /// module's own default then applies untouched).
        /// </summary>
        public string? Path { get; }

        /// <summary>
        /// Gets the source that supplied <see cref="Path"/>.
        /// </summary>
        public GamesRootSource Source { get; }
    }

    /// <summary>
    /// The outcome of loading the persisted settings at startup: the
    /// settings to compose with, plus a one-line warning when the load
    /// itself failed and the defaults were substituted.
    /// </summary>
    public sealed class SettingsLoadResult
    {
        internal SettingsLoadResult(AppSettings settings, string? loadWarning)
        {
            Settings = settings;
            LoadWarning = loadWarning;
        }

        /// <summary>
        /// Gets the loaded settings, or a default
        /// <see cref="AppSettings"/> when nothing was stored, the stored
        /// data was corrupt, or the load failed. Never <c>null</c>.
        /// </summary>
        public AppSettings Settings { get; }

        /// <summary>
        /// Gets a one-line, user-presentable warning describing why the
        /// defaults were substituted, or <c>null</c> when the stored
        /// settings (or their documented absence) were used. Corrupt
        /// stored data is <em>not</em> a load failure: the settings
        /// service already maps it to the defaults by contract, so no
        /// warning is produced for it.
        /// </summary>
        public string? LoadWarning { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Settings"/> is a
        /// fallback substituted after a failed load (see
        /// <see cref="LoadWarning"/>) rather than the outcome of a
        /// successful one.
        /// </summary>
        public bool UsedFallback => LoadWarning != null;
    }

    /// <summary>
    /// Loads the persisted <see cref="AppSettings"/> before the real
    /// composition is built, and resolves the precedence between the
    /// command line, the settings and the platform defaults.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Why a pre-composition: options such as the catalog root and the
    /// log minimum level are baked into the platform singletons the
    /// first time they are resolved, so the settings must be known
    /// <em>before</em> the real provider exists — they cannot be loaded
    /// from the real provider and applied afterwards. Loading therefore
    /// happens in two steps. <see cref="LoadAsync(CancellationToken)"/> builds a minimal
    /// throwaway provider whose registrations come from
    /// <c>AddXNativePlatform()</c> (the platform module stays the single
    /// owner of where storage lives on disk) plus
    /// <c>AddXNativeSettings()</c>, resolves only
    /// <see cref="ISettingsService"/> — which pulls in only its
    /// <see cref="IStorageService"/> dependency — loads the settings,
    /// and disposes the provider. The caller then hands the loaded
    /// settings to the settings-aware
    /// <see cref="CompositionRoot.Build(AppSettings, string)"/>, which
    /// applies them onto the options of the real composition.
    /// </para>
    /// <para>
    /// Failure honesty: loading never throws. The settings service
    /// already maps corrupt stored data to the defaults; if storage
    /// itself throws, <see cref="LoadAsync(CancellationToken)"/> substitutes the defaults
    /// as well and reports a one-line warning on
    /// <see cref="SettingsLoadResult.LoadWarning"/> so the caller can
    /// surface it (the CLI prints it to standard error) without the
    /// application failing to start. The guard covers the whole load,
    /// including building the throwaway provider and resolving the
    /// settings service from it, so a construction failure is a
    /// fallback too, not an escape. Cancellation is not swallowed.
    /// </para>
    /// <para>
    /// Games root precedence, highest first: an explicit command line
    /// path argument, then the persisted
    /// <see cref="AppSettings.GamesRootPath"/>, then the platform module
    /// default. See <see cref="ResolveGamesRoot(AppSettings, string)"/>.
    /// </para>
    /// </remarks>
    public static class SettingsBootstrap
    {
        /// <summary>
        /// Loads the persisted settings through a minimal
        /// pre-composition (see the type remarks), disposing it before
        /// returning. Never throws for a settings or storage failure —
        /// or for a failure to build the pre-composition or resolve
        /// the settings service from it: the result then carries the
        /// defaults and a <see cref="SettingsLoadResult.LoadWarning"/>.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the load.</param>
        /// <returns>The load outcome; never <c>null</c>.</returns>
        public static async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using ServiceProvider provider = BuildSettingsProvider();
                return await LoadAsync(provider, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Do not catch general exception types: a settings load failure must never stop the application; the defaults are substituted and the failure is reported as a warning instead.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                return CreateFallbackResult(exception);
            }
        }

        /// <summary>
        /// Loads the settings through the
        /// <see cref="ISettingsService"/> registered in an existing
        /// service provider. This is the testable core of the
        /// construction step of
        /// <see cref="LoadAsync(CancellationToken)"/>: a provider that
        /// cannot supply the settings service — the registration is
        /// missing, or constructing it fails — yields the defaults and
        /// a <see cref="SettingsLoadResult.LoadWarning"/> instead of
        /// throwing, exactly like a failed load. The provider is
        /// <em>not</em> disposed; its lifetime stays with the caller.
        /// </summary>
        /// <param name="provider">
        /// The provider to resolve the settings service from.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the load.</param>
        /// <returns>The load outcome; never <c>null</c>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="provider"/> is <c>null</c>.
        /// </exception>
        public static async Task<SettingsLoadResult> LoadAsync(
            IServiceProvider provider,
            CancellationToken cancellationToken = default)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            ISettingsService settingsService;
            try
            {
                settingsService = provider.GetRequiredService<ISettingsService>();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Do not catch general exception types: a settings load failure must never stop the application; the defaults are substituted and the failure is reported as a warning instead.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                return CreateFallbackResult(exception);
            }

            return await LoadAsync(settingsService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Loads the settings from an explicit
        /// <see cref="ISettingsService"/>. This is the testable core of
        /// <see cref="LoadAsync(CancellationToken)"/>: the fallback and warning behaviour
        /// lives here, so it can be exercised with a failing service
        /// double without touching real storage.
        /// </summary>
        /// <param name="settingsService">The service to load from.</param>
        /// <param name="cancellationToken">Token to cancel the load.</param>
        /// <returns>The load outcome; never <c>null</c>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="settingsService"/> is <c>null</c>.
        /// </exception>
        public static async Task<SettingsLoadResult> LoadAsync(
            ISettingsService settingsService,
            CancellationToken cancellationToken = default)
        {
            if (settingsService == null)
            {
                throw new ArgumentNullException(nameof(settingsService));
            }

            try
            {
                AppSettings settings = await settingsService.GetAsync(cancellationToken).ConfigureAwait(false);
                return new SettingsLoadResult(settings, null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Do not catch general exception types: a settings load failure must never stop the application; the defaults are substituted and the failure is reported as a warning instead.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                return CreateFallbackResult(exception);
            }
        }

        /// <summary>
        /// Resets the persisted settings to the defaults through a
        /// minimal pre-composition (see the type remarks). Unlike
        /// <see cref="LoadAsync(CancellationToken)"/>, a storage failure here is the
        /// command's own failure, so it propagates to the caller.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the reset.</param>
        /// <returns>A task representing the reset.</returns>
        public static async Task ResetAsync(CancellationToken cancellationToken = default)
        {
            using ServiceProvider provider = BuildSettingsProvider();
            ISettingsService settingsService = provider.GetRequiredService<ISettingsService>();
            await settingsService.ResetAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves which games root path wins: an explicit command
        /// line path beats the persisted
        /// <see cref="AppSettings.GamesRootPath"/>, which beats the
        /// platform module default. Whitespace-only values count as
        /// "not supplied" on both levels.
        /// </summary>
        /// <param name="settings">The loaded settings.</param>
        /// <param name="gamesRootPath">
        /// The explicit command line path, or <c>null</c> when the
        /// command line did not supply one.
        /// </param>
        /// <returns>The winning path and its source.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="settings"/> is <c>null</c>.
        /// </exception>
        public static GamesRootResolution ResolveGamesRoot(AppSettings settings, string? gamesRootPath)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!string.IsNullOrWhiteSpace(gamesRootPath))
            {
                return new GamesRootResolution(gamesRootPath, GamesRootSource.CommandLine);
            }

            if (!string.IsNullOrWhiteSpace(settings.GamesRootPath))
            {
                return new GamesRootResolution(settings.GamesRootPath, GamesRootSource.Settings);
            }

            return new GamesRootResolution(null, GamesRootSource.PlatformDefault);
        }

        /// <summary>
        /// Returns the settings the composition must apply: a clone of
        /// <paramref name="settings"/> whose
        /// <see cref="AppSettings.GamesRootPath"/> is the winner of
        /// <see cref="ResolveGamesRoot(AppSettings, string)"/> (or
        /// <c>null</c> when the platform default wins, so the options
        /// applier leaves the platform default untouched). The input
        /// instance is never mutated, so a cached settings instance can
        /// be reused safely.
        /// </summary>
        /// <param name="settings">The loaded settings.</param>
        /// <param name="gamesRootPath">
        /// The explicit command line path, or <c>null</c> when the
        /// command line did not supply one.
        /// </param>
        /// <returns>The effective settings to apply.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="settings"/> is <c>null</c>.
        /// </exception>
        public static AppSettings ResolveEffectiveSettings(AppSettings settings, string? gamesRootPath)
        {
            GamesRootResolution resolution = ResolveGamesRoot(settings, gamesRootPath);

            AppSettings effective = settings.Clone();
            effective.GamesRootPath = resolution.Path;
            return effective;
        }

        private static SettingsLoadResult CreateFallbackResult(Exception exception)
        {
            return new SettingsLoadResult(
                AppSettings.CreateDefault(),
                $"Warning: Could not load the saved settings ({exception.Message}); using defaults.");
        }

        private static ServiceProvider BuildSettingsProvider()
        {
            // The registrations come from the platform module so the
            // storage root has exactly one owner; only the settings
            // service (and through it the storage service) is ever
            // resolved from this provider, and the provider is disposed
            // by the caller as soon as the load completes.
            return new ServiceCollection()
                .AddXNativePlatform()
                .AddXNativeSettings()
                .BuildServiceProvider();
        }
    }
}
