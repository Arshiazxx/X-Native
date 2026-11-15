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

        [Theory]
        // Enum.TryParse would OR comma-joined names into a different
        // defined member ("Broken, Starts" -> Playable) and accept
        // numeric strings as values; both are malformed database data
        // and must throw like any other unknown rating name.
        [InlineData("Broken, Starts")]
        [InlineData("Broken,Starts")]
        [InlineData("3")]
        [InlineData("1")]
        [InlineData("-1")]
        [InlineData(" Playable")]
        public void Json_CompositeOrNumericRating_ThrowsInvalidDataException(string rating)
        {
            string json = "[{ \"id\": \"x\", \"executableName\": \"game.exe\", \"rating\": \"" + rating + "\" }]";
            Assert.Throws<InvalidDataException>(() => new JsonGameProfileStore(json));
        }

        [Theory]
        [InlineData("Unknown", CompatibilityRating.Unknown)]
        [InlineData("Broken", CompatibilityRating.Broken)]
        [InlineData("Starts", CompatibilityRating.Starts)]
        [InlineData("Playable", CompatibilityRating.Playable)]
        [InlineData("Perfect", CompatibilityRating.Perfect)]
        [InlineData("playable", CompatibilityRating.Playable)]
        [InlineData("BROKEN", CompatibilityRating.Broken)]
        public void Json_DefinedRatingNames_LoadCaseInsensitively(string ratingName, CompatibilityRating expected)
        {
            string json = "[{ \"id\": \"x\", \"executableName\": \"game.exe\", \"rating\": \"" + ratingName + "\" }]";
            var store = new JsonGameProfileStore(json);

            GameProfile profile = Assert.Single(store.GetProfiles());
            Assert.Equal(expected, profile.Rating);
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

        [Fact]
        public void InMemory_Constructor_ThrowsOnDuplicateProfileIds()
        {
            var profiles = new[]
            {
                new GameProfile("dup", "a.exe", CompatibilityRating.Playable),
                new GameProfile("dup", "b.exe", CompatibilityRating.Broken),
            };

            ArgumentException exception = Assert.Throws<ArgumentException>(() => new InMemoryGameProfileStore(profiles));
            Assert.Contains("dup", exception.Message);
        }

        [Fact]
        public void InMemory_Constructor_ThrowsOnProfileIdsDifferingOnlyByCase()
        {
            // The launch pipeline resolves LaunchOptions.ProfileId against
            // profile IDs case-insensitively, so IDs that differ only by
            // case would collide at lookup time and must count as duplicates.
            var profiles = new[]
            {
                new GameProfile("Game", "a.exe", CompatibilityRating.Playable),
                new GameProfile("game", "b.exe", CompatibilityRating.Broken),
            };

            Assert.Throws<ArgumentException>(() => new InMemoryGameProfileStore(profiles));
        }

        [Fact]
        public void InMemory_Constructor_ThrowsOnDuplicateExecutableNames()
        {
            var profiles = new[]
            {
                new GameProfile("a", "game.exe", CompatibilityRating.Playable),
                new GameProfile("b", "game.exe", CompatibilityRating.Broken),
            };

            ArgumentException exception = Assert.Throws<ArgumentException>(() => new InMemoryGameProfileStore(profiles));
            Assert.Contains("game.exe", exception.Message);
        }

        [Fact]
        public void InMemory_Constructor_ThrowsOnExecutableNamesDifferingOnlyByCase()
        {
            // Name matching is case-insensitive, so executable names that
            // differ only by case would collide at lookup time and must
            // count as duplicates, exactly like profile IDs.
            var profiles = new[]
            {
                new GameProfile("a", "Game.exe", CompatibilityRating.Playable),
                new GameProfile("b", "game.exe", CompatibilityRating.Broken),
            };

            Assert.Throws<ArgumentException>(() => new InMemoryGameProfileStore(profiles));
        }

        [Fact]
        public void InMemory_Constructor_ThrowsOnExecutableNamesDifferingOnlyByDirectory()
        {
            // Name matching strips any directory portion from the
            // profile's executable name, so a directory-prefixed entry
            // names the same executable as the bare file name.
            var profiles = new[]
            {
                new GameProfile("a", @"C:\Games\game.exe", CompatibilityRating.Playable),
                new GameProfile("b", "game.exe", CompatibilityRating.Broken),
            };

            Assert.Throws<ArgumentException>(() => new InMemoryGameProfileStore(profiles));
        }

        [Fact]
        public void Json_DuplicateProfileIds_ThrowsInvalidDataException()
        {
            const string json = @"[
              { ""id"": ""dup"", ""executableName"": ""a.exe"", ""rating"": ""Playable"" },
              { ""id"": ""dup"", ""executableName"": ""b.exe"", ""rating"": ""Broken"" }
            ]";

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => new JsonGameProfileStore(json));
            Assert.Contains("dup", exception.Message);
        }

        [Fact]
        public void Json_ProfileIdsDifferingOnlyByCase_ThrowsInvalidDataException()
        {
            const string json = @"[
              { ""id"": ""Game"", ""executableName"": ""a.exe"", ""rating"": ""Playable"" },
              { ""id"": ""game"", ""executableName"": ""b.exe"", ""rating"": ""Broken"" }
            ]";

            Assert.Throws<InvalidDataException>(() => new JsonGameProfileStore(json));
        }

        [Fact]
        public void Json_DuplicateExecutableNames_ThrowsInvalidDataException()
        {
            const string json = @"[
              { ""id"": ""a"", ""executableName"": ""game.exe"", ""rating"": ""Playable"" },
              { ""id"": ""b"", ""executableName"": ""game.exe"", ""rating"": ""Broken"" }
            ]";

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => new JsonGameProfileStore(json));
            Assert.Contains("game.exe", exception.Message);
        }

        [Fact]
        public void Json_ExecutableNamesDifferingOnlyByCase_ThrowsInvalidDataException()
        {
            const string json = @"[
              { ""id"": ""a"", ""executableName"": ""Game.exe"", ""rating"": ""Playable"" },
              { ""id"": ""b"", ""executableName"": ""game.exe"", ""rating"": ""Broken"" }
            ]";

            Assert.Throws<InvalidDataException>(() => new JsonGameProfileStore(json));
        }

        [Fact]
        public void Json_ExecutableNamesDifferingOnlyByDirectory_ThrowsInvalidDataException()
        {
            const string json = @"[
              { ""id"": ""a"", ""executableName"": ""C:\\Games\\game.exe"", ""rating"": ""Playable"" },
              { ""id"": ""b"", ""executableName"": ""game.exe"", ""rating"": ""Broken"" }
            ]";

            Assert.Throws<InvalidDataException>(() => new JsonGameProfileStore(json));
        }

        [Fact]
        public void Json_Profiles_RemainRetrievableById_AndMatchByName()
        {
            // The duplicate-name validation must not disturb the two
            // lookup paths: resolution by profile ID (how the launch
            // pipeline honours LaunchOptions.ProfileId, scanning the
            // loaded profiles case-insensitively) and matching by bare
            // executable name, including for a profile whose stored
            // name carries a directory portion.
            const string json = @"[
              { ""id"": ""a"", ""executableName"": ""C:\\Games\\game.exe"", ""rating"": ""Playable"" },
              { ""id"": ""b"", ""executableName"": ""other.exe"", ""rating"": ""Broken"" }
            ]";

            var store = new JsonGameProfileStore(json);

            GameProfile byId = store.GetProfiles().Single(
                p => string.Equals(p.Id, "B", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("other.exe", byId.ExecutableName);

            GameProfile? byName = store.FindForGame(CreateGame());
            Assert.NotNull(byName);
            Assert.Equal("a", byName!.Id);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Json_EmptyOrWhitespaceProfileId_ThrowsInvalidDataException(string id)
        {
            string json = "[{ \"id\": \"" + id + "\", \"executableName\": \"game.exe\", \"rating\": \"Playable\" }]";

            Assert.Throws<InvalidDataException>(() => new JsonGameProfileStore(json));
        }

        [Fact]
        public void Json_MultipleDistinctProfiles_LoadsAll()
        {
            const string json = @"[
              { ""id"": ""alpha"", ""executableName"": ""a.exe"", ""rating"": ""Playable"" },
              { ""id"": ""beta"", ""executableName"": ""b.exe"", ""rating"": ""Starts"" },
              { ""id"": ""gamma"", ""executableName"": ""c.exe"", ""rating"": ""Broken"" }
            ]";

            var store = new JsonGameProfileStore(json);

            Assert.Equal(3, store.GetProfiles().Count);
        }
    }
}
