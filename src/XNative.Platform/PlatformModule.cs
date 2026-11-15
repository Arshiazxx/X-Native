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
        /// Registers the platform module services, including the desktop
        /// <see cref="IProcessService"/> backed by
        /// <see cref="System.Diagnostics.Process"/>.
        /// </summary>
        /// <param name="services">The service collection to populate.</param>
        /// <returns>The same service collection, to allow chaining.</returns>
        public static IServiceCollection AddXNativePlatform(this IServiceCollection services)
        {
            return AddPlatformServices(services, includeProcessService: true);
        }

        /// <summary>
        /// Registers the platform module services <em>except</em>
        /// <see cref="IProcessService"/>. Hosts whose sandbox forbids
        /// starting processes (Xbox / UWP) compose this variant and
        /// register their own honest <see cref="IProcessService"/>: the
        /// desktop implementation wraps
        /// <see cref="System.Diagnostics.Process"/>, which cannot start
        /// processes inside the UWP sandbox, and claiming otherwise
        /// would promise a capability the platform does not have.
        /// </summary>
        /// <param name="services">The service collection to populate.</param>
        /// <returns>The same service collection, to allow chaining.</returns>
        public static IServiceCollection AddXNativePlatformCore(this IServiceCollection services)
        {
            return AddPlatformServices(services, includeProcessService: false);
        }

        private static IServiceCollection AddPlatformServices(
            IServiceCollection services,
            bool includeProcessService)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IModuleDescriptor, PlatformModuleDescriptor>();

            // Default roots: the game catalog scans the user's documents
            // folder, while storage and logs live under
            // LocalApplicationData/XNative. Registration only computes
            // paths; nothing is created on disk until a service is
            // first used.
            string appDataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "XNative");

            services.Configure<LocalGameCatalogOptions>(options =>
            {
                options.RootPath = DefaultGamesRootPath(appDataRoot);
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

            if (includeProcessService)
            {
                // SystemProcessService is IDisposable and owns its tracked
                // process handles; the singleton registration lets the
                // container dispose it at provider shutdown.
                services.AddSingleton<IProcessService, SystemProcessService>();
            }

            services.AddSingleton<IGameCatalog, LocalGameCatalog>();
            return services;
        }

        /// <summary>
        /// Resolves the default game catalog root. The documents folder
        /// is the default whenever the platform resolves it; some
        /// environments (notably Linux hosts without XDG user
        /// directories, such as a bare root shell) report it as empty,
        /// and an empty root would silently catalogue nothing, so the
        /// fallback is the "Games" folder under the same
        /// LocalApplicationData/XNative root that storage and logs use.
        /// </summary>
        /// <param name="appDataRoot">The LocalApplicationData/XNative
        /// root the other defaults are built from.</param>
        /// <returns>The documents folder when it resolves to a
        /// non-empty path; otherwise the fallback games root.</returns>
        private static string DefaultGamesRootPath(string appDataRoot)
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrWhiteSpace(documents)
                ? Path.Combine(appDataRoot, "Games")
                : documents;
        }
    }
}
