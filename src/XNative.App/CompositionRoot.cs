using System;
using Microsoft.Extensions.DependencyInjection;
using XNative.Core;
using XNative.Core.Abstractions;
using XNative.Graphics;
using XNative.Platform;
using XNative.Platform.Games;
using XNative.Platform.Input;
using XNative.Plugins;
using XNative.UI;
using XNative.Wine;

namespace XNative.App
{
    /// <summary>
    /// Builds the dependency injection container of the application. The
    /// application head, console or UWP, only calls into this type.
    /// </summary>
    public static class CompositionRoot
    {
        /// <summary>
        /// Registers every module of the application.
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
        /// Builds a service provider containing every module.
        /// </summary>
        /// <returns>The service provider of the application.</returns>
        public static ServiceProvider Build()
        {
            return Build(null);
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
