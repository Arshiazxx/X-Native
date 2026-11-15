using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using XNative.Core.Logging;
using XNative.Core.Settings;
using XNative.Platform.Settings;
using XNative.Platform.Storage;
using Xunit;

namespace XNative.Platform.Tests.Settings
{
    public sealed class JsonSettingsServiceTests
    {
        private static AppSettings CustomSettings()
        {
            return new AppSettings
            {
                GamesRootPath = @"E:\Games",
                LogMinimumLevel = LogLevel.Warning,
                ScanMaxDepth = 7,
                ExecutablePatterns = new[] { "*.exe", "*.lnk" },
            };
        }

        private static void AssertDefaults(AppSettings settings)
        {
            Assert.NotNull(settings);
            Assert.Null(settings.GamesRootPath);
            Assert.Equal(LogLevel.Information, settings.LogMinimumLevel);
            Assert.Equal(3, settings.ScanMaxDepth);
            Assert.Equal(new[] { "*.exe" }, settings.ExecutablePatterns);
        }

        private static void AssertCustom(AppSettings settings)
        {
            Assert.Equal(@"E:\Games", settings.GamesRootPath);
            Assert.Equal(LogLevel.Warning, settings.LogMinimumLevel);
            Assert.Equal(7, settings.ScanMaxDepth);
            Assert.Equal(new[] { "*.exe", "*.lnk" }, settings.ExecutablePatterns);
        }

        [Fact]
        public void Constructor_RejectsNullStorage()
        {
            Assert.Throws<ArgumentNullException>(() => new JsonSettingsService(null!));
        }

        [Fact]
        public async Task Get_WhenEmpty_ReturnsDefaults()
        {
            var service = new JsonSettingsService(new FakeStorageService());

            AssertDefaults(await service.GetAsync());
        }

        [Fact]
        public async Task SaveThenGet_RoundTrips_ThroughFake()
        {
            var storage = new FakeStorageService();
            var service = new JsonSettingsService(storage);

            await service.SaveAsync(CustomSettings());
            AssertCustom(await service.GetAsync());

            // The persisted text is versioned JSON with the level by name.
            string? stored = storage.Peek(JsonSettingsService.StorageKey);
            Assert.NotNull(stored);
            Assert.Contains("\"version\": 1", stored);
            Assert.Contains("Warning", stored);
        }

