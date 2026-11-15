using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XNative.Core.Abstractions;
using XNative.Core.Logging;
using XNative.Core.Settings;
using XNative.Platform.Games;
using XNative.Platform.Logging;
using XNative.Platform.Settings;
using Xunit;

namespace XNative.App.Tests
{
    /// <summary>
    /// Settings wiring of the console head: the composition resolves the
    /// settings service, loaded settings are applied onto the catalog
    /// and log options with the documented precedence, and the load
    /// seam (<see cref="SettingsBootstrap"/>) never lets a broken
    /// settings store stop the application.
    /// </summary>
    /// <remarks>
    /// The precedence and fallback behaviour is tested at the seam
    /// level with doubles because the storage root cannot be redirected
    /// in a test: <c>PlatformModule</c> computes it from
    /// <see cref="Environment.SpecialFolder.LocalApplicationData"/>
    /// internally and exposes no override, so a pre-seeded
    /// <c>settings.json</c> cannot be planted for the real composition
    /// without writing into the developer's real application data. The
    /// composition-level tests therefore hand an
    /// <see cref="AppSettings"/> instance straight to
    /// <see cref="CompositionRoot.Build(AppSettings, string)"/>, which
    /// is exactly what the CLI does with the instance the bootstrap
    /// loaded.
    /// </remarks>
    public sealed class SettingsWiringTests
    {
        // ---- Composition ----------------------------------------------

        [Fact]
        public void Build_ResolvesSettingsService()
        {
            using ServiceProvider provider = CompositionRoot.Build();

            ISettingsService service = provider.GetRequiredService<ISettingsService>();

            Assert.IsType<JsonSettingsService>(service);
        }

        [Fact]
        public void Build_WithSettings_AppliesCatalogOptions()
        {
            string root = Path.Combine(Path.GetTempPath(), "xnative-settings-root");
            var settings = new AppSettings
            {
                GamesRootPath = root,
                ScanMaxDepth = 5,
                ExecutablePatterns = new[] { "*.exe", "*.bat" },
            };

            using ServiceProvider provider = CompositionRoot.Build(settings, null);

            LocalGameCatalogOptions options =
                provider.GetRequiredService<IOptions<LocalGameCatalogOptions>>().Value;

            Assert.Equal(root, options.RootPath);
            Assert.Equal(5, options.MaxDepth);
            Assert.Equal(new[] { "*.exe", "*.bat" }, options.ExecutablePatterns);
        }

        [Fact]
        public void Build_WithSettings_AppliesLogMinimumLevel()
        {
            var settings = new AppSettings { LogMinimumLevel = LogLevel.Warning };

            using ServiceProvider provider = CompositionRoot.Build(settings, null);

            FileLogServiceOptions options =
                provider.GetRequiredService<IOptions<FileLogServiceOptions>>().Value;

            Assert.Equal(LogLevel.Warning, options.MinimumLevel);
        }

        [Fact]
        public void Build_WithSettingsAndPathArgument_PathArgumentWins()
        {
            string settingsRoot = Path.Combine(Path.GetTempPath(), "xnative-from-settings");
            string argumentRoot = Path.Combine(Path.GetTempPath(), "xnative-from-argument");
            var settings = new AppSettings { GamesRootPath = settingsRoot };

            using ServiceProvider provider = CompositionRoot.Build(settings, argumentRoot);

            LocalGameCatalogOptions options =
                provider.GetRequiredService<IOptions<LocalGameCatalogOptions>>().Value;

            Assert.Equal(argumentRoot, options.RootPath);

            // The caller's settings instance is never mutated by the
            // precedence resolution.
            Assert.Equal(settingsRoot, settings.GamesRootPath);
        }

        [Fact]
        public void Build_WithDefaultSettings_KeepsPlatformDefaultGamesRoot()
        {
            using ServiceProvider provider = CompositionRoot.Build(new AppSettings(), null);

            LocalGameCatalogOptions options =
                provider.GetRequiredService<IOptions<LocalGameCatalogOptions>>().Value;

            // The platform module default is the documents folder; a
            // default settings instance must not override it.
            Assert.Equal(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                options.RootPath);
            Assert.Equal(AppSettings.DefaultScanMaxDepth, options.MaxDepth);
        }

        // ---- Precedence seam ------------------------------------------

        [Fact]
        public void ResolveGamesRoot_CommandLineBeatsSettings()
        {
            var settings = new AppSettings { GamesRootPath = "from-settings" };

            GamesRootResolution resolution = SettingsBootstrap.ResolveGamesRoot(settings, "from-argument");

            Assert.Equal("from-argument", resolution.Path);
            Assert.Equal(GamesRootSource.CommandLine, resolution.Source);
        }

        [Fact]
        public void ResolveGamesRoot_SettingsBeatPlatformDefault()
        {
            var settings = new AppSettings { GamesRootPath = "from-settings" };

            GamesRootResolution resolution = SettingsBootstrap.ResolveGamesRoot(settings, null);

            Assert.Equal("from-settings", resolution.Path);
            Assert.Equal(GamesRootSource.Settings, resolution.Source);
        }

        [Fact]
        public void ResolveGamesRoot_NeitherSupplied_UsesPlatformDefault()
        {
            GamesRootResolution resolution = SettingsBootstrap.ResolveGamesRoot(new AppSettings(), null);

            Assert.Null(resolution.Path);
            Assert.Equal(GamesRootSource.PlatformDefault, resolution.Source);
        }

