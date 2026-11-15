using System;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Games;
using XNative.Wine.Profiles;
using XNative.Wine.Runtime;
using XNative.Wine.Tests.Fakes;
using Xunit;

namespace XNative.Wine.Tests.Runtime
{
    public sealed class CompatibilityGameRunnerPipelineTests
    {
        private const string GamePath = @"C:\Games\Test\game.exe";
        private const string GameDirectory = @"C:\Games\Test";

        private static GameInfo CreateGame(
            ExecutableType type = ExecutableType.WindowsExecutable,
            string? launchArguments = null,
            string path = GamePath)
        {
            return new GameInfo("test-id", "Test Game", path, GameDirectory, type, null, XNative.Core.Devices.ProcessorArchitecture.Unknown, launchArguments);
        }

        private static CompatibilityGameRunner CreateRunner(
            FakeFileSystemService fileSystem,
            FakeLogService log,
            params GameProfile[] profiles)
        {
            return new CompatibilityGameRunner(fileSystem, new InMemoryGameProfileStore(profiles), log);
        }

        [Fact]
        public void DependencyConstructor_ThrowsOnNullDependencies()
        {
            var fileSystem = new FakeFileSystemService(GamePath);
            var store = new InMemoryGameProfileStore();
            var log = new FakeLogService();

            Assert.Throws<ArgumentNullException>(() => new CompatibilityGameRunner(null!, store, log));
            Assert.Throws<ArgumentNullException>(() => new CompatibilityGameRunner(fileSystem, null!, log));
            Assert.Throws<ArgumentNullException>(() => new CompatibilityGameRunner(fileSystem, store, null!));
        }

        [Fact]
        public async Task LaunchAsync_ThrowsOnNullGame_WithDependencies()
        {
            var runner = CreateRunner(new FakeFileSystemService(GamePath), new FakeLogService());
            await Assert.ThrowsAsync<ArgumentNullException>(() => runner.LaunchAsync(null!));
        }

        [Fact]
        public async Task LaunchAsync_RespectsCancellation_WithDependencies()
        {
            var runner = CreateRunner(new FakeFileSystemService(GamePath), new FakeLogService());
            var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => runner.LaunchAsync(CreateGame(), cts.Token));
        }

        [Fact]
        public async Task LaunchAsync_UnsupportedType_ReturnsUnsupportedExecutableType()
        {
            var log = new FakeLogService();
            var runner = CreateRunner(new FakeFileSystemService(GamePath), log);

            LaunchResult result = await runner.LaunchAsync(CreateGame(ExecutableType.UwpApplication));

            Assert.False(result.Success);
            Assert.Equal(LaunchFailureReason.UnsupportedExecutableType, result.Reason);
            Assert.Equal("Executable type 'UwpApplication' is not supported.", result.Message);
            Assert.True(log.Contains("UwpApplication"));
        }

        [Fact]
        public async Task LaunchAsync_UnsupportedType_TakesPriorityOverMissingFile()
        {
            var runner = CreateRunner(new FakeFileSystemService(), new FakeLogService());

            LaunchResult result = await runner.LaunchAsync(CreateGame(ExecutableType.Unknown));

            Assert.Equal(LaunchFailureReason.UnsupportedExecutableType, result.Reason);
        }

        [Fact]
        public async Task LaunchAsync_MissingExecutable_ReturnsExecutableNotFound()
        {
            var log = new FakeLogService();
            var runner = CreateRunner(new FakeFileSystemService(), log);

            LaunchResult result = await runner.LaunchAsync(CreateGame());

            Assert.False(result.Success);
            Assert.Equal(LaunchFailureReason.ExecutableNotFound, result.Reason);
            Assert.Contains(GamePath, result.Message!);
            Assert.True(log.Contains("not found"));
        }

        [Fact]
        public async Task LaunchAsync_BrokenProfile_ReturnsKnownBrokenWithNotes()
        {
            var log = new FakeLogService();
            var profile = new GameProfile("p-broken", "game.exe", CompatibilityRating.Broken, "Crashes on the title screen.");
            var runner = CreateRunner(new FakeFileSystemService(GamePath), log, profile);

            LaunchResult result = await runner.LaunchAsync(CreateGame());

            Assert.False(result.Success);
            Assert.Equal(LaunchFailureReason.KnownBroken, result.Reason);
            Assert.Contains("Crashes on the title screen.", result.Message!);
            Assert.True(log.Contains("Broken"));
        }

