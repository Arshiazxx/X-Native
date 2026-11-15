using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace XNative.App.Tests
{
    /// <summary>
    /// The <c>settings</c> command of the console head: parsing of the
    /// <c>show</c>/<c>reset</c> subcommands and the end-to-end behaviour
    /// of both through <see cref="Cli.RunAsync"/>.
    /// </summary>
    /// <remarks>
    /// The end-to-end tests run against the real application data
    /// folder, like the rest of this suite already does for the other
    /// commands: <c>settings reset</c> writes the default settings file
    /// there, and <c>settings show</c> reads whatever is stored. The
    /// assertions therefore stick to the output shape and never to
    /// specific stored values, which are the developer's own.
    /// </remarks>
    public sealed class SettingsCliTests
    {
        // ---- Parsing ---------------------------------------------------

        [Fact]
        public void Parse_SettingsShow_IsSettingsWithShowSubcommand()
        {
            ParsedCommand command = CommandLine.Parse(new[] { "settings", "show" });

            Assert.Equal(CommandKind.Settings, command.Kind);
            Assert.Equal("show", command.Argument);
        }

        [Fact]
        public void Parse_SettingsReset_IsSettingsWithResetSubcommand()
        {
            ParsedCommand command = CommandLine.Parse(new[] { "settings", "reset" });

            Assert.Equal(CommandKind.Settings, command.Kind);
            Assert.Equal("reset", command.Argument);
        }

        [Fact]
        public void Parse_SettingsSubcommand_IsCaseInsensitiveAndNormalised()
        {
            ParsedCommand command = CommandLine.Parse(new[] { "SETTINGS", "SHOW" });

            Assert.Equal(CommandKind.Settings, command.Kind);
            Assert.Equal("show", command.Argument);
        }

        [Fact]
        public void Parse_SettingsWithoutSubcommand_IsError()
        {
            ParsedCommand command = CommandLine.Parse(new[] { "settings" });

            Assert.Equal(CommandKind.Error, command.Kind);
            Assert.Contains("show", command.ErrorMessage);
            Assert.Contains("reset", command.ErrorMessage);
        }

        [Fact]
        public void Parse_SettingsUnknownSubcommand_IsError()
        {
            ParsedCommand command = CommandLine.Parse(new[] { "settings", "frobnicate" });

            Assert.Equal(CommandKind.Error, command.Kind);
            Assert.Contains("frobnicate", command.ErrorMessage);
        }

        [Fact]
        public void Parse_SettingsWithExtraArgument_IsError()
        {
            ParsedCommand command = CommandLine.Parse(new[] { "settings", "show", "extra" });

            Assert.Equal(CommandKind.Error, command.Kind);
        }

        // ---- Dispatch / exit codes --------------------------------------

        [Fact]
        public async Task RunAsync_SettingsWithoutSubcommand_ReturnsUsageExitCode()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "settings" }, output, error);

            Assert.Equal(Cli.ExitUsage, exitCode);
            Assert.Contains("Usage:", error.ToString());
        }

        [Fact]
        public async Task RunAsync_SettingsShow_ReturnsSuccessAndPrintsEffectiveSettings()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "settings", "show" }, output, error);

            Assert.Equal(Cli.ExitSuccess, exitCode);
            string text = output.ToString();
            Assert.Contains("Effective settings:", text);
            Assert.Contains("Games root:", text);
            Assert.Contains("(source:", text);
            Assert.Contains("Log minimum level:", text);
            Assert.Contains("Scan max depth:", text);
            Assert.Contains("Executable patterns:", text);
        }

        [Fact]
        public async Task RunAsync_SettingsReset_ReturnsSuccessAndConfirms()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "settings", "reset" }, output, error);

            Assert.Equal(Cli.ExitSuccess, exitCode);
            Assert.Contains("Settings reset to the defaults.", output.ToString());
        }

        [Fact]
        public async Task RunAsync_Help_ListsSettingsCommand()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "help" }, output, error);

            Assert.Equal(Cli.ExitSuccess, exitCode);
            Assert.Contains("settings show", output.ToString());
            Assert.Contains("settings reset", output.ToString());
        }
    }
}
