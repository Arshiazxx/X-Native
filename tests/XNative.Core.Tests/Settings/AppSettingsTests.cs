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
