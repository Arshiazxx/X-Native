using System;
using System.IO;
using System.Threading.Tasks;
using XNative.Core.Settings;
using XNative.Platform.Settings;
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
    /// specific stored values, which are the developer's own. The
    /// broken-storage tests at the end narrow that rule deliberately:
    /// they plant or obstruct the settings file themselves (moving any
    /// pre-existing file aside and restoring it afterwards) so the
    /// exit-code contract for a failing store is pinned, not just the
    /// happy path.
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
            Assert.Contains("Source:", text);
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

        // ---- Broken storage: the exit-code contract -------------------

        [Fact]
        public async Task RunAsync_SettingsReset_WithUnwritableSettingsFile_ReturnsFailure()
        {
            string settingsPath = GetSettingsFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            string? backupPath = MoveSettingsFileAside(settingsPath);
            bool directoryCreated = false;
            try
            {
                // A directory where the settings file belongs breaks
                // the write: the storage service writes to a temp file
                // and swaps it into place, and the swap cannot replace
                // a directory. Unlike loading, resetting does not fall
                // back — a storage failure here is the command's own
                // failure, so it must surface as exit 1 with the error
                // on standard error, never as a false success.
                if (!Directory.Exists(settingsPath))
                {
                    Directory.CreateDirectory(settingsPath);
                    directoryCreated = true;
                }

                var output = new StringWriter();
                var error = new StringWriter();

                int exitCode = await Cli.RunAsync(new[] { "settings", "reset" }, output, error);

                Assert.Equal(Cli.ExitFailure, exitCode);
                Assert.Contains("Error: ", error.ToString());
                Assert.DoesNotContain("Settings reset to the defaults.", output.ToString());
            }
            finally
            {
                if (directoryCreated && Directory.Exists(settingsPath))
                {
                    Directory.Delete(settingsPath, recursive: false);
                }

                RestoreSettingsFile(settingsPath, backupPath);
            }
        }

        [Fact]
        public async Task RunAsync_SettingsReset_WithStoredSettings_RestoresDefaults()
        {
            string settingsPath = GetSettingsFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            string? backupPath = MoveSettingsFileAside(settingsPath);
            try
            {
                // Plant non-default settings (the stored JSON shape
                // the wiring tests pin) so the reset has something to
                // discard, and prove the plant is what `show` reports
                // before resetting — otherwise the post-reset defaults
                // assertions below could pass vacuously.
                File.WriteAllText(
                    settingsPath,
                    "{\"version\": 1, \"gamesRootPath\": \"planted-root\", "
                    + "\"logMinimumLevel\": \"Warning\", \"scanMaxDepth\": 9, "
                    + "\"executablePatterns\": [\"*.bat\"]}");

                var plantedOutput = new StringWriter();
                var plantedError = new StringWriter();

                int plantedExitCode = await Cli.RunAsync(
                    new[] { "settings", "show" }, plantedOutput, plantedError);

                Assert.Equal(Cli.ExitSuccess, plantedExitCode);
                Assert.Contains("Scan max depth: 9", plantedOutput.ToString());
                Assert.Contains("(source: settings file)", plantedOutput.ToString());

                var output = new StringWriter();
                var error = new StringWriter();

                int exitCode = await Cli.RunAsync(new[] { "settings", "reset" }, output, error);

                Assert.Equal(Cli.ExitSuccess, exitCode);
                Assert.Contains("Settings reset to the defaults.", output.ToString());

                // The reset must be real, not just reported: a fresh
                // `show` afterwards reports the defaults, with the
                // games root back to the platform default.
                var afterOutput = new StringWriter();
                var afterError = new StringWriter();

                int afterExitCode = await Cli.RunAsync(
                    new[] { "settings", "show" }, afterOutput, afterError);

                Assert.Equal(Cli.ExitSuccess, afterExitCode);
                string after = afterOutput.ToString();
                Assert.Contains($"Scan max depth: {AppSettings.DefaultScanMaxDepth}", after);
                Assert.Contains("Log minimum level: Information", after);
                Assert.Contains("Executable patterns: *.exe", after);
                Assert.Contains("(source: platform default)", after);
            }
            finally
            {
                RestoreSettingsFile(settingsPath, backupPath);
            }
        }

        [Fact]
        public async Task RunAsync_SettingsShow_WithUnreadableSettingsFile_ReturnsSuccessWithDefaults()
        {
            string settingsPath = GetSettingsFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            string? backupPath = MoveSettingsFileAside(settingsPath);
            try
            {
                // Plant valid non-default settings, then hold the file
                // open with no sharing so the storage read itself
                // throws. Loading must never throw: the settings
                // service degrades a failed read to the defaults (its
                // documented contract, so no fallback warning is
                // printed either) and `show` still succeeds.
                File.WriteAllText(
                    settingsPath,
                    "{\"version\": 1, \"gamesRootPath\": \"planted-root\", "
                    + "\"logMinimumLevel\": \"Warning\", \"scanMaxDepth\": 9, "
                    + "\"executablePatterns\": [\"*.bat\"]}");

                using (var heldOpen = new FileStream(
                    settingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var output = new StringWriter();
                    var error = new StringWriter();

                    int exitCode = await Cli.RunAsync(
                        new[] { "settings", "show" }, output, error);

                    Assert.Equal(Cli.ExitSuccess, exitCode);
                    string text = output.ToString();
                    Assert.Contains("Effective settings:", text);
                    Assert.Contains($"Scan max depth: {AppSettings.DefaultScanMaxDepth}", text);
                    Assert.Contains("(source: platform default)", text);
                    Assert.DoesNotContain("Error:", error.ToString());
                    Assert.DoesNotContain("Warning:", error.ToString());
                }
            }
            finally
            {
                RestoreSettingsFile(settingsPath, backupPath);
            }
        }

        // ---- Real-store helpers ----------------------------------------

        /// <summary>
        /// The settings file's path in the real store, computed the
        /// way <c>PlatformModule</c> computes it: storage lives under
        /// <c>LocalApplicationData/XNative/Storage</c>, one file per
        /// storage key.
        /// </summary>
        private static string GetSettingsFilePath()
        {
            string appDataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "XNative");
            return Path.Combine(appDataRoot, "Storage", JsonSettingsService.StorageKey);
        }

        /// <summary>
        /// Moves a pre-existing settings file out of the way so a test
        /// can plant or obstruct the path; returns the backup path, or
        /// <c>null</c> when there was no file to move.
        /// </summary>
        private static string? MoveSettingsFileAside(string settingsPath)
        {
            if (!File.Exists(settingsPath))
            {
                return null;
            }

            string backupPath = settingsPath + ".test-backup";
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            File.Move(settingsPath, backupPath);
            return backupPath;
        }

        /// <summary>
        /// Undoes <see cref="MoveSettingsFileAside"/>: removes whatever
        /// the test left at the settings path and puts the original
        /// file back.
        /// </summary>
        private static void RestoreSettingsFile(string settingsPath, string? backupPath)
        {
            if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }

            if (backupPath != null && File.Exists(backupPath))
            {
                File.Move(backupPath, settingsPath);
            }
        }
    }
}
