using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XNative.Core.Abstractions;
using XNative.Platform.FileSystem;
using XNative.Platform.Games;
using XNative.Platform.Logging;
using XNative.Platform.Processes;
using XNative.Platform.Storage;

namespace XNative.Platform
{
    /// <summary>
    /// Registration entry point of the platform module.
    /// </summary>
    public static class PlatformModule
    {
        /// <summary>
        /// Registers the platform module services.
        /// </summary>
        /// <param name="services">The service collection to populate.</param>
        /// <returns>The same service collection, to allow chaining.</returns>
        public static IServiceCollection AddXNativePlatform(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IModuleDescriptor, PlatformModuleDescriptor>();

            // Default roots live under LocalApplicationData/XNative.
            // Registration only computes paths; nothing is created on
            // disk until a service is first used.
            string appDataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "XNative");

            services.Configure<LocalGameCatalogOptions>(options =>
            {
                options.RootPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            });
            services.Configure<FileStorageServiceOptions>(options =>
            {
                options.RootPath = Path.Combine(appDataRoot, "Storage");
            });
            services.Configure<FileLogServiceOptions>(options =>
            {
                options.LogDirectory = Path.Combine(appDataRoot, "Logs");
            });

            services.AddSingleton<IFileSystemService, SystemFileSystemService>();
            services.AddSingleton<ILogService, FileLogService>();
            services.AddSingleton<IStorageService, FileStorageService>();

            // SystemProcessService is IDisposable and owns its tracked
            // process handles; the singleton registration lets the
            // container dispose it at provider shutdown.
            services.AddSingleton<IProcessService, SystemProcessService>();

            services.AddSingleton<IGameCatalog, LocalGameCatalog>();
            return services;
        }
    }
}
