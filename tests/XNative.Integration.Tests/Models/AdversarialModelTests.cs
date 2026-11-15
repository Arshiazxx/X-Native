using System;
using XNative.Core.Devices;
using XNative.Core.Games;
using XNative.Core.Processes;
using Xunit;

namespace XNative.Integration.Tests.Models
{
    /// <summary>
    /// Adversarial constructor tests for the core models. The rule under
    /// test: a constructor may throw ONLY the exceptions its documentation
    /// promises (ArgumentNullException for null, ArgumentException for
    /// empty/whitespace where documented) and must accept everything else,
    /// including very long and unicode values. Where a model accepts input
    /// its siblings reject, the test pins the current behaviour and the
    /// AUDIT.md flags the inconsistency — pinning is not endorsement.
    /// </summary>
    public sealed class AdversarialModelTests
    {
        private static string LongString => new string('a', 10_000);
        private const string Unicode = "ゲーム 🎮 ñoño — Ünïcodé";

        // ---------- GameInfo ----------

        [Fact]
        public void GameInfo_NullRequiredArguments_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GameInfo(null!, "Name", "e.exe", "wd", ExecutableType.WindowsExecutable));
            Assert.Throws<ArgumentNullException>(() => new GameInfo("id", null!, "e.exe", "wd", ExecutableType.WindowsExecutable));
            Assert.Throws<ArgumentNullException>(() => new GameInfo("id", "Name", null!, "wd", ExecutableType.WindowsExecutable));
            Assert.Throws<ArgumentNullException>(() => new GameInfo("id", "Name", "e.exe", null!, ExecutableType.WindowsExecutable));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n")]
        public void GameInfo_EmptyOrWhitespaceRequiredArguments_ThrowArgumentException(string bad)
        {
            Assert.Throws<ArgumentException>(() => new GameInfo(bad, "Name", "e.exe", "wd", ExecutableType.WindowsExecutable));
            Assert.Throws<ArgumentException>(() => new GameInfo("id", bad, "e.exe", "wd", ExecutableType.WindowsExecutable));
            Assert.Throws<ArgumentException>(() => new GameInfo("id", "Name", bad, "wd", ExecutableType.WindowsExecutable));
            Assert.Throws<ArgumentException>(() => new GameInfo("id", "Name", "e.exe", bad, ExecutableType.WindowsExecutable));
        }

        [Fact]
        public void GameInfo_VeryLongAndUnicodeValues_AreAccepted()
        {
            var info = new GameInfo(LongString, Unicode, LongString, Unicode, ExecutableType.WindowsExecutable);

            Assert.Equal(LongString, info.Id);
            Assert.Equal(Unicode, info.Name);
        }

        [Fact]
        public void GameInfo_OptionalArguments_AcceptNullAndEmpty()
        {
            var info = new GameInfo(
                "id", "Name", "e.exe", "wd", ExecutableType.Unknown,
                metadata: null,
                launchArguments: "");

            Assert.Null(info.Metadata);
            Assert.Equal("", info.LaunchArguments);
        }

        [Fact]
        public void GameInfo_UndefinedExecutableType_IsAccepted()
        {
            // Pins current behaviour: no enum validation is documented or
            // performed. GameViewModel maps unknown values to "unknown".
            var info = new GameInfo("id", "Name", "e.exe", "wd", (ExecutableType)999);

            Assert.Equal((ExecutableType)999, info.ExecutableType);
        }

        // ---------- DeviceInfo ----------

