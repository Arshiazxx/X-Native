using System;
using XNative.Core.Games;
using Xunit;

namespace XNative.Core.Tests.Games
{
    public sealed class GameProfileTests
    {
        [Fact]
        public void Constructor_ThrowsOnNullId()
        {
            Assert.Throws<ArgumentNullException>(() => new GameProfile(null!, "game.exe", CompatibilityRating.Playable));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_ThrowsOnEmptyOrWhitespaceId(string id)
        {
            // The profile stores rely on this: an unusable ID must never
            // reach a store, where it could never be looked up.
            Assert.Throws<ArgumentException>(() => new GameProfile(id, "game.exe", CompatibilityRating.Playable));
        }

        [Fact]
        public void Constructor_AcceptsValidId()
        {
            var profile = new GameProfile("my-game", "game.exe", CompatibilityRating.Playable);

            Assert.Equal("my-game", profile.Id);
        }
    }
}
