using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;

namespace XNative.Plugins
{
    /// <summary>
    /// Registration entry point of the plugins module.
    /// </summary>
    public static class PluginsModule
    {
        /// <summary>
        /// Registers the plugins module services: the module descriptor,
        /// an empty in-memory <see cref="IPluginCatalog"/> (the default;
        /// a head that ships plugins registers its own catalog or plugin
        /// instances to replace it), a <see cref="PluginHost"/> built from
        /// the registered <see cref="ILogService"/> and the application
        /// base directory, and a <see cref="PluginManager"/> over the
        /// catalog.
        /// </summary>
        /// <remarks>
        /// Plugins are in-process only: on Xbox / UWP the sandbox blocks
        /// loading arbitrary assemblies at runtime, so there is no folder
        /// scanning here by design. The plugin directory handed to the
        /// host is the application base directory
        /// (<see cref="AppDomain.BaseDirectory"/>), where an in-package
        /// plugin's companion files live.
        /// </remarks>
        /// <param name="services">The service collection to populate.</param>
        /// <returns>The same service collection, to allow chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is <c>null</c>.</exception>
        public static IServiceCollection AddXNativePlugins(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IModuleDescriptor, PluginsModuleDescriptor>();
            services.AddSingleton<IPluginCatalog>(
                _ => new InMemoryPluginCatalog(new List<IPlugin>()));
            services.AddSingleton<IPluginHost>(serviceProvider => new PluginHost(
                serviceProvider.GetRequiredService<ILogService>(),
                AppDomain.CurrentDomain.BaseDirectory));
            services.AddSingleton<PluginManager>();
            return services;
        }
    }
}
