using System;
using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;

namespace XNative.Platform.Settings
{
    /// <summary>
    /// Registration entry point for the persisted settings layer.
    /// Kept separate from <see cref="PlatformModule"/> so heads opt in
    /// explicitly; the platform module itself is unchanged.
    /// </summary>
    /// <remarks>
    /// Wiring note for the composition roots (integration step): call
    /// <c>AddXNativeSettings()</c> alongside
    /// <c>AddXNativePlatform()</c>. The platform registration must
    /// provide an <see cref="IStorageService"/> (it does, via
    /// <c>FileStorageService</c>), because
    /// <see cref="JsonSettingsService"/> is built on top of it. A head
    /// that wants settings-driven options then resolves
    /// <see cref="ISettingsService"/>, loads the settings, and applies
    /// them with <see cref="SettingsOptionsApplier"/> before the
    /// catalog and log services are first used.
    /// </remarks>
    public static class SettingsModule
    {
        /// <summary>
        /// Registers <see cref="ISettingsService"/> backed by
        /// <see cref="JsonSettingsService"/> as a singleton.
        /// </summary>
        /// <param name="services">The service collection to populate.</param>
        /// <returns>The same service collection, to allow chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is <c>null</c>.
        /// </exception>
        public static IServiceCollection AddXNativeSettings(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<ISettingsService, JsonSettingsService>();
            return services;
        }
    }
}
