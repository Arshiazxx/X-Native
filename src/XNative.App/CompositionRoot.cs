using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using XNative.Core;
using XNative.Core.Abstractions;
using XNative.Core.Settings;
using XNative.Graphics;
using XNative.Platform;
using XNative.Platform.Games;
using XNative.Platform.Input;
using XNative.Platform.Logging;
using XNative.Platform.Settings;
using XNative.Plugins;
using XNative.UI;
using XNative.Wine;

namespace XNative.App
{
    /// <summary>
    /// Builds the dependency injection container of the application. The
    /// application head, console or UWP, only calls into this type.
    /// </summary>
    /// <remarks>
    /// The settings-aware overloads
    /// (<see cref="AddXNative(IServiceCollection, AppSettings, string)"/>
    /// and <see cref="Build(AppSettings, string)"/>) are what the
    /// console head runs with: the persisted settings are loaded first
    /// (see <see cref="SettingsBootstrap"/>), then applied onto the
    /// catalog and log options here, so the very first scan and the
    /// very first log entry already honour them. The plain overloads
    /// keep the platform defaults and exist for tests and tooling that
    /// do not want settings involved.
    /// </remarks>
    public static class CompositionRoot
    {
        /// <summary>
        /// Registers every module of the application, including the
        /// persisted settings service (<see cref="ISettingsService"/>).
        /// </summary>
        /// <param name="services">The service collection to populate.</param>
        /// <returns>The same service collection, to allow chaining.</returns>
        public static IServiceCollection AddXNative(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IModuleDescriptor, CoreModuleDescriptor>();
            services.AddSingleton<IDeviceInfoService, DesktopDeviceInfoService>();
            services.AddXNativePlatform();
            services.AddXNativeSettings();
            services.AddXNativeInput();
            services.AddXNativeGraphics();
            services.AddXNativeWine();
            services.AddXNativePlugins();
            services.AddXNativeUI();
            return services;
        }

        /// <summary>
        /// Registers every module of the application and, when a games
        /// root is supplied, points the local game catalog at it.
        /// </summary>
        /// <param name="services">The service collection to populate.</param>
        /// <param name="gamesRootPath">
        /// The directory the local game catalog scans, or <c>null</c> to
        /// keep the platform module default.
        /// </param>
        /// <returns>The same service collection, to allow chaining.</returns>
        public static IServiceCollection AddXNative(this IServiceCollection services, string? gamesRootPath)
        {
            services.AddXNative();

            if (!string.IsNullOrWhiteSpace(gamesRootPath))
            {
                services.Configure<LocalGameCatalogOptions>(options =>
                {
                    options.RootPath = gamesRootPath!;
                });
            }

            return services;
        }

        /// <summary>
        /// Registers every module of the application and applies
        /// previously loaded <paramref name="settings"/> onto the
        /// catalog and log options, with an explicit games root path
        /// taking precedence over the settings (see
        /// <see cref="SettingsBootstrap.ResolveGamesRoot(AppSettings, string)"/>).
        /// The composed <see cref="ISettingsService"/> is seeded with
        /// the same effective settings, so resolving it serves the
        /// values this composition runs with instead of re-reading
        /// storage (see <see cref="SeededSettingsService"/>).
        /// </summary>
        /// <param name="services">The service collection to populate.</param>
        /// <param name="settings">
        /// The loaded settings, typically from
        /// <see cref="SettingsBootstrap.LoadAsync(System.Threading.CancellationToken)"/>.
        /// </param>
        /// <param name="gamesRootPath">
        /// An explicit games root path (for example from the command
        /// line), or <c>null</c> when none was supplied.
        /// </param>
        /// <returns>The same service collection, to allow chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or
        /// <paramref name="settings"/> is <c>null</c>.
        /// </exception>
        public static IServiceCollection AddXNative(
            this IServiceCollection services,
            AppSettings settings,
            string? gamesRootPath)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            services.AddXNative();

            // The Configure actions run after the platform module's own
            // defaults (registered inside AddXNative), in registration
            // order, so the settings win over the defaults and the
            // explicit path — folded into the effective settings by
            // ResolveEffectiveSettings — wins over both.
            AppSettings effectiveSettings = SettingsBootstrap.ResolveEffectiveSettings(settings, gamesRootPath);
            services.Configure<LocalGameCatalogOptions>(options =>
                SettingsOptionsApplier.ApplyToCatalog(effectiveSettings, options));
            services.Configure<FileLogServiceOptions>(options =>
                SettingsOptionsApplier.ApplyToLog(effectiveSettings, options));

