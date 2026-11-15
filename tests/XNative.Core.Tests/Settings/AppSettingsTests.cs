using System;
using XNative.Core.Logging;
using XNative.Core.Settings;
using Xunit;

namespace XNative.Core.Tests.Settings
{
    public sealed class AppSettingsTests
    {
        [Fact]
        public void Defaults_AreSane()
        {
            var settings = new AppSettings();

            Assert.Null(settings.GamesRootPath);
            Assert.Equal(LogLevel.Information, settings.LogMinimumLevel);
            Assert.Equal(3, settings.ScanMaxDepth);
            Assert.Equal(new[] { "*.exe" }, settings.ExecutablePatterns);
        }

        [Fact]
        public void CreateDefault_MatchesConstructorDefaults()
        {
            var settings = AppSettings.CreateDefault();

            Assert.Null(settings.GamesRootPath);
            Assert.Equal(LogLevel.Information, settings.LogMinimumLevel);
            Assert.Equal(3, settings.ScanMaxDepth);
            Assert.Equal(new[] { "*.exe" }, settings.ExecutablePatterns);
        }

        [Fact]
        public void Validate_DefaultsPass()
        {
            var settings = new AppSettings();
            settings.Validate();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        public void Validate_DepthAtBoundsPasses(int depth)
        {
            var settings = new AppSettings { ScanMaxDepth = depth };
            settings.Validate();
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(11)]
        [InlineData(99)]
        public void Validate_DepthOutOfRangeThrowsNamingProperty(int depth)
        {
            var settings = new AppSettings { ScanMaxDepth = depth };

            var ex = Assert.Throws<ArgumentException>(() => settings.Validate());
            Assert.Equal("ScanMaxDepth", ex.ParamName);
            Assert.Contains("ScanMaxDepth", ex.Message);
        }

        [Fact]
        public void Validate_NullPatternsThrowsNamingProperty()
        {
            var settings = new AppSettings { ExecutablePatterns = null! };

            var ex = Assert.Throws<ArgumentException>(() => settings.Validate());
            Assert.Equal("ExecutablePatterns", ex.ParamName);
        }

        [Fact]
        public void Validate_EmptyPatternsThrowsNamingProperty()
        {
            var settings = new AppSettings { ExecutablePatterns = Array.Empty<string>() };

            var ex = Assert.Throws<ArgumentException>(() => settings.Validate());
            Assert.Equal("ExecutablePatterns", ex.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_BlankPatternEntryThrowsNamingProperty(string pattern)
        {
            var settings = new AppSettings { ExecutablePatterns = new[] { "*.exe", pattern } };

            var ex = Assert.Throws<ArgumentException>(() => settings.Validate());
            Assert.Equal("ExecutablePatterns", ex.ParamName);
        }

        [Fact]
        public void Validate_NullPatternEntryThrowsNamingProperty()
        {
            var settings = new AppSettings { ExecutablePatterns = new string[] { null! } };

            var ex = Assert.Throws<ArgumentException>(() => settings.Validate());
            Assert.Equal("ExecutablePatterns", ex.ParamName);
        }

        [Fact]
        public void Validate_PatternCountAtCapPasses()
        {
            var patterns = new string[AppSettings.MaxExecutablePatterns];
            for (int i = 0; i < patterns.Length; i++)
            {
                patterns[i] = "*.exe" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            var settings = new AppSettings { ExecutablePatterns = patterns };
            settings.Validate();
        }

        [Fact]
        public void Validate_TooManyPatternsThrowsNamingProperty()
        {
            // Each pattern costs one directory enumeration per scanned
            // directory, so an unbounded list (a corrupt or hostile
            // settings file could carry thousands) is a scan DoS.
            var patterns = new string[AppSettings.MaxExecutablePatterns + 1];
            for (int i = 0; i < patterns.Length; i++)
            {
                patterns[i] = "*.exe" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            var settings = new AppSettings { ExecutablePatterns = patterns };

            var ex = Assert.Throws<ArgumentException>(() => settings.Validate());
            Assert.Equal("ExecutablePatterns", ex.ParamName);
        }

        [Theory]
        [InlineData("sub/*.exe")]
        [InlineData("sub\\*.exe")]
        [InlineData("/abs/*.exe")]
        public void Validate_PatternWithDirectorySeparatorThrowsNamingProperty(string pattern)
        {
            // Patterns are file-name patterns, not paths.
            var settings = new AppSettings { ExecutablePatterns = new[] { pattern } };

            var ex = Assert.Throws<ArgumentException>(() => settings.Validate());
            Assert.Equal("ExecutablePatterns", ex.ParamName);
        }

        [Theory]
        [InlineData("*")]
        [InlineData("*.*")]
        public void Validate_BareWildcardPatternsPass(string pattern)
        {
            // Pinned decision: a match-everything pattern is NOT
            // banned. It costs the same single enumeration per
            // directory as "*.exe", and matching every file is a
            // legitimate choice when executables carry no consistent
            // extension — the catalog's PE inspection decides what is
            // actually a game.
            var settings = new AppSettings { ExecutablePatterns = new[] { pattern } };
            settings.Validate();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_BlankGamesRootPathThrowsNamingProperty(string path)
        {
            var settings = new AppSettings { GamesRootPath = path };

            var ex = Assert.Throws<ArgumentException>(() => settings.Validate());
            Assert.Equal("GamesRootPath", ex.ParamName);
        }

        [Fact]
        public void Validate_UndefinedLogLevelThrowsNamingProperty()
        {
            var settings = new AppSettings { LogMinimumLevel = (LogLevel)99 };

            var ex = Assert.Throws<ArgumentException>(() => settings.Validate());
            Assert.Equal("LogMinimumLevel", ex.ParamName);
        }

        [Fact]
        public void Clone_IsDeepAndIndependent()
        {
            var settings = new AppSettings
            {
                GamesRootPath = @"D:\Games",
                LogMinimumLevel = LogLevel.Warning,
                ScanMaxDepth = 5,
                ExecutablePatterns = new[] { "*.exe", "*.bat" },
            };

            var clone = settings.Clone();
            clone.ExecutablePatterns[0] = "*.com";
            clone.ScanMaxDepth = 1;

            Assert.Equal(@"D:\Games", clone.GamesRootPath);
            Assert.Equal(LogLevel.Warning, clone.LogMinimumLevel);
            Assert.Equal("*.exe", settings.ExecutablePatterns[0]);
            Assert.Equal(5, settings.ScanMaxDepth);
        }
    }
}
