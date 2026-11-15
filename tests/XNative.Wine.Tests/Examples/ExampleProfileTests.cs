using System;
using System.IO;
using XNative.Core.Games;
using XNative.Wine.Profiles;
using Xunit;

namespace XNative.Wine.Tests.Examples
{
    /// <summary>
    /// Pins the worked profile database at
    /// <c>examples/profiles/profiles.json</c> against the real
    /// <see cref="JsonGameProfileStore"/>. The expected IDs, ratings and
    /// recommended arguments are the ones <c>examples/README.md</c>
    /// documents; if the file or the store's reading of it drifts, these
    /// tests fail instead of the example silently rotting.
    /// </summary>
    public sealed class ExampleProfileTests
    {
        [Fact]
        public void ExampleProfiles_LoadExactlyThreeProfiles()
        {
            var store = LoadExampleStore();

            Assert.Equal(3, store.GetProfiles().Count);
        }

        [Fact]
        public void ExampleProfiles_MatchDocumentedIdsRatingsAndArguments()
        {
            var profiles = LoadExampleStore().GetProfiles();

            var fullFeatured = Assert.Single(profiles, p => p.Id == "full-featured-game");
            Assert.Equal("FullFeaturedGame.exe", fullFeatured.ExecutableName);
            Assert.Equal(CompatibilityRating.Playable, fullFeatured.Rating);
            Assert.Equal("-windowed", fullFeatured.RecommendedArguments);

            var bare = Assert.Single(profiles, p => p.Id == "bare-game");
            Assert.Equal("BareGame.exe", bare.ExecutableName);
            Assert.Equal(CompatibilityRating.Starts, bare.Rating);
            Assert.Equal("-novid -windowed", bare.RecommendedArguments);

            var minimal = Assert.Single(profiles, p => p.Id == "minimal-game");
            Assert.Equal("MinimalGame.exe", minimal.ExecutableName);
            Assert.Equal(CompatibilityRating.Broken, minimal.Rating);
            Assert.Equal("-safe-mode", minimal.RecommendedArguments);
        }

        [Fact]
        public void FindForGame_ResolvesEachExampleGameToItsDocumentedProfile()
        {
            var store = LoadExampleStore();

            var fullFeatured = store.FindForGame(CreateGame("/games/FullFeaturedGame/game/FullFeaturedGame.exe"));
            Assert.NotNull(fullFeatured);
            Assert.Equal("full-featured-game", fullFeatured!.Id);
            Assert.Equal(CompatibilityRating.Playable, fullFeatured.Rating);

            var bare = store.FindForGame(CreateGame("/games/BareGame/BareGame.exe"));
            Assert.NotNull(bare);
            Assert.Equal("bare-game", bare!.Id);
            Assert.Equal(CompatibilityRating.Starts, bare.Rating);

            var minimal = store.FindForGame(CreateGame("/games/MinimalGame/MinimalGame.exe"));
            Assert.NotNull(minimal);
            Assert.Equal("minimal-game", minimal!.Id);
            Assert.Equal(CompatibilityRating.Broken, minimal.Rating);
        }

        private static JsonGameProfileStore LoadExampleStore()
        {
            var path = Path.Combine(FindRepositoryRoot(), "examples", "profiles", "profiles.json");
            return new JsonGameProfileStore(File.ReadAllText(path));
        }

        private static GameInfo CreateGame(string executablePath)
        {
            var name = Path.GetFileNameWithoutExtension(executablePath);
            return new GameInfo(
                "example-" + name,
                name,
                executablePath,
                Path.GetDirectoryName(executablePath) ?? string.Empty,
                ExecutableType.WindowsExecutable);
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "examples")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                "Could not locate the repository root: no ancestor of '" + AppContext.BaseDirectory +
                "' contains an 'examples' directory. The example tests must run from a build output " +
                "inside the X-Native repository.");
        }
    }
}
