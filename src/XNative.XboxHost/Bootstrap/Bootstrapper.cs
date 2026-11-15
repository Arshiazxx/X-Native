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
using XNative.XboxHost.Services;

namespace XNative.XboxHost.Bootstrap
{
    /// <summary>
    /// Composition root of the Xbox host. It uses the same module
    /// registration pattern as the development host, so the modules stay
    /// unaware of the head that starts them.
    /// </summary>
    public static class Bootstrapper
    {
        private static ServiceProvider? _provider;

        /// <summary>
        /// The service provider of the running application.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the host has not been started.</exception>
        public static IServiceProvider Services =>
            _provider ?? throw new InvalidOperationException("The host has not been started yet.");

        /// <summary>
        /// Indicates whether the host has already been started.
        /// </summary>
        public static bool IsStarted => _provider != null;

        /// <summary>
        /// Registers every module of the application.
        /// </summary>
        /// <param name="services">The service collection to populate.</param>
        /// <param name="gamesRootPath">
        /// The directory the local game catalog scans, or <c>null</c> to
        /// keep the platform module default. Games on Xbox live on
        /// removable or external storage rather than in the default
        /// documents folder, so the host passes that location here once
        /// it knows it.
        /// </param>
        /// <returns>The same service collection, to allow chaining.</returns>
        public static IServiceCollection AddXNative(
            this IServiceCollection services,
            string? gamesRootPath = null)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IModuleDescriptor, CoreModuleDescriptor>();

            // The Xbox head composes the platform core, not the full
            // desktop platform module: the desktop IProcessService wraps
            // System.Diagnostics.Process, which the UWP sandbox forbids.
            // The head registers its own honest implementation instead,
            // one that reports the limitation instead of failing at
            // first use.
            services.AddXNativePlatformCore();
            services.AddSingleton<IProcessService, UnavailableProcessService>();
            services.AddXNativeInput();
            services.AddXNativeGraphics();
            services.AddXNativeWine();
            services.AddXNativePlugins();
            services.AddXNativeUI();
            services.AddXboxHost();

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
        /// Builds the container and resolves every module so that failures
        /// surface during startup instead of on first navigation.
        /// </summary>
        /// <returns>The result of the startup sequence.</returns>
        public static StartupReport Start()
        {
            if (_provider != null)
            {
                return StartupReport.Succeeded(_provider);
            }

            try
            {
                ServiceProvider provider = new ServiceCollection()
                    .AddXNative()
                    .BuildServiceProvider(new ServiceProviderOptions
                    {
                        ValidateOnBuild = true,
                        ValidateScopes = true,
                    });

                foreach (IModuleDescriptor module in provider.GetServices<IModuleDescriptor>())
                {
                    _ = module.Id;
                }

                _ = provider.GetRequiredService<IDeviceInfoService>();

                _provider = provider;
                return StartupReport.Succeeded(provider);
            }
            catch (Exception exception)
            {
                return StartupReport.Failed(exception);
            }
        }

        /// <summary>
        /// Disposes the current service provider and clears the started
        /// state, so that <see cref="Start"/> can be called again. This
        /// is test and support tooling: the running application never
        /// calls it, because a UWP host starts exactly once per process.
        /// </summary>
        public static void Reset()
        {
            _provider?.Dispose();
            _provider = null;
        }

        /// <summary>
        /// Resolves a required service from the running host.
        /// </summary>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <returns>The resolved service.</returns>
        public static TService Resolve<TService>()
            where TService : notnull
        {
            return Services.GetRequiredService<TService>();
        }
    }
}
