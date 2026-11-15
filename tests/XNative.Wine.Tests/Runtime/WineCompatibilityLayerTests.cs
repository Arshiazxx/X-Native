using System;
using System.Threading.Tasks;
using XNative.Core.Games;
using XNative.Wine.Profiles;
using XNative.Wine.Runtime;
using XNative.Wine.Tests.Fakes;
using Xunit;

namespace XNative.Wine.Tests.Runtime
{
    public sealed class WineCompatibilityLayerTests
    {
        private const string GamePath = @"C:\Games\Test\game.exe";

        private static GameInfo CreateGame(ExecutableType type = ExecutableType.WindowsExecutable)
        {
            return new GameInfo("test-id", "Test Game", GamePath, @"C:\Games\Test", type);
        }

        [Fact]
        public void Identity_IsWineResearch()
        {
            var layer = new WineCompatibilityLayer();
            Assert.Equal("wine", layer.Id);
            Assert.Equal("Wine (research)", layer.DisplayName);
        }

        [Fact]
        public void CanRun_NoStore_ReturnsUnknown()
        {
            var layer = new WineCompatibilityLayer();
            Assert.Equal(CompatibilityRating.Unknown, layer.CanRun(CreateGame()));
        }

        [Fact]
        public void CanRun_NoProfile_ReturnsUnknown()
        {
            var layer = new WineCompatibilityLayer(new InMemoryGameProfileStore());
            Assert.Equal(CompatibilityRating.Unknown, layer.CanRun(CreateGame()));
        }

        [Theory]
        [InlineData(CompatibilityRating.Broken)]
        [InlineData(CompatibilityRating.Starts)]
        [InlineData(CompatibilityRating.Playable)]
        [InlineData(CompatibilityRating.Perfect)]
        public void CanRun_ReturnsProfileRating(CompatibilityRating rating)
        {
            var store = new InMemoryGameProfileStore(new[] { new GameProfile("p", "GAME.EXE", rating) });
            var layer = new WineCompatibilityLayer(store);
            Assert.Equal(rating, layer.CanRun(CreateGame()));
        }

        [Fact]
        public void CanRun_ThrowsOnNullGame()
        {
            var layer = new WineCompatibilityLayer();
            Assert.Throws<ArgumentNullException>(() => layer.CanRun(null!));
        }

        [Fact]
        public async Task RunAsync_SharesPipeline_BrokenProfileIsKnownBroken()
        {
            var profile = new GameProfile("p", "game.exe", CompatibilityRating.Broken, "Does not start.");
            var layer = new WineCompatibilityLayer(
                new FakeFileSystemService(GamePath),
                new InMemoryGameProfileStore(new[] { profile }),
                new FakeLogService());

            LaunchResult result = await layer.RunAsync(CreateGame(), LaunchOptions.Default);

            Assert.False(result.Success);
            Assert.Equal(LaunchFailureReason.KnownBroken, result.Reason);
            Assert.Contains("Does not start.", result.Message!);
        }

        [Fact]
        public async Task RunAsync_SharesPipeline_MissingFileIsExecutableNotFound()
        {
            var layer = new WineCompatibilityLayer(
                new FakeFileSystemService(),
                new InMemoryGameProfileStore(),
                new FakeLogService());

            LaunchResult result = await layer.RunAsync(CreateGame(), LaunchOptions.Default);

            Assert.Equal(LaunchFailureReason.ExecutableNotFound, result.Reason);
        }

        [Fact]
        public async Task RunAsync_Parameterless_KeepsLegacyMessage()
        {
            var layer = new WineCompatibilityLayer();

            LaunchResult result = await layer.RunAsync(CreateGame(), LaunchOptions.Default);

            Assert.Equal(LaunchFailureReason.BackendNotImplemented, result.Reason);
            Assert.Equal("Windows executable detected. Compatibility backend not implemented.", result.Message);
        }
    }
}
