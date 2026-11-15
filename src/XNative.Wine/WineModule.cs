using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;
using XNative.Core.Games;
using XNative.Wine.Profiles;
using XNative.Wine.Runtime;

namespace XNative.Wine
{
    /// <summary>
    /// Registration entry point of the compatibility layer module.
    /// </summary>
    public static class WineModule
    {
        /// <summary>
        /// Registers the compatibility layer module services.
        /// </summary>
        /// <param name="services">The service collection to populate.</param>
        /// <returns>The same service collection, to allow chaining.</returns>
        public static IServiceCollection AddXNativeWine(this IServiceCollection services)
        {
            services.AddSingleton<IModuleDescriptor, WineModuleDescriptor>();
            services.AddSingleton<IGameProfileStore>(_ => new InMemoryGameProfileStore(new GameProfile[0]));
            services.AddSingleton<IGameRunner, CompatibilityGameRunner>();
            services.AddSingleton<ICompatibilityLayer, WineCompatibilityLayer>();
            return services;
        }
    }
}
