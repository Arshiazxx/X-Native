using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;
using XNative.UI.ViewModels;

namespace XNative.UI
{
    /// <summary>
    /// Registration entry point of the presentation module.
    /// </summary>
    public static class UiModule
    {
        /// <summary>
        /// Registers the presentation module services.
        /// </summary>
        /// <remarks>
        /// The lifetimes are deliberate and asymmetric.
        /// <see cref="GameLibraryViewModel"/> is a singleton: it owns the
        /// scanned game library, and a UWP/WinUI host's pages are not
        /// cached, so the main page resolves the view model again on every
        /// navigation. A transient registration would hand each navigation
        /// a fresh, empty view model and force a full catalog rescan;
        /// the singleton makes re-navigation rebind the instance that
        /// already holds the scan, the search text, and the selection.
        /// The singleton is per provider: a host that disposes its
        /// provider and builds a new one (the Xbox bootstrapper's failed
        /// start and <c>Reset</c> paths) still gets a fresh view model
        /// from the new provider. Its dependencies
        /// (<see cref="IGameCatalog"/> and <see cref="IGameRunner"/>) are
        /// themselves singletons, and the view model holds no disposable
        /// or provider-scoped state, so the longer lifetime captures and
        /// leaks nothing. <see cref="StartupViewModel"/> stays transient:
        /// its <see cref="StartupViewModel.Load"/> is synchronous and
        /// cheap, it carries no scan state, and re-resolving it per
        /// navigation simply re-reads the current module and device
        /// state.
        /// </remarks>
        /// <param name="services">The service collection to populate.</param>
        /// <returns>The same service collection, to allow chaining.</returns>
        public static IServiceCollection AddXNativeUI(this IServiceCollection services)
        {
            services.AddSingleton<IModuleDescriptor, UiModuleDescriptor>();
            services.AddTransient<StartupViewModel>();
            services.AddSingleton<GameLibraryViewModel>();
            return services;
        }
    }
}
