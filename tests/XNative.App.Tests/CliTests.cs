using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace XNative.App.Tests
{
    public sealed class CliTests : IDisposable
    {
        private readonly string _root;

        public CliTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(),
                "xnative-cli-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup of the temporary folder.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup of the temporary folder.
            }
        }

        // ---- Command line parsing -------------------------------------

        [Fact]
        public void Parse_NoArgs_IsInfoWithDefaultRoot()
        {
            ParsedCommand command = CommandLine.Parse(Array.Empty<string>());

            Assert.Equal(CommandKind.Info, command.Kind);
            Assert.Null(command.Argument);
        }

        [Fact]
        public void Parse_InfoWithPath_KeepsPath()
        {
            ParsedCommand command = CommandLine.Parse(new[] { "info", "some/root" });

            Assert.Equal(CommandKind.Info, command.Kind);
            Assert.Equal("some/root", command.Argument);
        }

        [Fact]
        public void Parse_ScanWithoutPath_UsesDefaultRoot()
        {
            ParsedCommand command = CommandLine.Parse(new[] { "scan" });

            Assert.Equal(CommandKind.Scan, command.Kind);
            Assert.Null(command.Argument);
        }

        [Fact]
        public void Parse_InspectWithoutPath_IsError()
        {
            ParsedCommand command = CommandLine.Parse(new[] { "inspect" });

            Assert.Equal(CommandKind.Error, command.Kind);
            Assert.False(string.IsNullOrWhiteSpace(command.ErrorMessage));
        }

        [Fact]
        public void Parse_LaunchWithoutArgument_IsError()
        {
            ParsedCommand command = CommandLine.Parse(new[] { "launch" });

            Assert.Equal(CommandKind.Error, command.Kind);
        }

        [Fact]
        public void Parse_UnknownCommand_IsError()
        {
            ParsedCommand command = CommandLine.Parse(new[] { "frobnicate" });

            Assert.Equal(CommandKind.Error, command.Kind);
            Assert.Contains("frobnicate", command.ErrorMessage);
        }

        [Fact]
        public void Parse_Help_IsHelp()
        {
            Assert.Equal(CommandKind.Help, CommandLine.Parse(new[] { "help" }).Kind);
            Assert.Equal(CommandKind.Help, CommandLine.Parse(new[] { "--help" }).Kind);
        }

        [Fact]
        public void Parse_LegacyPathArgument_IsInfoWithThatRoot()
        {
            string path = Path.Combine("some", "games");

            ParsedCommand command = CommandLine.Parse(new[] { path });

            Assert.Equal(CommandKind.Info, command.Kind);
            Assert.Equal(path, command.Argument);
        }

        [Fact]
        public void Parse_NullFirstArgument_IsError()
        {
            // Unreachable from a real command line, but Parse is a
            // public surface: a null first element must be a usage
            // error, not a NullReferenceException escaping the host's
            // exit-code contract.
            ParsedCommand command = CommandLine.Parse(new string[] { null! });

            Assert.Equal(CommandKind.Error, command.Kind);
            Assert.False(string.IsNullOrWhiteSpace(command.ErrorMessage));
        }

        // ---- Dispatch / exit codes ------------------------------------

        [Fact]
        public async Task RunAsync_UnknownCommand_ReturnsUsageExitCodeAndPrintsUsage()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "frobnicate" }, output, error);

            Assert.Equal(Cli.ExitUsage, exitCode);
            Assert.NotEqual(0, exitCode);
            Assert.Contains("Usage:", error.ToString());
            Assert.Contains("Unknown command", error.ToString());
        }

        [Fact]
        public async Task RunAsync_InspectWithoutPath_ReturnsUsageExitCode()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "inspect" }, output, error);

            Assert.Equal(Cli.ExitUsage, exitCode);
            Assert.Contains("Usage:", error.ToString());
        }

        [Fact]
        public async Task RunAsync_Help_ReturnsSuccessAndPrintsUsage()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "help" }, output, error);

            Assert.Equal(Cli.ExitSuccess, exitCode);
            Assert.Contains("Usage:", output.ToString());
            Assert.Contains("inspect", output.ToString());
            Assert.Contains("launch", output.ToString());
        }

        [Fact]
        public async Task RunAsync_NoArgs_RunsInfoAndReturnsSuccess()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(Array.Empty<string>(), output, error);

            Assert.Equal(Cli.ExitSuccess, exitCode);
            string text = output.ToString();
            Assert.Contains("X-Native modules:", text);
            Assert.Contains("Device information:", text);
            Assert.Contains("Connected gamepads:", text);
            Assert.Contains("Games found:", text);
        }

        [Fact]
        public async Task RunAsync_NullFirstArgument_ReturnsUsageExitCode()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new string[] { null! }, output, error);

            Assert.Equal(Cli.ExitUsage, exitCode);
            Assert.Contains("Usage:", error.ToString());
        }

        [Fact]
        public async Task RunAsync_ScanNonexistentRoot_ReturnsFailure()
        {
            // An explicitly supplied root that does not exist is an
            // error, not an empty library: the catalog's missing-root
            // behaviour would otherwise report a false success for a
            // typo'd path (inspect already fails for a missing file).
            string missing = Path.Combine(_root, "no-such-dir");
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "scan", missing }, output, error);

            Assert.Equal(Cli.ExitFailure, exitCode);
            Assert.Contains("not found", error.ToString());
            Assert.Contains(missing, error.ToString());
        }

        [Fact]
        public async Task RunAsync_InfoNonexistentRoot_ReturnsFailure()
        {
            string missing = Path.Combine(_root, "no-such-dir");
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "info", missing }, output, error);

            Assert.Equal(Cli.ExitFailure, exitCode);
            Assert.Contains("not found", error.ToString());
            Assert.Contains(missing, error.ToString());
        }

        [Fact]
        public async Task RunAsync_LegacyNonexistentPath_ReturnsFailure()
        {
            // The legacy single-path form is an explicit root too.
            string missing = Path.Combine(_root, "no-such-dir");
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { missing }, output, error);

            Assert.Equal(Cli.ExitFailure, exitCode);
            Assert.Contains("not found", error.ToString());
        }

        // ---- scan ------------------------------------------------------

        [Fact]
        public async Task RunAsync_ScanEmptyFolder_ReturnsSuccessWithZeroGames()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "scan", _root }, output, error);

            Assert.Equal(Cli.ExitSuccess, exitCode);
            Assert.Contains("Games found: 0", output.ToString());
        }

        [Fact]
        public async Task RunAsync_ScanListsGameWithArchitectureTypeAndPath()
        {
            string exePath = Path.Combine(_root, "game.exe");
            File.WriteAllBytes(exePath, Array.Empty<byte>());

            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "scan", _root }, output, error);

            Assert.Equal(Cli.ExitSuccess, exitCode);
            string text = output.ToString();
            Assert.Contains("game", text);
            Assert.Contains("Architecture:", text);
            Assert.Contains("WindowsExecutable", text);
            Assert.Contains(exePath, text);
            Assert.Contains("Games found: 1", text);
        }

        // ---- inspect ---------------------------------------------------

        [Fact]
        public async Task RunAsync_InspectMissingFile_ReturnsFailure()
        {
            string missing = Path.Combine(_root, "no-such-game.exe");
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "inspect", missing }, output, error);

            Assert.Equal(Cli.ExitFailure, exitCode);
            Assert.NotEqual(0, exitCode);
            Assert.Contains("File not found", error.ToString());
        }

        [Fact]
        public async Task RunAsync_InspectNonPeFile_ReturnsFailureAndReportsInvalid()
        {
            string file = Path.Combine(_root, "notes.exe");
            File.WriteAllText(file, "this is not a portable executable at all");
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "inspect", file }, output, error);

            Assert.Equal(Cli.ExitFailure, exitCode);
            Assert.Contains("Valid PE: False", output.ToString());
            Assert.Contains("Not a valid PE file", error.ToString());
        }

        [Fact]
        public async Task RunAsync_InspectRealAssembly_ReturnsSuccessWithFullInfo()
        {
            // The host's own assembly is a real managed PE image.
            string assemblyPath = typeof(Program).Assembly.Location;
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "inspect", assemblyPath }, output, error);

            Assert.Equal(Cli.ExitSuccess, exitCode);
            string text = output.ToString();
            Assert.Contains("Valid PE: True", text);
            Assert.Contains("Architecture:", text);
            Assert.Contains("DLL:", text);
            Assert.Contains("Managed (.NET): True", text);
            Assert.Contains("Subsystem:", text);
            Assert.Contains("Sections:", text);
        }

        // ---- launch ----------------------------------------------------

        [Fact]
        public async Task RunAsync_LaunchUnknownId_ReturnsFailure()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(
                new[] { "launch", "no-such-game-id-xyz" }, output, error);

            Assert.Equal(Cli.ExitFailure, exitCode);
            Assert.Contains("No game found", error.ToString());
        }

        [Fact]
        public async Task RunAsync_LaunchByPath_ReportsStructuredFailureHonestly()
        {
            // An empty .exe exists on disk, so launch resolves it as a
            // path — but no backend can run it, and the CLI must say so
            // through the structured result instead of claiming success.
            string exePath = Path.Combine(_root, "game.exe");
            File.WriteAllBytes(exePath, Array.Empty<byte>());
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = await Cli.RunAsync(new[] { "launch", exePath }, output, error);

            Assert.Equal(Cli.ExitFailure, exitCode);
            string text = output.ToString();
            Assert.Contains("Success: False", text);
            Assert.Contains("Reason:", text);
            Assert.Contains("Message:", text);
        }
    }
}
