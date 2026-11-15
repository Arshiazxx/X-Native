using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XNative.Core.Abstractions;
using XNative.Core.Devices;
using XNative.Core.Games;
using XNative.Platform.Executables;
using XNative.Platform.Games;
using XNative.Platform.Logging;
using XNative.Platform.Settings;

namespace XNative.App
{
    /// <summary>
    /// The development host's command line interface: a small diagnostic
    /// front-end over the same composition root the Xbox head uses. It can
    /// print the resolved modules, device description and catalogued games
    /// (<c>info</c>), list what a catalog scan finds (<c>scan</c>), dump
    /// the PE headers of one executable (<c>inspect</c>), and ask the game
    /// runner to launch a game (<c>launch</c>), reporting the structured
    /// result honestly — no compatibility backend can actually run a PC
    /// game yet, so a launch reports its failure reason instead of
    /// pretending the game started. The persisted settings can be shown
    /// or reset (<c>settings</c>); every command that builds the
    /// composition loads them first (see <see cref="SettingsBootstrap"/>)
    /// and applies them to the catalog and log options, with an explicit
    /// path argument still winning over the persisted games root.
    /// </summary>
    public static class Cli
    {
        /// <summary>Exit code: the command succeeded.</summary>
        public const int ExitSuccess = 0;

        /// <summary>Exit code: the command ran but failed (missing file, invalid PE, failed launch, unexpected error).</summary>
        public const int ExitFailure = 1;

        /// <summary>Exit code: the command line itself was invalid; usage text was printed.</summary>
        public const int ExitUsage = 2;

        /// <summary>
        /// Runs one command.
        /// </summary>
        /// <param name="args">The raw process arguments, without the program name.</param>
        /// <param name="output">Writer for normal output.</param>
        /// <param name="error">Writer for errors and usage text on a usage error.</param>
        /// <returns>
        /// <see cref="ExitSuccess"/>, <see cref="ExitFailure"/> or
        /// <see cref="ExitUsage"/>, as appropriate for what happened.
        /// </returns>
        /// <exception cref="ArgumentNullException">Any argument is <c>null</c>.</exception>
        public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            ParsedCommand command = CommandLine.Parse(args);

            switch (command.Kind)
            {
                case CommandKind.Help:
                    PrintUsage(output);
                    return ExitSuccess;

                case CommandKind.Error:
                    error.WriteLine($"Error: {command.ErrorMessage}");
                    error.WriteLine();
                    PrintUsage(error);
                    return ExitUsage;
            }

            try
            {
                switch (command.Kind)
                {
                    case CommandKind.Info:
                        return await RunInfoAsync(command.Argument, output, error).ConfigureAwait(false);

                    case CommandKind.Scan:
                        return await RunScanAsync(command.Argument, output, error).ConfigureAwait(false);

                    case CommandKind.Inspect:
                        return RunInspect(command.Argument!, output, error);

                    case CommandKind.Launch:
                        return await RunLaunchAsync(command.Argument!, output, error).ConfigureAwait(false);

                    case CommandKind.Settings:
                        return await RunSettingsAsync(command.Argument!, output, error).ConfigureAwait(false);

                    default:
                        error.WriteLine($"Error: Unsupported command '{command.Kind}'.");
                        return ExitFailure;
                }
            }
            catch (Exception exception)
            {
                error.WriteLine($"Error: {exception.Message}");
                return ExitFailure;
            }
        }

