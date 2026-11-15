using System;
using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;

namespace XNative.Graphics
{
    /// <summary>
    /// Registration entry point of the graphics module.
    /// </summary>
    public static class GraphicsModule
    {
        /// <summary>
        /// Registers the graphics module services: the module descriptor,
        /// a <see cref="GraphicsBackendRegistry"/> pre-populated with the
        /// <see cref="NullGraphicsBackend"/> (the only backend that exists
        /// today; it renders nothing, see its documentation), the null
        /// backend itself as the resolvable <see cref="IGraphicsBackend"/>,
        /// and a <see cref="GraphicsBackendSelector"/> over the registry.
        /// </summary>
        /// <param name="services">The service collection to populate.</param>
        /// <returns>The same service collection, to allow chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is <c>null</c>.</exception>
        public static IServiceCollection AddXNativeGraphics(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IModuleDescriptor, GraphicsModuleDescriptor>();
            services.AddSingleton<NullGraphicsBackend>();
            services.AddSingleton<IGraphicsBackend>(
                serviceProvider => serviceProvider.GetRequiredService<NullGraphicsBackend>());
            services.AddSingleton<GraphicsBackendRegistry>(serviceProvider =>
            {
                var registry = new GraphicsBackendRegistry();
                registry.Register(serviceProvider.GetRequiredService<NullGraphicsBackend>());
                return registry;
            });
            services.AddSingleton<GraphicsBackendSelector>();
            return services;
        }
    }
}