        [Fact]
        public async Task LaunchAsync_BrokenProfileWithoutNotes_StillReturnsKnownBroken()
        {
            var profile = new GameProfile("p-broken", "game.exe", CompatibilityRating.Broken);
            var runner = CreateRunner(new FakeFileSystemService(GamePath), new FakeLogService(), profile);

            LaunchResult result = await runner.LaunchAsync(CreateGame());

            Assert.Equal(LaunchFailureReason.KnownBroken, result.Reason);
        }

        [Fact]
        public async Task LaunchAsync_ExistingExecutableNoProfile_ReturnsBackendNotImplementedWithLegacyMessage()
        {
            var log = new FakeLogService();
            var runner = CreateRunner(new FakeFileSystemService(GamePath), log);

            LaunchResult result = await runner.LaunchAsync(CreateGame());

            Assert.False(result.Success);
            Assert.Equal(LaunchFailureReason.BackendNotImplemented, result.Reason);
            Assert.Equal("Windows executable detected. Compatibility backend not implemented.", result.Message);
            Assert.True(log.Entries.Count > 0);
        }

        [Fact]
        public async Task LaunchAsync_KnownProfile_NamesRatingInBackendMessageAndLogsIt()
        {
            var log = new FakeLogService();
            var profile = new GameProfile("p-playable", "game.exe", CompatibilityRating.Playable, "Works well.");
            var runner = CreateRunner(new FakeFileSystemService(GamePath), log, profile);

            LaunchResult result = await runner.LaunchAsync(CreateGame());

            Assert.Equal(LaunchFailureReason.BackendNotImplemented, result.Reason);
            Assert.Contains("Playable", result.Message!);
            Assert.True(log.Contains("Playable"));
        }

        [Fact]
        public async Task LaunchAsync_OptionsProfileId_SelectsProfileById()
        {
            var log = new FakeLogService();
            var byName = new GameProfile("p-by-name", "game.exe", CompatibilityRating.Playable);
            var byId = new GameProfile("p-special", "other.exe", CompatibilityRating.Perfect);
            var runner = CreateRunner(new FakeFileSystemService(GamePath), log, byName, byId);

            LaunchResult result = await runner.LaunchAsync(CreateGame(), new LaunchOptions { ProfileId = "p-special" });

            Assert.Equal(LaunchFailureReason.BackendNotImplemented, result.Reason);
            Assert.Contains("Perfect", result.Message!);
        }

        [Fact]
        public async Task LaunchAsync_OptionsProfileId_BrokenSelectedProfile_ReturnsKnownBroken()
        {
            var byId = new GameProfile("p-special", "other.exe", CompatibilityRating.Broken, "Selected profile is broken.");
            var runner = CreateRunner(new FakeFileSystemService(GamePath), new FakeLogService(), byId);

            LaunchResult result = await runner.LaunchAsync(CreateGame(), new LaunchOptions { ProfileId = "P-SPECIAL" });

            Assert.Equal(LaunchFailureReason.KnownBroken, result.Reason);
            Assert.Contains("Selected profile is broken.", result.Message!);
        }

        [Fact]
        public async Task LaunchAsync_UnknownProfileId_FallsThroughAndLogsWarning()
        {
            var log = new FakeLogService();
            var runner = CreateRunner(new FakeFileSystemService(GamePath), log);

            LaunchResult result = await runner.LaunchAsync(CreateGame(), new LaunchOptions { ProfileId = "no-such-profile" });

            Assert.Equal(LaunchFailureReason.BackendNotImplemented, result.Reason);
            Assert.True(log.Contains("no-such-profile"));
        }

        [Fact]
        public async Task LaunchAsync_ArgumentsPreference_OptionsWinOverGameAndProfile()
        {
            var log = new FakeLogService();
            var profile = new GameProfile("p", "game.exe", CompatibilityRating.Playable, null, "--profile-arg");
            var runner = CreateRunner(new FakeFileSystemService(GamePath), log, profile);
            GameInfo game = CreateGame(launchArguments: "--game-arg");

            await runner.LaunchAsync(game, new LaunchOptions { Arguments = "--option-arg" });

            Assert.True(log.Contains("--option-arg"));
            Assert.False(log.Contains("--game-arg"));
        }