        [Fact]
        public async Task RoundTrip_ThroughRealFileStorageService()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "xnative-settings-" + Guid.NewGuid().ToString("N"));
            try
            {
                var fileStorage = new FileStorageService(
                    Options.Create(new FileStorageServiceOptions { RootPath = root }));

                var writer = new JsonSettingsService(fileStorage);
                await writer.SaveAsync(CustomSettings());

                // A fresh service over the same storage proves the
                // values really persisted, not just the cache.
                var reader = new JsonSettingsService(fileStorage);
                AssertCustom(await reader.GetAsync());
                Assert.True(File.Exists(Path.Combine(root, "settings.json")));
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, recursive: true);
                    }
                }
                catch (IOException)
                {
                }
            }
        }

        [Fact]
        public async Task Get_CorruptJson_ReturnsDefaultsAndOverwritesNothing()
        {
            var storage = new FakeStorageService();
            storage.Seed(JsonSettingsService.StorageKey, "{ this is not json");
            var service = new JsonSettingsService(storage);

            AssertDefaults(await service.GetAsync());

            Assert.Equal(0, storage.WriteCount);
            Assert.Equal("{ this is not json", storage.Peek(JsonSettingsService.StorageKey));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("null")]
        [InlineData("[]")]
        public async Task Get_MalformedPayload_ReturnsDefaults(string payload)
        {
            var storage = new FakeStorageService();
            storage.Seed(JsonSettingsService.StorageKey, payload);
            var service = new JsonSettingsService(storage);

            AssertDefaults(await service.GetAsync());
            Assert.Equal(0, storage.WriteCount);
        }

        [Fact]
        public async Task Get_InvalidStoredValues_ReturnDefaultsAndOverwriteNothing()
        {
            const string payload = "{\"version\":1,\"gamesRootPath\":null,"
                + "\"logMinimumLevel\":\"Warning\",\"scanMaxDepth\":99,"
                + "\"executablePatterns\":[\"*.exe\"]}";
            var storage = new FakeStorageService();
            storage.Seed(JsonSettingsService.StorageKey, payload);
            var service = new JsonSettingsService(storage);

            AssertDefaults(await service.GetAsync());
            Assert.Equal(0, storage.WriteCount);
            Assert.Equal(payload, storage.Peek(JsonSettingsService.StorageKey));
        }

        [Fact]
        public async Task Get_FutureVersion_ReturnsDefaultsAndOverwritesNothing()
        {
            const string payload = "{\"version\":2,\"gamesRootPath\":\"E:\\\\Games\","
                + "\"logMinimumLevel\":\"Warning\",\"scanMaxDepth\":7,"
                + "\"executablePatterns\":[\"*.exe\"]}";
            var storage = new FakeStorageService();
            storage.Seed(JsonSettingsService.StorageKey, payload);
            var service = new JsonSettingsService(storage);

            AssertDefaults(await service.GetAsync());
            Assert.Equal(0, storage.WriteCount);
            Assert.Equal(payload, storage.Peek(JsonSettingsService.StorageKey));
        }

        [Fact]
        public async Task Get_UnknownLogLevelName_FallsBackForThatFieldOnly()
        {
            // Pinned decision: an unknown level name defaults that one
            // field to Information; the other stored fields survive.
            const string payload = "{\"version\":1,\"gamesRootPath\":\"E:\\\\Games\","
                + "\"logMinimumLevel\":\"Chatty\",\"scanMaxDepth\":7,"
                + "\"executablePatterns\":[\"*.exe\",\"*.lnk\"]}";
            var storage = new FakeStorageService();
            storage.Seed(JsonSettingsService.StorageKey, payload);
            var service = new JsonSettingsService(storage);

            var settings = await service.GetAsync();

            Assert.Equal(LogLevel.Information, settings.LogMinimumLevel);
            Assert.Equal(@"E:\Games", settings.GamesRootPath);
            Assert.Equal(7, settings.ScanMaxDepth);
            Assert.Equal(new[] { "*.exe", "*.lnk" }, settings.ExecutablePatterns);
        }

        [Fact]
        public async Task Get_LogLevelName_IsParsedCaseInsensitively()
        {
            const string payload = "{\"version\":1,\"logMinimumLevel\":\"warning\"}";
            var storage = new FakeStorageService();
            storage.Seed(JsonSettingsService.StorageKey, payload);
            var service = new JsonSettingsService(storage);

            var settings = await service.GetAsync();

            Assert.Equal(LogLevel.Warning, settings.LogMinimumLevel);
            Assert.Equal(3, settings.ScanMaxDepth);
            Assert.Equal(new[] { "*.exe" }, settings.ExecutablePatterns);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(99)]
        public async Task Save_InvalidDepth_ThrowsAndWritesNothing(int depth)
        {
            var storage = new FakeStorageService();
            var service = new JsonSettingsService(storage);
            var settings = CustomSettings();
            settings.ScanMaxDepth = depth;

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(settings));

            Assert.Equal("ScanMaxDepth", ex.ParamName);
            Assert.Equal(0, storage.WriteCount);
            Assert.Null(storage.Peek(JsonSettingsService.StorageKey));
        }

        [Fact]
        public async Task Save_EmptyPatterns_ThrowsAndWritesNothing()
        {
            var storage = new FakeStorageService();
            var service = new JsonSettingsService(storage);
            var settings = CustomSettings();
            settings.ExecutablePatterns = Array.Empty<string>();

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(settings));

            Assert.Equal("ExecutablePatterns", ex.ParamName);
            Assert.Equal(0, storage.WriteCount);
        }

        [Fact]
        public async Task Save_NullSettings_ThrowsAndWritesNothing()
        {
            var storage = new FakeStorageService();
            var service = new JsonSettingsService(storage);

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveAsync(null!));
            Assert.Equal(0, storage.WriteCount);
        }

        [Fact]
        public async Task Get_CachesAfterFirstLoad_SecondGetDoesNotReReadStorage()
        {
            var storage = new FakeStorageService();
            storage.Seed(
                JsonSettingsService.StorageKey,
                "{\"version\":1,\"gamesRootPath\":\"E:\\\\Games\","
                + "\"logMinimumLevel\":\"Warning\",\"scanMaxDepth\":7,"
                + "\"executablePatterns\":[\"*.exe\",\"*.lnk\"]}");
            var service = new JsonSettingsService(storage);

            AssertCustom(await service.GetAsync());
            AssertCustom(await service.GetAsync());
            AssertCustom(await service.GetAsync());

            Assert.Equal(1, storage.ReadCount);
        }

        [Fact]
        public async Task Get_ReturnsClones_CallerMutationDoesNotPoisonCache()
        {
            var storage = new FakeStorageService();
            var service = new JsonSettingsService(storage);
            await service.SaveAsync(CustomSettings());

            var first = await service.GetAsync();
            first.ScanMaxDepth = 1;
            first.ExecutablePatterns[0] = "*.com";

            AssertCustom(await service.GetAsync());
        }

        [Fact]
        public async Task Save_UpdatesCache_WithoutReReadingStorage()
        {
            var storage = new FakeStorageService();
            var service = new JsonSettingsService(storage);

            AssertDefaults(await service.GetAsync());
            Assert.Equal(1, storage.ReadCount);

            await service.SaveAsync(CustomSettings());

            AssertCustom(await service.GetAsync());
            Assert.Equal(1, storage.ReadCount);
        }

        [Fact]
        public async Task Reset_RestoresDefaults_AndPersistsThem()
        {
            var storage = new FakeStorageService();
            var service = new JsonSettingsService(storage);
            await service.SaveAsync(CustomSettings());
            AssertCustom(await service.GetAsync());

            await service.ResetAsync();

            AssertDefaults(await service.GetAsync());

            // A fresh service over the same storage also sees defaults,
            // proving the reset was persisted, not just cached.
            var fresh = new JsonSettingsService(storage);
            AssertDefaults(await fresh.GetAsync());
        }
    }
}
