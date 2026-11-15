using System;
using System.IO;

namespace XNative.App
{
    /// <summary>
    /// The command a parsed command line asks the host to run.
    /// </summary>
    public enum CommandKind
    {
        /// <summary>Print modules, device information and games.</summary>
        Info = 0,

        /// <summary>List the games a catalog scan finds.</summary>
        Scan = 1,

        /// <summary>Inspect one executable's PE headers.</summary>
        Inspect = 2,

        /// <summary>Launch one game through the game runner.</summary>
        Launch = 3,

        /// <summary>Print the usage text; nothing else runs.</summary>
        Help = 4,

        /// <summary>The command line is invalid; <see cref="ParsedCommand.ErrorMessage"/> says why.</summary>
        Error = 5,

        /// <summary>Show or reset the persisted settings; <see cref="ParsedCommand.Argument"/> is the subcommand (<c>show</c> or <c>reset</c>).</summary>
        Settings = 6,
    }

    /// <summary>
    /// The result of parsing the host's command line arguments.
    /// </summary>
    public sealed class ParsedCommand
    {
        internal ParsedCommand(CommandKind kind, string? argument, string? errorMessage)
        {
            Kind = kind;
            Argument = argument;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Gets the command to run.
        /// </summary>
        public CommandKind Kind { get; }

        /// <summary>
        /// Gets the command's single argument: the games root path for
        /// <see cref="CommandKind.Info"/> and <see cref="CommandKind.Scan"/>
        /// (or <c>null</c> for the configured default root), the executable
        /// path for <see cref="CommandKind.Inspect"/>, the game id or
        /// executable path for <see cref="CommandKind.Launch"/>, and the
        /// subcommand (<c>show</c> or <c>reset</c>, normalised to lower
        /// case) for <see cref="CommandKind.Settings"/>.
        /// </summary>
        public string? Argument { get; }

        /// <summary>
        /// Gets why parsing failed when <see cref="Kind"/> is
        /// <see cref="CommandKind.Error"/>; otherwise <c>null</c>.
        /// </summary>
        public string? ErrorMessage { get; }
    }

    /// <summary>
    /// Hand-rolled parser for the development host's command line. The
    /// grammar is deliberately tiny: an optional command word followed by
    /// at most one argument. For backward compatibility a single first
    /// argument that looks like a path (it contains a directory separator
    /// or names an existing file or directory) is treated as the games
    /// root path of the <see cref="CommandKind.Info"/> command, which is
    /// what the host's original single-argument form meant.
    /// </summary>
    public static class CommandLine
    {
        /// <summary>
        /// Parses the process arguments.
        /// </summary>
        /// <param name="args">The raw arguments, without the program name.</param>
        /// <returns>The parsed command; never <c>null</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="args"/> is <c>null</c>.</exception>
        public static ParsedCommand Parse(string[] args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (args.Length == 0)
            {
                return new ParsedCommand(CommandKind.Info, null, null);
            }

            string first = args[0];

            if (first == null)
            {
                // Unreachable from a real command line (argv elements
                // are never null), but Parse is a public surface:
                // report it as a usage error instead of dereferencing
                // the null and crashing the host outside its exit-code
                // contract.
                return Error("The first argument must not be null.");
            }

            if (Is(first, "help") || Is(first, "--help") || Is(first, "-h"))
            {
                return new ParsedCommand(CommandKind.Help, null, null);
            }

            if (Is(first, "info"))
            {
                return ParseOptionalPathCommand(CommandKind.Info, "info", args);
            }

            if (Is(first, "scan"))
            {
                return ParseOptionalPathCommand(CommandKind.Scan, "scan", args);
            }

            if (Is(first, "inspect"))
            {
                return ParseRequiredArgumentCommand(
                    CommandKind.Inspect,
                    "inspect",
                    "an executable path",
                    args);
            }

            if (Is(first, "launch"))
            {
                return ParseRequiredArgumentCommand(
                    CommandKind.Launch,
                    "launch",
                    "a game id or executable path",
                    args);
            }

            if (Is(first, "settings"))
            {
                return ParseSettingsCommand(args);
            }

            if (first.StartsWith("-", StringComparison.Ordinal))
            {
                return Error($"Unknown option '{first}'.");
            }

            if (LooksLikePath(first))
            {
                // Legacy form: the original host took the games root path
                // as its only argument. Extra arguments were ignored then
                // and stay ignored so existing scripts keep working.
                return new ParsedCommand(CommandKind.Info, first, null);
            }

            return Error($"Unknown command '{first}'.");
        }

        private static ParsedCommand ParseSettingsCommand(string[] args)
        {
            if (args.Length == 1)
            {
                return Error("The 'settings' command requires a subcommand: 'show' or 'reset'.");
            }

            if (args.Length > 2)
            {
                return Error("The 'settings' command takes exactly one subcommand argument ('show' or 'reset').");
            }

            if (Is(args[1], "show"))
            {
                return new ParsedCommand(CommandKind.Settings, "show", null);
            }

            if (Is(args[1], "reset"))
            {
                return new ParsedCommand(CommandKind.Settings, "reset", null);
            }

            return Error($"Unknown settings subcommand '{args[1]}'. Use 'show' or 'reset'.");
        }

        private static ParsedCommand ParseOptionalPathCommand(CommandKind kind, string name, string[] args)
        {
            if (args.Length == 1)
            {
                return new ParsedCommand(kind, null, null);
            }

            if (args.Length == 2)
            {
                return new ParsedCommand(kind, args[1], null);
            }

            return Error($"The '{name}' command takes at most one path argument.");
        }

        private static ParsedCommand ParseRequiredArgumentCommand(
            CommandKind kind,
            string name,
            string argumentDescription,
            string[] args)
        {
            if (args.Length == 2)
            {
                return new ParsedCommand(kind, args[1], null);
            }

            if (args.Length < 2)
            {
                return Error($"The '{name}' command requires {argumentDescription}.");
            }

            return Error($"The '{name}' command takes exactly one argument ({argumentDescription}).");
        }

        private static bool LooksLikePath(string value)
        {
            return value.IndexOf('/') >= 0
                || value.IndexOf('\\') >= 0
                || Directory.Exists(value)
                || File.Exists(value);
        }

        private static bool Is(string value, string command)
        {
            return string.Equals(value, command, StringComparison.OrdinalIgnoreCase);
        }

        private static ParsedCommand Error(string message)
        {
            return new ParsedCommand(CommandKind.Error, null, message);
        }
    }
}