        [Fact]
        public async Task LaunchAsync_ArgumentsPreference_GameWinsOverProfile()
        {
            var log = new FakeLogService();
            var profile = new GameProfile("p", "game.exe", CompatibilityRating.Playable, null, "--profile-arg");
            var runner = CreateRunner(new FakeFileSystemService(GamePath), log, profile);

            await runner.LaunchAsync(CreateGame(launchArguments: "--game-arg"), LaunchOptions.Default);

            Assert.True(log.Contains("--game-arg"));
            Assert.False(log.Contains("--profile-arg"));
        }

        [Fact]
        public async Task LaunchAsync_ArgumentsPreference_ProfileUsedWhenNothingElse()
        {
            var log = new FakeLogService();
            var profile = new GameProfile("p", "game.exe", CompatibilityRating.Playable, null, "--profile-arg");
            var runner = CreateRunner(new FakeFileSystemService(GamePath), log, profile);

            await runner.LaunchAsync(CreateGame(), LaunchOptions.Default);

            Assert.True(log.Contains("--profile-arg"));
        }

        [Fact]
        public async Task LaunchAsync_EveryFailureOutcomeIsLogged()
        {
            var log = new FakeLogService();
            var runner = CreateRunner(new FakeFileSystemService(), log);

            await runner.LaunchAsync(CreateGame(ExecutableType.UwpApplication));
            int afterUnsupported = log.Entries.Count;
            Assert.True(afterUnsupported > 0);

            await runner.LaunchAsync(CreateGame());
            Assert.True(log.Entries.Count > afterUnsupported);
        }

        [Fact]
        public async Task LaunchAsync_WorkingDirectorySupplied_IsLoggedAsNotApplied()
        {
            var log = new FakeLogService();
            var runner = CreateRunner(new FakeFileSystemService(GamePath), log);

            LaunchResult result = await runner.LaunchAsync(
                CreateGame(), new LaunchOptions { WorkingDirectory = @"C:\Elsewhere" });

            // No behaviour change: the launch still ends in the same
            // structured failure, but the ignored option is on record.
            Assert.Equal(LaunchFailureReason.BackendNotImplemented, result.Reason);
            Assert.True(log.Contains("not applied"));
            Assert.True(log.Contains(@"C:\Elsewhere"));
        }

        [Fact]
        public async Task LaunchAsync_EnvironmentVariablesSupplied_AreLoggedAsNotApplied()
        {
            var log = new FakeLogService();
            var runner = CreateRunner(new FakeFileSystemService(GamePath), log);
            var options = new LaunchOptions();
            options.EnvironmentVariables["XNATIVE_FOO"] = "bar";

            LaunchResult result = await runner.LaunchAsync(CreateGame(), options);

            Assert.Equal(LaunchFailureReason.BackendNotImplemented, result.Reason);
            Assert.True(log.Contains("EnvironmentVariables"));
            Assert.True(log.Contains("not applied"));
        }

        [Fact]
        public async Task LaunchAsync_NoUnappliedOptions_LogsNoNotAppliedWarning()
        {
            var log = new FakeLogService();
            var runner = CreateRunner(new FakeFileSystemService(GamePath), log);

            await runner.LaunchAsync(CreateGame(), LaunchOptions.Default);

            Assert.False(log.Contains("not applied"));
        }

        [Fact]
        public async Task ParameterlessRunner_ReportsStructuredReasons()
        {
            var runner = new CompatibilityGameRunner();

            LaunchResult windows = await runner.LaunchAsync(CreateGame());
            Assert.Equal(LaunchFailureReason.BackendNotImplemented, windows.Reason);
            Assert.Equal("Windows executable detected. Compatibility backend not implemented.", windows.Message);

            LaunchResult other = await runner.LaunchAsync(CreateGame(ExecutableType.UwpApplication));
            Assert.Equal(LaunchFailureReason.UnsupportedExecutableType, other.Reason);
            Assert.Equal("Executable type 'UwpApplication' is not supported.", other.Message);
        }

        [Fact]
        public async Task ParameterlessRunner_OptionsOverload_KeepsLegacyResults()
        {
            var runner = new CompatibilityGameRunner();

            LaunchResult result = await runner.LaunchAsync(CreateGame(), new LaunchOptions { ProfileId = "anything", Arguments = "--x" });

            Assert.Equal(LaunchFailureReason.BackendNotImplemented, result.Reason);
            Assert.Equal("Windows executable detected. Compatibility backend not implemented.", result.Message);
        }
    }
}
