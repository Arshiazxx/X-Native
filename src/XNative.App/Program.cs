using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;
using XNative.Core.Devices;
using XNative.Core.Games;

namespace XNative.App
{
    /// <summary>
    /// Development host of X-Native. It exists so that the composition root
    /// can be executed and verified outside of the Xbox environment.
    /// Running it with a folder argument performs an end-to-end smoke
    /// test of the whole stack on a desktop: it builds the container,
    /// prints the resolved modules and the device description, then
    /// scans that folder with the game catalog.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point of the development host.
        /// </summary>
        /// <param name="args">
        /// Optional arguments. The first argument, when present, is the
        /// games root path the catalog scans.
        /// </param>
        /// <returns>
        /// Zero when the run succeeded; a non-zero exit code when it
        /// failed, with a clear message written to standard error.
        /// </returns>
        public static async Task<int> Main(string[] args)
        {
            try
            {
                string? gamesRootPath = args.Length > 0 ? args[0] : null;

                using ServiceProvider provider = CompositionRoot.Build(gamesRootPath);

                IEnumerable<IModuleDescriptor> modules = provider.GetServices<IModuleDescriptor>();

                Console.WriteLine("X-Native modules:");
                foreach (IModuleDescriptor module in modules)
                {
                    Console.WriteLine($"  {module.Id} - {module.DisplayName}");
                }

                IDeviceInfoService deviceInfoService = provider.GetRequiredService<IDeviceInfoService>();
                DeviceInfo deviceInfo = deviceInfoService.GetDeviceInfo();

                Console.WriteLine();
                Console.WriteLine("Device information:");
                Console.WriteLine($"  Device family: {deviceInfo.DeviceFamily}");
                Console.WriteLine($"  OS version: {deviceInfo.OperatingSystemVersion}");
                Console.WriteLine($"  Architecture: {deviceInfo.Architecture}");
                Console.WriteLine($"  Manufacturer: {deviceInfo.Manufacturer}");
                Console.WriteLine($"  Model: {deviceInfo.Model}");

                IGameCatalog catalog = provider.GetRequiredService<IGameCatalog>();
                IReadOnlyCollection<GameInfo> games = await catalog.GetGamesAsync().ConfigureAwait(false);
                List<GameInfo> orderedGames = games
                    .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Console.WriteLine();
                Console.WriteLine("Games:");
                foreach (GameInfo game in orderedGames)
                {
                    if (game.Architecture != ProcessorArchitecture.Unknown)
                    {
                        Console.WriteLine($"  {game.Name} ({game.Architecture}) - {game.ExecutablePath}");
                    }
                    else
                    {
                        Console.WriteLine($"  {game.Name} - {game.ExecutablePath}");
                    }
                }

                Console.WriteLine($"Games found: {orderedGames.Count}");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Error: {exception.Message}");
                return 1;
            }
        }
    }
}