        /// <summary>
        /// Prints the usage text describing every command and the exit
        /// code contract.
        /// </summary>
        /// <param name="writer">The writer to print to.</param>
        /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <c>null</c>.</exception>
        public static void PrintUsage(TextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteLine("Usage: XNative.App [command] [argument]");
            writer.WriteLine();
            writer.WriteLine("Commands:");
            writer.WriteLine("  info [path]       Show the resolved modules, the device description,");
            writer.WriteLine("                    input status and the games in the games root");
            writer.WriteLine("                    ([path] overrides the default games root).");
            writer.WriteLine("                    With no command at all this is what runs; a single");
            writer.WriteLine("                    path argument is accepted as the legacy form.");
            writer.WriteLine("  scan [path]       List the games found in [path] (or the default games");
            writer.WriteLine("                    root) with architecture, executable type and path.");
            writer.WriteLine("  inspect <exe>     Print the PE header information of one executable:");
            writer.WriteLine("                    validity, architecture, DLL, managed, subsystem and");
            writer.WriteLine("                    section count. Exits non-zero for a missing or");
            writer.WriteLine("                    invalid file.");
            writer.WriteLine("  launch <id|path>  Launch a game by catalog id or executable path");
            writer.WriteLine("                    through the game runner and print the structured");
            writer.WriteLine("                    result. The exit code is non-zero when the launch");
            writer.WriteLine("                    fails; no backend can run a PC game yet, so a");
            writer.WriteLine("                    failure with its reason is the honest outcome.");
            writer.WriteLine("  settings show     Show the effective settings: the values the app");
            writer.WriteLine("                    runs with after the persisted settings are");
            writer.WriteLine("                    applied over the platform defaults, including");
            writer.WriteLine("                    which source supplied the games root.");
            writer.WriteLine("  settings reset    Discard the persisted settings and restore the");
            writer.WriteLine("                    defaults.");
            writer.WriteLine("  help              Show this usage text.");
            writer.WriteLine();
            writer.WriteLine("Exit codes: 0 = success, 1 = failure, 2 = usage error.");
        }

        private static async Task<int> RunInfoAsync(string? gamesRootPath, TextWriter output, TextWriter error)
        {
            if (!CheckExplicitGamesRoot(gamesRootPath, error))
            {
                return ExitFailure;
            }

            SettingsLoadResult load = await LoadSettingsAsync(error).ConfigureAwait(false);
            using ServiceProvider provider = CompositionRoot.Build(load.Settings, gamesRootPath);

            IEnumerable<IModuleDescriptor> modules = provider.GetServices<IModuleDescriptor>();

            output.WriteLine("X-Native modules:");
            foreach (IModuleDescriptor module in modules)
            {
                output.WriteLine($"  {module.Id} - {module.DisplayName}");
            }

            IDeviceInfoService deviceInfoService = provider.GetRequiredService<IDeviceInfoService>();
            DeviceInfo deviceInfo = deviceInfoService.GetDeviceInfo();

            output.WriteLine();
            output.WriteLine("Device information:");
            output.WriteLine($"  Device family: {deviceInfo.DeviceFamily}");
            output.WriteLine($"  OS version: {deviceInfo.OperatingSystemVersion}");
            output.WriteLine($"  Architecture: {deviceInfo.Architecture}");
            output.WriteLine($"  Manufacturer: {deviceInfo.Manufacturer}");
            output.WriteLine($"  Model: {deviceInfo.Model}");

            IInputService inputService = provider.GetRequiredService<IInputService>();

            output.WriteLine();
            output.WriteLine("Input:");
            output.WriteLine($"  Connected gamepads: {inputService.ConnectedGamepadCount}");

            IGameCatalog catalog = provider.GetRequiredService<IGameCatalog>();
            IReadOnlyCollection<GameInfo> games = await catalog.GetGamesAsync().ConfigureAwait(false);
            List<GameInfo> orderedGames = games
                .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            output.WriteLine();
            output.WriteLine("Games:");
            foreach (GameInfo game in orderedGames)
            {
                if (game.Architecture != ProcessorArchitecture.Unknown)
                {
                    output.WriteLine($"  {game.Name} ({game.Architecture}) - {game.ExecutablePath}");
                }
                else
                {
                    output.WriteLine($"  {game.Name} - {game.ExecutablePath}");
                }
            }

            output.WriteLine($"Games found: {orderedGames.Count}");
            return ExitSuccess;
        }