        [Fact]
        public void DeviceInfo_NullArguments_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DeviceInfo(null!, "v", "a", "m", "mo"));
            Assert.Throws<ArgumentNullException>(() => new DeviceInfo("f", null!, "a", "m", "mo"));
            Assert.Throws<ArgumentNullException>(() => new DeviceInfo("f", "v", null!, "m", "mo"));
            Assert.Throws<ArgumentNullException>(() => new DeviceInfo("f", "v", "a", null!, "mo"));
            Assert.Throws<ArgumentNullException>(() => new DeviceInfo("f", "v", "a", "m", null!));
        }

        [Fact]
        public void DeviceInfo_EmptyAndWhitespace_AreAccepted_PinsMissingValidation()
        {
            // Unlike GameInfo/GameProfile, DeviceInfo documents no
            // ArgumentException and performs no empty check. A host that
            // reports empty strings therefore produces a DeviceInfo full of
            // blanks instead of the documented "Unknown" fallback. Pinned
            // here; flagged in AUDIT.md as an inconsistency.
            var info = new DeviceInfo("", "   ", "", "", "");

            Assert.Equal("", info.DeviceFamily);
            Assert.False(info.IsXbox);
        }

        [Fact]
        public void DeviceInfo_VeryLongAndUnicodeValues_AreAccepted()
        {
            var info = new DeviceInfo(Unicode, LongString, Unicode, Unicode, Unicode);

            Assert.Equal(Unicode, info.DeviceFamily);
            Assert.Equal(LongString, info.OperatingSystemVersion);
        }

        [Fact]
        public void DeviceInfo_CreateUnknown_UsesUnknownValueEverywhere()
        {
            DeviceInfo info = DeviceInfo.CreateUnknown();

            Assert.Equal(DeviceInfo.UnknownValue, info.DeviceFamily);
            Assert.Equal(DeviceInfo.UnknownValue, info.OperatingSystemVersion);
            Assert.Equal(DeviceInfo.UnknownValue, info.Architecture);
            Assert.Equal(DeviceInfo.UnknownValue, info.Manufacturer);
            Assert.Equal(DeviceInfo.UnknownValue, info.Model);
            Assert.False(info.IsXbox);
        }

        [Fact]
        public void DeviceInfo_IsXbox_MatchesXboxFamilyCaseInsensitively()
        {
            Assert.True(new DeviceInfo("Windows.Xbox", "v", "a", "m", "mo").IsXbox);
            Assert.True(new DeviceInfo("windows.xbox", "v", "a", "m", "mo").IsXbox);
            Assert.False(new DeviceInfo("Windows.Desktop", "v", "a", "m", "mo").IsXbox);
        }

        // ---------- GameProfile ----------

        [Fact]
        public void GameProfile_NullRequiredArguments_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GameProfile(null!, "game.exe", CompatibilityRating.Playable));
            Assert.Throws<ArgumentNullException>(() => new GameProfile("id", null!, CompatibilityRating.Playable));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void GameProfile_EmptyOrWhitespaceRequiredArguments_ThrowArgumentException(string bad)
        {
            Assert.Throws<ArgumentException>(() => new GameProfile(bad, "game.exe", CompatibilityRating.Playable));
            Assert.Throws<ArgumentException>(() => new GameProfile("id", bad, CompatibilityRating.Playable));
        }

        [Fact]
        public void GameProfile_VeryLongUnicodeAndNullOptionals_AreAccepted()
        {
            var profile = new GameProfile(LongString, Unicode, CompatibilityRating.Perfect, notes: null, recommendedArguments: null);

            Assert.Equal(LongString, profile.Id);
            Assert.Equal(Unicode, profile.ExecutableName);
            Assert.Null(profile.Notes);
            Assert.Null(profile.RecommendedArguments);
        }

        // ---------- ProcessStartRequest / RunningProcess ----------

        [Fact]
        public void ProcessStartRequest_NullFileName_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ProcessStartRequest(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ProcessStartRequest_EmptyOrWhitespaceFileName_ThrowsArgumentException(string bad)
        {
            Assert.Throws<ArgumentException>(() => new ProcessStartRequest(bad));
        }

        [Fact]
        public void RunningProcess_NullFileName_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RunningProcess(1, null!, DateTime.UtcNow, false, null));
        }

        [Fact]
        public void RunningProcess_EmptyFileName_IsAccepted_PinsMissingValidation()
        {
            // ProcessStartRequest rejects an empty FileName; RunningProcess,
            // describing the same path after launch, accepts it. Pinned;
            // flagged in AUDIT.md.
            var process = new RunningProcess(1, "", DateTime.UtcNow, false, null);

            Assert.Equal("", process.FileName);
        }

        // ---------- LaunchResult invariants ----------

        [Fact]
        public void LaunchResult_Ok_HasReasonNone()
        {
            LaunchResult bare = LaunchResult.Ok();
            Assert.True(bare.Success);
            Assert.Equal(LaunchFailureReason.None, bare.Reason);
            Assert.Null(bare.Message);
            Assert.Null(bare.ProcessId);

            LaunchResult withPid = LaunchResult.Ok("started", 4242);
            Assert.True(withPid.Success);
            Assert.Equal(LaunchFailureReason.None, withPid.Reason);
            Assert.Equal(4242, withPid.ProcessId);
        }

        [Fact]
        public void LaunchResult_Fail_DefaultsReasonToUnknownAndProcessIdToNull()
        {
            LaunchResult result = LaunchResult.Fail("boom");

            Assert.False(result.Success);
            Assert.Equal(LaunchFailureReason.Unknown, result.Reason);
            Assert.Null(result.ProcessId);
            Assert.Equal("boom", result.Message);
        }

        [Fact]
        public void LaunchResult_Fail_KeepsExplicitReasonAndNullProcessId()
        {
            LaunchResult result = LaunchResult.Fail("no backend", LaunchFailureReason.BackendNotImplemented);

            Assert.False(result.Success);
            Assert.Equal(LaunchFailureReason.BackendNotImplemented, result.Reason);
            Assert.Null(result.ProcessId);
        }

        // ---------- LaunchOptions / GameMetadata ----------

        [Fact]
        public void LaunchOptions_NewInstance_HasEmptyNonNullEnvironment()
        {
            var options = new LaunchOptions();

            Assert.NotNull(options.EnvironmentVariables);
            Assert.Empty(options.EnvironmentVariables);
            Assert.Null(options.Arguments);
            Assert.Null(options.WorkingDirectory);
            Assert.Null(options.ProfileId);
        }

        [Fact]
        public void GameMetadata_AllNull_IsAccepted()
        {
            var metadata = new GameMetadata();

            Assert.Null(metadata.Description);
            Assert.Null(metadata.Publisher);
            Assert.Null(metadata.Version);
            Assert.Null(metadata.ReleaseDate);
            Assert.Null(metadata.IconPath);
        }
    }
}