        [Fact]
        public void ResolveGamesRoot_WhitespaceCountsAsNotSupplied()
        {
            var settings = new AppSettings { GamesRootPath = "from-settings" };

            GamesRootResolution resolution = SettingsBootstrap.ResolveGamesRoot(settings, "   ");

            Assert.Equal("from-settings", resolution.Path);
            Assert.Equal(GamesRootSource.Settings, resolution.Source);
        }

        [Fact]
        public void ResolveEffectiveSettings_DoesNotMutateInput()
        {
            var settings = new AppSettings { GamesRootPath = "from-settings" };

            AppSettings effective = SettingsBootstrap.ResolveEffectiveSettings(settings, "from-argument");

            Assert.Equal("from-argument", effective.GamesRootPath);
            Assert.Equal("from-settings", settings.GamesRootPath);
            Assert.NotSame(settings, effective);
        }

        // ---- Load seam: failure honesty --------------------------------

        [Fact]
        public async Task LoadAsync_ThrowingService_ReturnsDefaultsWithWarning()
        {
            SettingsLoadResult result = await SettingsBootstrap.LoadAsync(new ThrowingSettingsService());

            Assert.True(result.UsedFallback);
            Assert.False(string.IsNullOrWhiteSpace(result.LoadWarning));
            Assert.Contains("using defaults", result.LoadWarning);
            Assert.Null(result.Settings.GamesRootPath);
            Assert.Equal(LogLevel.Information, result.Settings.LogMinimumLevel);
            Assert.Equal(AppSettings.DefaultScanMaxDepth, result.Settings.ScanMaxDepth);
        }

        [Fact]
        public async Task LoadAsync_StubService_ReturnsItsSettingsWithoutWarning()
        {
            var stored = new AppSettings
            {
                GamesRootPath = "stored-root",
                LogMinimumLevel = LogLevel.Debug,
                ScanMaxDepth = 7,
            };

            SettingsLoadResult result = await SettingsBootstrap.LoadAsync(new StubSettingsService(stored));

            Assert.False(result.UsedFallback);
            Assert.Null(result.LoadWarning);
            Assert.Equal("stored-root", result.Settings.GamesRootPath);
            Assert.Equal(LogLevel.Debug, result.Settings.LogMinimumLevel);
            Assert.Equal(7, result.Settings.ScanMaxDepth);
        }

        [Fact]
        public async Task LoadAsync_CorruptStoredJson_ReturnsDefaultsWithoutWarning()
        {
            // Corruption is the settings service's own contract: it maps
            // bad data to the defaults, so the bootstrap sees a
            // successful load and produces no warning.
            var storage = new InMemoryStorageService();
            storage.Seed(JsonSettingsService.StorageKey, "{ this is not settings json");
            var service = new JsonSettingsService(storage);

            SettingsLoadResult result = await SettingsBootstrap.LoadAsync(service);

            Assert.False(result.UsedFallback);
            Assert.Null(result.LoadWarning);
            Assert.Null(result.Settings.GamesRootPath);
            Assert.Equal(AppSettings.DefaultScanMaxDepth, result.Settings.ScanMaxDepth);
        }

        [Fact]
        public async Task LoadAsync_ValidStoredJson_ReturnsStoredValues()
        {
            var storage = new InMemoryStorageService();
            storage.Seed(
                JsonSettingsService.StorageKey,
                "{\"version\": 1, \"gamesRootPath\": \"stored-root\", "
                + "\"logMinimumLevel\": \"Warning\", \"scanMaxDepth\": 5, "
                + "\"executablePatterns\": [\"*.exe\"]}");
            var service = new JsonSettingsService(storage);

            SettingsLoadResult result = await SettingsBootstrap.LoadAsync(service);

            Assert.False(result.UsedFallback);
            Assert.Equal("stored-root", result.Settings.GamesRootPath);
            Assert.Equal(LogLevel.Warning, result.Settings.LogMinimumLevel);
            Assert.Equal(5, result.Settings.ScanMaxDepth);
        }

        // ---- Doubles ----------------------------------------------------

        private sealed class StubSettingsService : ISettingsService
        {
            private readonly AppSettings _settings;

            public StubSettingsService(AppSettings settings)
            {
                _settings = settings;
            }

            public Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_settings.Clone());
            }

            public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task ResetAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingSettingsService : ISettingsService
        {
            public Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
            {
                throw new IOException("storage is unavailable");
            }

            public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
            {
                throw new IOException("storage is unavailable");
            }

            public Task ResetAsync(CancellationToken cancellationToken = default)
            {
                throw new IOException("storage is unavailable");
            }
        }

        private sealed class InMemoryStorageService : IStorageService
        {
            private readonly Dictionary<string, string> _values =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public void Seed(string key, string value)
            {
                _values[key] = value;
            }

            public Task<string?> ReadTextAsync(string key, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(
                    _values.TryGetValue(key, out string? value) ? value : null);
            }

            public Task WriteTextAsync(string key, string value, CancellationToken cancellationToken = default)
            {
                _values[key] = value;
                return Task.CompletedTask;
            }

            public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_values.ContainsKey(key));
            }

            public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
            {
                _values.Remove(key);
                return Task.CompletedTask;
            }
        }
    }
}