        private static async Task<int> RunScanAsync(string? gamesRootPath, TextWriter output, TextWriter error)
        {
            if (!CheckExplicitGamesRoot(gamesRootPath, error))
            {
                return ExitFailure;
            }

            SettingsLoadResult load = await LoadSettingsAsync(error).ConfigureAwait(false);
            using ServiceProvider provider = CompositionRoot.Build(load.Settings, gamesRootPath);

            string rootPath = provider
                .GetRequiredService<IOptions<LocalGameCatalogOptions>>().Value.RootPath;

            output.WriteLine($"Games root: {rootPath}");

            IGameCatalog catalog = provider.GetRequiredService<IGameCatalog>();
            IReadOnlyCollection<GameInfo> games = await catalog.GetGamesAsync().ConfigureAwait(false);
            List<GameInfo> orderedGames = games
                .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            output.WriteLine("Games:");
            foreach (GameInfo game in orderedGames)
            {
                output.WriteLine(
                    $"  {game.Name} - Architecture: {game.Architecture}, " +
                    $"Type: {game.ExecutableType}, Path: {game.ExecutablePath}");
            }

            output.WriteLine($"Games found: {orderedGames.Count}");
            return ExitSuccess;
        }

        private static int RunInspect(string executablePath, TextWriter output, TextWriter error)
        {
            if (!File.Exists(executablePath))
            {
                error.WriteLine($"Error: File not found: {executablePath}");
                return ExitFailure;
            }

            PeInfo info;
            try
            {
                using (FileStream stream = File.OpenRead(executablePath))
                {
                    info = new PeFileInspector().Inspect(stream);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error.WriteLine($"Error: Could not read '{executablePath}': {exception.Message}");
                return ExitFailure;
            }

            output.WriteLine($"File: {Path.GetFullPath(executablePath)}");
            output.WriteLine($"Valid PE: {info.IsValid}");
            output.WriteLine($"Architecture: {info.Architecture}");
            output.WriteLine($"Machine: 0x{info.MachineRaw:X4}");
            output.WriteLine($"Format: {FormatName(info.IsPe32Plus)}");
            output.WriteLine($"DLL: {info.IsDll}");
            output.WriteLine($"Managed (.NET): {info.IsManaged}");
            output.WriteLine($"Subsystem: {info.Subsystem} (raw {info.SubsystemRaw})");
            output.WriteLine($"Sections: {info.NumberOfSections}");

            if (!info.IsValid)
            {
                error.WriteLine($"Error: Not a valid PE file: {info.Error}");
                return ExitFailure;
            }

            return ExitSuccess;
        }

        private static async Task<int> RunLaunchAsync(string idOrPath, TextWriter output, TextWriter error)
        {
            SettingsLoadResult load = await LoadSettingsAsync(error).ConfigureAwait(false);
            using ServiceProvider provider = CompositionRoot.Build(load.Settings, null);

            IGameCatalog catalog = provider.GetRequiredService<IGameCatalog>();

            GameInfo? game = await catalog.FindByIdAsync(idOrPath).ConfigureAwait(false);
            if (game == null)
            {
                game = await catalog.FindByExecutablePathAsync(idOrPath).ConfigureAwait(false);
            }

            if (game == null && LooksLikeExistingFile(idOrPath))
            {
                string fullPath = Path.GetFullPath(idOrPath);
                game = await catalog.FindByExecutablePathAsync(fullPath).ConfigureAwait(false);
                if (game == null)
                {
                    game = CreateAdHocGame(fullPath);
                }
            }

            if (game == null)
            {
                error.WriteLine($"Error: No game found with id or path '{idOrPath}'.");
                return ExitFailure;
            }

            output.WriteLine($"Launching: {game.Name}");
            output.WriteLine($"  Executable: {game.ExecutablePath}");
            output.WriteLine($"  Architecture: {game.Architecture}");
            output.WriteLine($"  Type: {game.ExecutableType}");

            IGameRunner runner = provider.GetRequiredService<IGameRunner>();
            LaunchResult result = await runner.LaunchAsync(game).ConfigureAwait(false);

            output.WriteLine($"Success: {result.Success}");
            output.WriteLine($"Reason: {result.Reason}");
            output.WriteLine($"Message: {result.Message ?? "(none)"}");
            if (result.ProcessId.HasValue)
            {
                output.WriteLine($"Process ID: {result.ProcessId.Value}");
            }

            return result.Success ? ExitSuccess : ExitFailure;
        }

        // Internal rather than private so the dispatch itself can be
        // pinned by tests: the parser guarantees only "show"/"reset"
        // ever reach this method (see CommandLine.ParseSettingsCommand),
        // so the unrecognised-subcommand branch below is unreachable
        // through RunAsync and could not be exercised end to end.
        internal static async Task<int> RunSettingsAsync(string subcommand, TextWriter output, TextWriter error)
        {
            if (string.Equals(subcommand, "reset", StringComparison.Ordinal))
            {
                // A storage failure here is the command's own failure:
                // it propagates to RunAsync, which reports it and
                // returns the failure exit code.
                await SettingsBootstrap.ResetAsync().ConfigureAwait(false);
                output.WriteLine("Settings reset to the defaults.");
                return ExitSuccess;
            }

            if (!string.Equals(subcommand, "show", StringComparison.Ordinal))
            {
                // The dispatch enforces the parser's invariant itself
                // instead of treating every non-"reset" value as
                // "show": if a subcommand is ever added at the parse
                // layer before it is handled here, it fails loudly
                // instead of silently running the show implementation.
                error.WriteLine($"Error: Unsupported settings subcommand '{subcommand}'.");
                return ExitFailure;
            }

            // "show": print the values the app actually runs with, read
            // back from the composed options after the loaded settings
            // were applied over the platform defaults.
            SettingsLoadResult load = await LoadSettingsAsync(error).ConfigureAwait(false);
            using ServiceProvider provider = CompositionRoot.Build(load.Settings, null);

            LocalGameCatalogOptions catalogOptions =
                provider.GetRequiredService<IOptions<LocalGameCatalogOptions>>().Value;
            FileLogServiceOptions logOptions =
                provider.GetRequiredService<IOptions<FileLogServiceOptions>>().Value;
            GamesRootResolution gamesRoot = SettingsBootstrap.ResolveGamesRoot(load.Settings, null);

            output.WriteLine("Effective settings:");
            if (load.StoredOutcome != null)
            {
                // The effective values alone cannot distinguish "no
                // settings file" from "the settings file was rejected
                // and the defaults substituted" — the exact confusion
                // this diagnostic command exists to resolve — so the
                // load outcome is reported alongside them. Reporting a
                // rejected file is still a successful show: the exit
                // code stays success, like the values themselves.
                output.WriteLine($"  Source: {DescribeStoredOutcome(load.StoredOutcome)}");
            }

            output.WriteLine($"  Games root: {catalogOptions.RootPath} (source: {DescribeGamesRootSource(gamesRoot.Source)})");
            output.WriteLine($"  Log minimum level: {logOptions.MinimumLevel}");
            output.WriteLine($"  Scan max depth: {catalogOptions.MaxDepth}");
            output.WriteLine($"  Executable patterns: {string.Join(", ", catalogOptions.ExecutablePatterns)}");
            return ExitSuccess;
        }

        /// <summary>
        /// Validates an explicitly supplied games root. The catalog
        /// treats a missing root as an empty library by design, which
        /// is right for the configured default — but for a path the
        /// user typed on the command line it would report a false
        /// success ("Games found: 0", exit 0) for what is almost
        /// certainly a typo, inconsistent with <c>inspect</c> failing
        /// for a missing file. An explicit root that does not exist is
        /// therefore an error; the no-argument path (default or
        /// settings-supplied root) keeps the empty-library behaviour.
        /// </summary>
        /// <returns>
        /// <c>true</c> when the command may proceed: no explicit root
        /// was supplied, or the supplied root exists.
        /// </returns>
        private static bool CheckExplicitGamesRoot(string? gamesRootPath, TextWriter error)
        {
            if (gamesRootPath == null || Directory.Exists(gamesRootPath))
            {
                return true;
            }

            error.WriteLine($"Error: Games root not found: {gamesRootPath}");
            return false;
        }

        /// <summary>
        /// Loads the persisted settings for a command, printing the
        /// one-line fallback warning to <paramref name="error"/> when
        /// the load failed and the defaults were substituted. Loading
        /// itself never throws for a settings or storage failure (see
        /// <see cref="SettingsBootstrap"/>), so a broken settings store
        /// can never stop a command from running.
        /// </summary>
        private static async Task<SettingsLoadResult> LoadSettingsAsync(TextWriter error)
        {
            SettingsLoadResult result = await SettingsBootstrap.LoadAsync().ConfigureAwait(false);
            if (result.LoadWarning != null)
            {
                error.WriteLine(result.LoadWarning);
            }

            return result;
        }

        private static string DescribeStoredOutcome(SettingsLoadOutcome outcome)
        {
            switch (outcome.State)
            {
                case StoredSettingsState.Loaded:
                    return "settings file";

                case StoredSettingsState.Absent:
                    return "defaults (no settings file found)";

                default:
                    return $"defaults (settings file could not be used: {DescribeRejectionReason(outcome.RejectionReason)})";
            }
        }

        private static string DescribeRejectionReason(SettingsRejectionReason reason)
        {
            switch (reason)
            {
                case SettingsRejectionReason.CorruptData:
                    return "not valid settings data";

                case SettingsRejectionReason.UnsupportedVersion:
                    return "unsupported format version";

                case SettingsRejectionReason.InvalidValues:
                    return "stored values failed validation";

                case SettingsRejectionReason.ReadFailure:
                    return "could not be read";

                default:
                    return "unknown reason";
            }
        }

        private static string DescribeGamesRootSource(GamesRootSource source)
        {
            switch (source)
            {
                case GamesRootSource.CommandLine:
                    return "command line";

                case GamesRootSource.Settings:
                    return "settings file";

                default:
                    return "platform default";
            }
        }

        private static GameInfo CreateAdHocGame(string fullPath)
        {
            ExecutableType executableType = ExecutableType.Unknown;
            ProcessorArchitecture architecture = ProcessorArchitecture.Unknown;

            try
            {
                using (FileStream stream = File.OpenRead(fullPath))
                {
                    PeInfo info = new PeFileInspector().Inspect(stream);
                    (ExecutableType classifiedType, ProcessorArchitecture classifiedArchitecture) =
                        ExecutableClassifier.Classify(info);
                    executableType = classifiedType;
                    architecture = classifiedArchitecture;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // Classification is best-effort: the runner re-checks the
                // file and reports its own structured failure.
            }

            string name = Path.GetFileNameWithoutExtension(fullPath);
            string workingDirectory = Path.GetDirectoryName(fullPath) ?? string.Empty;
            if (string.IsNullOrEmpty(workingDirectory))
            {
                workingDirectory = Directory.GetCurrentDirectory();
            }

            return new GameInfo(
                "adhoc-" + Path.GetFileName(fullPath),
                name,
                fullPath,
                workingDirectory,
                executableType,
                architecture: architecture);
        }

        private static bool LooksLikeExistingFile(string value)
        {
            return value.IndexOf('/') >= 0
                || value.IndexOf('\\') >= 0
                || File.Exists(value);
        }

        private static string FormatName(bool? isPe32Plus)
        {
            if (isPe32Plus.HasValue)
            {
                return isPe32Plus.Value ? "PE32+" : "PE32";
            }

            return "Unknown";
        }
    }
}
