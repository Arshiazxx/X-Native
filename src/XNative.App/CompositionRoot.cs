using System;
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
    }
}
