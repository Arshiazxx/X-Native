using XNative.Core.Games;
using Xunit;

namespace XNative.Core.Tests.Games
{
    public sealed class LaunchOptionsTests
    {
        [Fact]
        public void Default_ReturnsAFreshInstanceOnEveryAccess()
        {
            LaunchOptions first = LaunchOptions.Default;
            LaunchOptions second = LaunchOptions.Default;

            Assert.NotSame(first, second);
        }

        [Fact]
        public void Default_MutatingOneInstance_DoesNotAffectAnother()
        {
            LaunchOptions mutated = LaunchOptions.Default;
            mutated.Arguments = "--mutated";
            mutated.WorkingDirectory = @"C:\Elsewhere";
            mutated.ProfileId = "p-mutated";
            mutated.EnvironmentVariables["XNATIVE_TEST"] = "1";

            LaunchOptions fresh = LaunchOptions.Default;

            Assert.Null(fresh.Arguments);
            Assert.Null(fresh.WorkingDirectory);
            Assert.Null(fresh.ProfileId);
            Assert.Empty(fresh.EnvironmentVariables);
        }

        [Fact]
        public void Default_CarriesNoOverrides()
        {
            LaunchOptions options = LaunchOptions.Default;

            Assert.Null(options.Arguments);
            Assert.Null(options.WorkingDirectory);
            Assert.Null(options.ProfileId);
            Assert.NotNull(options.EnvironmentVariables);
            Assert.Empty(options.EnvironmentVariables);
        }
    }
}
