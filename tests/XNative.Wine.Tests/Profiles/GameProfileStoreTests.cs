using System;
using System.IO;
using System.Linq;
using XNative.Core.Games;
using XNative.Wine.Profiles;
using Xunit;

namespace XNative.Wine.Tests.Profiles
{
    public sealed class GameProfileStoreTests
    {
        private static GameInfo CreateGame(string path = @"C:\Games\Test\game.exe")
        {
            return new GameInfo("test-id", "Test Game", path, @"C:\Games\Test", ExecutableType.WindowsExecutable);
        }

        [Fact]
        public void InMemory_FindForGame_ThrowsOnNull()
        {
            var store = new InMemoryGameProfileStore();
            Assert.Throws<ArgumentNullException>(() => store.FindForGame(null!));
        }

        [Fact]
        public void InMemory_Constructor_ThrowsOnNullCollection()
        {
            Assert.Throws<ArgumentNullException>(() => new InMemoryGameProfileStore(null!));
        }

        [Fact]
        public void InMemory_FindForGame_MatchesExecutableNameCaseInsensitively()
        {
            var profile = new GameProfile("p", "GaMe.ExE", CompatibilityRating.Playable);
            var store = new InMemoryGameProfileStore(new[] { profile });

            Assert.Same(profile, store.FindForGame(CreateGame()));
        }

        [Fact]
        public void InMemory_FindForGame_IgnoresDirectoryAndForwardSlashes()
        {
            var profile = new GameProfile("p", "game.exe", CompatibilityRating.Starts);
            var store = new InMemoryGameProfileStore(new[] { profile });

            Assert.Same(profile, store.FindForGame(CreateGame("/games/test/GAME.EXE")));
        }

        [Fact]
        public void InMemory_FindForGame_ReturnsNullWhenNoMatch()
        {
            var store = new InMemoryGameProfileStore(new[] { new GameProfile("p", "other.exe", CompatibilityRating.Perfect) });
            Assert.Null(store.FindForGame(CreateGame()));
        }

        [Fact]
        public void InMemory_GetProfiles_ReturnsAllProfiles()
        {
            var store = new InMemoryGameProfileStore(new[]
            {
                new GameProfile("a", "a.exe", CompatibilityRating.Playable),
                new GameProfile("b", "b.exe", CompatibilityRating.Broken),
            });

            Assert.Equal(2, store.GetProfiles().Count);
        }

        [Fact]
        public void Json_RoundTrip_PreservesAllFields()
        {
            const string json = @"[
              { ""id"": ""halo"", ""executableName"": ""halo.exe"", ""rating"": ""Playable"", ""notes"": ""Needs a tweak."", ""recommendedArguments"": ""-windowed"" },
              { ""id"": ""doom"", ""executableName"": ""doom.exe"", ""rating"": ""broken"" }
            ]";

            var store = new JsonGameProfileStore(json);

            Assert.Equal(2, store.GetProfiles().Count);
            GameProfile halo = store.GetProfiles().Single(p => p.Id == "halo");
            Assert.Equal("halo.exe", halo.ExecutableName);
            Assert.Equal(CompatibilityRating.Playable, halo.Rating);
            Assert.Equal("Needs a tweak.", halo.Notes);
            Assert.Equal("-windowed", halo.RecommendedArguments);

            GameProfile doom = store.GetProfiles().Single(p => p.Id == "doom");
            Assert.Equal(CompatibilityRating.Broken, doom.Rating);
            Assert.Null(doom.Notes);
            Assert.Null(doom.RecommendedArguments);
        }

        [Fact]
        public void Json_FindForGame_MatchesLoadedProfiles()
        {
            var store = JsonGameProfileStore.FromJson(@"[{ ""id"": ""p"", ""executableName"": ""game.exe"", ""rating"": ""Perfect"" }]");
            GameProfile? profile = store.FindForGame(CreateGame());
            Assert.NotNull(profile);
            Assert.Equal(CompatibilityRating.Perfect, profile!.Rating);
        }

        [Fact]
        public void Json_FromReader_LoadsProfiles()
        {
            using (var reader = new StringReader(@"[{ ""id"": ""p"", ""executableName"": ""game.exe"", ""rating"": ""Starts"" }]"))
            {
                var store = JsonGameProfileStore.FromReader(reader);
                Assert.Single(store.GetProfiles());
            }
        }

        [Fact]
        public void Json_EmptyArray_IsEmptyStore()
        {
            var store = new JsonGameProfileStore("[]");
            Assert.Empty(store.GetProfiles());
            Assert.Null(store.FindForGame(CreateGame()));
        }

        [Theory]
        [InlineData("not json")]
        [InlineData(@"{ ""id"": ""x"" }")]
        [InlineData(@"[{ ""executableName"": ""game.exe"", ""rating"": ""Playable"" }]")]
        [InlineData(@"[{ ""id"": ""x"", ""rating"": ""Playable"" }]")]
        [InlineData(@"[{ ""id"": ""x"", ""executableName"": ""game.exe"" }]")]
        [InlineData(@"[{ ""id"": ""x"", ""executableName"": ""game.exe"", ""rating"": ""Amazing"" }]")]
        [InlineData("[42]")]
        public void Json_Malformed_ThrowsInvalidDataException(string json)
        {
            Assert.Throws<InvalidDataException>(() => new JsonGameProfileStore(json));
        }

        [Fact]
        public void Json_Constructor_ThrowsOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => new JsonGameProfileStore((string)null!));
            Assert.Throws<ArgumentNullException>(() => new JsonGameProfileStore((TextReader)null!));
        }

        [Fact]
        public void Json_FindForGame_ThrowsOnNull()
        {
            var store = new JsonGameProfileStore("[]");
            Assert.Throws<ArgumentNullException>(() => store.FindForGame(null!));
        }
    }
}
