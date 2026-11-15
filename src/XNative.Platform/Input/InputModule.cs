using System;
using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;

namespace XNative.Platform.Input
{
    /// <summary>
    /// Registration entry point for the platform input services.
    /// Kept separate from <c>PlatformModule</c>: composition roots opt
    /// into input explicitly by calling <see cref="AddXNativeInput"/>
    /// alongside <c>AddXNativePlatform()</c>.
    /// </summary>
    public static class InputModule
    {
        /// <summary>
        /// Registers <see cref="ManualInputService"/> as the
        /// <see cref="IInputService"/>.
        /// </summary>
        /// <param name="services">The service collection to populate.</param>
        /// <returns>The same service collection, to allow chaining.</returns>
        /// <remarks>
        /// <see cref="ManualInputService"/> is the functional default:
        /// it starts with every slot disconnected (so consumers behave
        /// exactly as with <see cref="NullInputService"/>) and a head
        /// whose OS delivers input as events feeds it through
        /// <see cref="ManualInputService.SetGamepadState"/>. The same
        /// singleton instance is registered as the concrete type too,
        /// so the head and the consumers share one instance.
        /// <see cref="NullInputService"/> is deliberately not
        /// registered: it stays available for heads that want to state
        /// "no input, ever" explicitly in their own composition.
        /// </remarks>
        public static IServiceCollection AddXNativeInput(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<ManualInputService>();
            services.AddSingleton<IInputService>(
                provider => provider.GetRequiredService<ManualInputService>());
            return services;
        }
    }
}
