using System;
using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;
using XNative.Core.Logging;
using XNative.Core.Settings;
using XNative.Platform.Games;
using XNative.Platform.Logging;
using XNative.Platform.Settings;
using Xunit;

namespace XNative.Platform.Tests.Settings
{
    public sealed class SettingsOptionsApplierTests
    {
        [Fact]
        public void ApplyToCatalog_NonNullRootOverrides_AndDepthAndPatternsApply()
        {
            var settings = new AppSettings
            {
                GamesRootPath = @"E:\Games",
                ScanMaxDepth = 6,
                ExecutablePatterns = new[] { "*.exe", "*.lnk" },
            };
            var options = new LocalGameCatalogOptions
            {
                RootPath = @"C:\Old",
                MaxDepth = 3,
                ExecutablePatterns = new[] { "*.exe" },
            };

            SettingsOptionsApplier.ApplyToCatalog(settings, options);

            Assert.Equal(@"E:\Games", options.RootPath);
            Assert.Equal(6, options.MaxDepth);
            Assert.Equal(new[] { "*.exe", "*.lnk" }, options.ExecutablePatterns);
        }

        [Fact]
        public void ApplyToCatalog_NullRoot_KeepsExistingRoot()
        {
            var settings = new AppSettings
            {
                GamesRootPath = null,
                ScanMaxDepth = 2,
                ExecutablePatterns = new[] { "*.bat" },
            };
            var options = new LocalGameCatalogOptions
            {
                RootPath = @"C:\PlatformDefault",
                MaxDepth = 3,
            };

            SettingsOptionsApplier.ApplyToCatalog(settings, options);

            Assert.Equal(@"C:\PlatformDefault", options.RootPath);
            Assert.Equal(2, options.MaxDepth);
            Assert.Equal(new[] { "*.bat" }, options.ExecutablePatterns);
        }

        [Fact]
        public void ApplyToCatalog_CopiesPatternArray()
        {
            var settings = new AppSettings { ExecutablePatterns = new[] { "*.exe" } };
            var options = new LocalGameCatalogOptions();

            SettingsOptionsApplier.ApplyToCatalog(settings, options);
            settings.ExecutablePatterns[0] = "*.com";

            Assert.Equal("*.exe", options.ExecutablePatterns[0]);
        }

        [Fact]
        public void ApplyToLog_SetsMinimumLevelOnly()
        {
            var settings = new AppSettings { LogMinimumLevel = LogLevel.Error };
            var options = new FileLogServiceOptions
            {
                LogDirectory = @"C:\Logs",
                FileName = "custom.log",
                MinimumLevel = LogLevel.Information,
                BufferCapacity = 42,
            };

            SettingsOptionsApplier.ApplyToLog(settings, options);

            Assert.Equal(LogLevel.Error, options.MinimumLevel);
            Assert.Equal(@"C:\Logs", options.LogDirectory);
            Assert.Equal("custom.log", options.FileName);
            Assert.Equal(42, options.BufferCapacity);
        }

        [Fact]
        public void Apply_Overloads_MatchNamedHelpers()
        {
            var settings = new AppSettings
            {
                GamesRootPath = @"E:\Games",
                LogMinimumLevel = LogLevel.Debug,
                ScanMaxDepth = 4,
            };
            var catalog = new LocalGameCatalogOptions();
            var log = new FileLogServiceOptions();

            SettingsOptionsApplier.Apply(settings, catalog);
            SettingsOptionsApplier.Apply(settings, log);

            Assert.Equal(@"E:\Games", catalog.RootPath);
            Assert.Equal(4, catalog.MaxDepth);
            Assert.Equal(LogLevel.Debug, log.MinimumLevel);
        }

        [Fact]
        public void ApplyToCatalog_InvalidSettings_ThrowsAndAppliesNothing()
        {
            // The applier is the boundary between persisted settings
            // and the catalog: an out-of-range depth must fail loudly
            // here instead of flowing into the catalog options.
            var settings = new AppSettings
            {
                GamesRootPath = @"E:\Games",
                ScanMaxDepth = 10000,
                ExecutablePatterns = new[] { "*.exe" },
            };
            var options = new LocalGameCatalogOptions
            {
                RootPath = @"C:\PlatformDefault",
                MaxDepth = 3,
                ExecutablePatterns = new[] { "*.bin" },
            };

            var ex = Assert.Throws<ArgumentException>(
                () => SettingsOptionsApplier.ApplyToCatalog(settings, options));

            Assert.Equal("ScanMaxDepth", ex.ParamName);
            Assert.Equal(@"C:\PlatformDefault", options.RootPath);
            Assert.Equal(3, options.MaxDepth);
            Assert.Equal(new[] { "*.bin" }, options.ExecutablePatterns);
        }

        [Fact]
        public void ApplyToCatalog_NullPatterns_ThrowsInsteadOfPropagatingNull()
        {
            // Regression pin: the applier used to write the null array
            // into the catalog options (via null!), and the catalog
            // scan then crashed with a NullReferenceException.
            var settings = new AppSettings { ExecutablePatterns = null! };
            var options = new LocalGameCatalogOptions
            {
                ExecutablePatterns = new[] { "*.exe" },
            };

            var ex = Assert.Throws<ArgumentException>(
                () => SettingsOptionsApplier.ApplyToCatalog(settings, options));

            Assert.Equal("ExecutablePatterns", ex.ParamName);
            Assert.Equal(new[] { "*.exe" }, options.ExecutablePatterns);
        }

        [Fact]
        public void ApplyToLog_InvalidSettings_ThrowsAndAppliesNothing()
        {
            var settings = new AppSettings { LogMinimumLevel = (LogLevel)99 };
            var options = new FileLogServiceOptions { MinimumLevel = LogLevel.Information };

            var ex = Assert.Throws<ArgumentException>(
                () => SettingsOptionsApplier.ApplyToLog(settings, options));

            Assert.Equal("LogMinimumLevel", ex.ParamName);
            Assert.Equal(LogLevel.Information, options.MinimumLevel);
        }

        [Fact]
        public void Applier_RejectsNullArguments()
        {
            var settings = new AppSettings();
            var catalog = new LocalGameCatalogOptions();
            var log = new FileLogServiceOptions();

            Assert.Throws<ArgumentNullException>(() => SettingsOptionsApplier.ApplyToCatalog(null!, catalog));
            Assert.Throws<ArgumentNullException>(() => SettingsOptionsApplier.ApplyToCatalog(settings, null!));
            Assert.Throws<ArgumentNullException>(() => SettingsOptionsApplier.ApplyToLog(null!, log));
            Assert.Throws<ArgumentNullException>(() => SettingsOptionsApplier.ApplyToLog(settings, null!));
        }
    }

    public sealed class SettingsModuleTests
    {
        [Fact]
        public void AddXNativeSettings_RegistersSettingsService()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IStorageService>(new FakeStorageService());

            services.AddXNativeSettings();

            using (var provider = services.BuildServiceProvider())
            {
                var service = provider.GetService<ISettingsService>();
                Assert.NotNull(service);
                Assert.IsType<JsonSettingsService>(service);
            }
        }

        [Fact]
        public void AddXNativeSettings_RejectsNullServices()
        {
            IServiceCollection services = null!;
            Assert.Throws<ArgumentNullException>(() => services.AddXNativeSettings());
        }
    }
}