            // Replace the settings service the plain composition
            // registered with one seeded from the effective settings
            // above. A fresh JsonSettingsService would re-read storage
            // on first use and could serve values that differ from the
            // ones baked into the options (the file may have changed
            // since the bootstrap load); the seeded service serves
            // exactly the effective settings until a save or reset is
            // made through it, which it persists through the real
            // store. Registered last, so it wins single-service
            // resolution over the AddXNativeSettings() registration.
            services.AddSingleton<ISettingsService>(provider =>
                new SeededSettingsService(
                    effectiveSettings,
                    new JsonSettingsService(provider.GetRequiredService<IStorageService>())));

            return services;
        }

        /// <summary>
        /// Builds a service provider containing every module.
        /// </summary>
        /// <returns>The service provider of the application.</returns>
        public static ServiceProvider Build()
        {
            return Build(null);
        }

        /// <summary>
        /// Builds a service provider containing every module, applying
        /// previously loaded <paramref name="settings"/> onto the
        /// catalog and log options, with an explicit games root path
        /// taking precedence over the settings (see
        /// <see cref="SettingsBootstrap.ResolveGamesRoot(AppSettings, string)"/>).
        /// </summary>
        /// <param name="settings">
        /// The loaded settings, typically from
        /// <see cref="SettingsBootstrap.LoadAsync(System.Threading.CancellationToken)"/>.
        /// </param>
        /// <param name="gamesRootPath">
        /// An explicit games root path (for example from the command
        /// line), or <c>null</c> when none was supplied.
        /// </param>
        /// <returns>The service provider of the application.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="settings"/> is <c>null</c>.
        /// </exception>
        public static ServiceProvider Build(AppSettings settings, string? gamesRootPath)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return new ServiceCollection()
                .AddXNative(settings, gamesRootPath)
                .BuildServiceProvider();
        }

        /// <summary>
        /// Builds a service provider containing every module, optionally
        /// reconfiguring the local game catalog to scan
        /// <paramref name="gamesRootPath"/> instead of its default root.
        /// </summary>
        /// <param name="gamesRootPath">
        /// The directory the local game catalog scans, or <c>null</c> to
        /// keep the platform module default.
        /// </param>
        /// <returns>The service provider of the application.</returns>
        public static ServiceProvider Build(string? gamesRootPath)
        {
            return new ServiceCollection()
                .AddXNative(gamesRootPath)
                .BuildServiceProvider();
        }

        /// <summary>
        /// <see cref="ISettingsService"/> that serves the settings a
        /// composition was built with — the instance the bootstrap
        /// loaded and the composition applied to its options — instead
        /// of re-reading storage, so the composed provider can never
        /// observe values that differ from the ones it is running
        /// with. Saves and resets are persisted through a real
        /// <see cref="JsonSettingsService"/> over the same storage;
        /// once one succeeds the seed is dropped and the store's own
        /// (just-written) values are served, so what was last written
        /// and what is served can never disagree either.
        /// </summary>
        private sealed class SeededSettingsService : ISettingsService, IDisposable
        {
            private readonly object _gate = new object();
            private readonly JsonSettingsService _store;
            private AppSettings? _seeded;

            /// <summary>
            /// Initializes a new instance of the
            /// <see cref="SeededSettingsService"/> class.
            /// </summary>
            /// <param name="seeded">
            /// The effective settings to serve until the first save or
            /// reset. Cloned on entry; never mutated afterwards.
            /// </param>
            /// <param name="store">
            /// The store saves and resets are persisted through.
            /// </param>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="seeded"/> or
            /// <paramref name="store"/> is <c>null</c>.
            /// </exception>
            public SeededSettingsService(AppSettings seeded, JsonSettingsService store)
            {
                _seeded = (seeded ?? throw new ArgumentNullException(nameof(seeded))).Clone();
                _store = store ?? throw new ArgumentNullException(nameof(store));
            }

            /// <inheritdoc />
            public Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_gate)
                {
                    if (_seeded != null)
                    {
                        return Task.FromResult(_seeded.Clone());
                    }
                }

                return _store.GetAsync(cancellationToken);
            }

            /// <inheritdoc />
            public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
            {
                // Persist first: when the store throws (validation or
                // storage), nothing was written, so the seed remains
                // the served value — still matching the options the
                // application is running with.
                await _store.SaveAsync(settings, cancellationToken).ConfigureAwait(false);

                lock (_gate)
                {
                    _seeded = null;
                }
            }

            /// <inheritdoc />
            public async Task ResetAsync(CancellationToken cancellationToken = default)
            {
                await _store.ResetAsync(cancellationToken).ConfigureAwait(false);

                lock (_gate)
                {
                    _seeded = null;
                }
            }

            /// <summary>
            /// Releases the underlying store.
            /// </summary>
            public void Dispose()
            {
                _store.Dispose();
            }
        }
    }
}
