using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using XNative.Core.Abstractions;
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
        public async Task Get_CompositeLogLevelName_FallsBackForThatFieldOnly()
        {
            // Enum.TryParse ORs comma-joined names: "Debug, Information"
            // would parse to 1|2 = 3 = Warning — a real but wrong level.
            // Only exact defined names are part of the stored format, so
            // the composite falls back to Information for this field and
            // the other stored fields survive.
            const string payload = "{\"version\":1,\"gamesRootPath\":\"E:\\\\Games\","
                + "\"logMinimumLevel\":\"Debug, Information\",\"scanMaxDepth\":7,"
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

        [Theory]
        [InlineData("3")]
        [InlineData("99")]
        [InlineData("-1")]
        public async Task Get_NumericLogLevelName_FallsBackForThatFieldOnly(string level)
        {
            // "3" is Warning's numeric value, but numeric names are not
            // part of the stored format: the field falls back instead
            // of loading as Warning.
            string payload = "{\"version\":1,\"gamesRootPath\":\"E:\\\\Games\","
                + "\"logMinimumLevel\":\"" + level + "\",\"scanMaxDepth\":7,"
                + "\"executablePatterns\":[\"*.exe\"]}";
            var storage = new FakeStorageService();
            storage.Seed(JsonSettingsService.StorageKey, payload);
            var service = new JsonSettingsService(storage);

            var settings = await service.GetAsync();

            Assert.Equal(LogLevel.Information, settings.LogMinimumLevel);
            Assert.Equal(@"E:\Games", settings.GamesRootPath);
            Assert.Equal(7, settings.ScanMaxDepth);
        }

        [Fact]
        public async Task Get_StorageReadIOException_ReturnsDefaults()
        {
            // The contract: GetAsync never throws because the stored
            // data is corrupt OR cannot be read — a storage failure
            // degrades to the defaults like corrupt data does.
            var storage = new ThrowingStorageService
            {
                ReadException = new IOException("disk gone"),
            };
            var service = new JsonSettingsService(storage);

            AssertDefaults(await service.GetAsync());
        }

        [Fact]
        public async Task Get_StorageReadUnauthorized_ReturnsDefaults()
        {
            var storage = new ThrowingStorageService
            {
                ReadException = new UnauthorizedAccessException("denied"),
            };
            var service = new JsonSettingsService(storage);

            AssertDefaults(await service.GetAsync());
        }

        [Fact]
        public async Task Get_StorageReadCancellation_Propagates()
        {
            var storage = new ThrowingStorageService
            {
                ReadException = new OperationCanceledException(),
            };
            var service = new JsonSettingsService(storage);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync());
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var service = new JsonSettingsService(new FakeStorageService());

            service.Dispose();
            service.Dispose();
        }

        [Fact]
        public async Task Get_AfterDispose_ThrowsObjectDisposedException()
        {
            var service = new JsonSettingsService(new FakeStorageService());
            service.Dispose();

            var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => service.GetAsync());

            Assert.Equal("JsonSettingsService", ex.ObjectName);
        }

        [Fact]
        public async Task Save_AfterDispose_ThrowsObjectDisposedException()
        {
            var storage = new FakeStorageService();
            var service = new JsonSettingsService(storage);
            service.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => service.SaveAsync(CustomSettings()));
            Assert.Equal(0, storage.WriteCount);
        }

        [Fact]
        public async Task Reset_AfterDispose_ThrowsObjectDisposedException()
        {
            var service = new JsonSettingsService(new FakeStorageService());
            service.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => service.ResetAsync());
        }

        [Fact]
        public async Task Dispose_DuringInFlightGet_DoesNotMaskTheResult()
        {
            var storage = new BlockingStorageService();
            var service = new JsonSettingsService(storage);

            Task<AppSettings> inFlight = service.GetAsync();
            await storage.ReadStarted.Task;

            service.Dispose();
            storage.ReleaseRead();

            // The in-flight load runs to completion and returns its
            // real result (defaults — the read yields nothing) instead
            // of an ObjectDisposedException leaking out of the gate
            // release in its finally block.
            AssertDefaults(await inFlight);
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

        // ---- Load outcome (ISettingsLoadOutcomeProvider) ----------------
        //
        // GetAsync deliberately collapses absent / unreadable / corrupt
        // stored data into the same defaults; these tests pin the side
        // channel that keeps the three cases distinguishable for
        // diagnostics.

        private static void AssertOutcome(
            JsonSettingsService service,
            StoredSettingsState state,
            SettingsRejectionReason reason)
        {
            Assert.NotNull(service.LastLoadOutcome);
            Assert.Equal(state, service.LastLoadOutcome!.State);
            Assert.Equal(reason, service.LastLoadOutcome.RejectionReason);
        }

        [Fact]
        public void LastLoadOutcome_BeforeAnyStorageInteraction_IsNull()
        {
            var service = new JsonSettingsService(new FakeStorageService());

            Assert.Null(service.LastLoadOutcome);
        }

        [Fact]
        public async Task Get_WhenEmpty_ReportsAbsentOutcome()
        {
            var service = new JsonSettingsService(new FakeStorageService());

            AssertDefaults(await service.GetAsync());

            AssertOutcome(service, StoredSettingsState.Absent, SettingsRejectionReason.None);
        }

        [Fact]
        public async Task Get_ValidStoredJson_ReportsLoadedOutcome()
        {
            const string payload = "{\"version\":1,\"gamesRootPath\":\"E:\\\\Games\","
                + "\"logMinimumLevel\":\"Warning\",\"scanMaxDepth\":7,"
                + "\"executablePatterns\":[\"*.exe\",\"*.lnk\"]}";
            var storage = new FakeStorageService();
            storage.Seed(JsonSettingsService.StorageKey, payload);
            var service = new JsonSettingsService(storage);

            AssertCustom(await service.GetAsync());

            AssertOutcome(service, StoredSettingsState.Loaded, SettingsRejectionReason.None);
        }

        [Fact]
        public async Task Get_CorruptJson_ReportsRejectedCorruptDataOutcome()
        {
            var storage = new FakeStorageService();
            storage.Seed(JsonSettingsService.StorageKey, "{ this is not json");
            var service = new JsonSettingsService(storage);

            AssertDefaults(await service.GetAsync());

            AssertOutcome(service, StoredSettingsState.Rejected, SettingsRejectionReason.CorruptData);
        }

        [Fact]
        public async Task Get_FutureVersion_ReportsRejectedUnsupportedVersionOutcome()
        {
            const string payload = "{\"version\":2,\"gamesRootPath\":\"E:\\\\Games\","
                + "\"logMinimumLevel\":\"Warning\",\"scanMaxDepth\":7,"
                + "\"executablePatterns\":[\"*.exe\"]}";
            var storage = new FakeStorageService();
            storage.Seed(JsonSettingsService.StorageKey, payload);
            var service = new JsonSettingsService(storage);

            AssertDefaults(await service.GetAsync());

            AssertOutcome(service, StoredSettingsState.Rejected, SettingsRejectionReason.UnsupportedVersion);
        }

        [Fact]
        public async Task Get_InvalidStoredValues_ReportsRejectedInvalidValuesOutcome()
        {
            const string payload = "{\"version\":1,\"gamesRootPath\":null,"
                + "\"logMinimumLevel\":\"Warning\",\"scanMaxDepth\":99,"
                + "\"executablePatterns\":[\"*.exe\"]}";
            var storage = new FakeStorageService();
            storage.Seed(JsonSettingsService.StorageKey, payload);
            var service = new JsonSettingsService(storage);

            AssertDefaults(await service.GetAsync());

            AssertOutcome(service, StoredSettingsState.Rejected, SettingsRejectionReason.InvalidValues);
        }

        [Fact]
        public async Task Get_StorageReadIOException_ReportsRejectedReadFailureOutcome()
        {
            var storage = new ThrowingStorageService
            {
                ReadException = new IOException("disk gone"),
            };
            var service = new JsonSettingsService(storage);

            AssertDefaults(await service.GetAsync());

            AssertOutcome(service, StoredSettingsState.Rejected, SettingsRejectionReason.ReadFailure);
        }

        [Fact]
        public async Task Get_CachedSecondCall_RetainsTheLoadOutcome()
        {
            // The outcome describes the stored data, not the most
            // recent call: a cache hit does not erase or replace it.
            var storage = new FakeStorageService();
            storage.Seed(JsonSettingsService.StorageKey, "{ this is not json");
            var service = new JsonSettingsService(storage);

            AssertDefaults(await service.GetAsync());
            AssertDefaults(await service.GetAsync());
            Assert.Equal(1, storage.ReadCount);

            AssertOutcome(service, StoredSettingsState.Rejected, SettingsRejectionReason.CorruptData);
        }

        [Fact]
        public async Task Save_AfterRejectedLoad_ReportsLoadedOutcome()
        {
            // A save replaces the rejected data with valid stored
            // settings; the outcome must say so, not keep reporting
            // the earlier rejection.
            var storage = new FakeStorageService();
            storage.Seed(JsonSettingsService.StorageKey, "{ this is not json");
            var service = new JsonSettingsService(storage);
            AssertDefaults(await service.GetAsync());
            AssertOutcome(service, StoredSettingsState.Rejected, SettingsRejectionReason.CorruptData);

            await service.SaveAsync(CustomSettings());

            AssertOutcome(service, StoredSettingsState.Loaded, SettingsRejectionReason.None);
        }

        [Fact]
        public async Task Reset_AfterRejectedLoad_ReportsLoadedOutcome()
        {
            var storage = new FakeStorageService();
            storage.Seed(JsonSettingsService.StorageKey, "{ this is not json");
            var service = new JsonSettingsService(storage);
            AssertDefaults(await service.GetAsync());
            AssertOutcome(service, StoredSettingsState.Rejected, SettingsRejectionReason.CorruptData);

            await service.ResetAsync();

            AssertOutcome(service, StoredSettingsState.Loaded, SettingsRejectionReason.None);
        }

        /// <summary>
        /// Storage whose reads always fail with a configured exception;
        /// writes succeed silently.
        /// </summary>
        private sealed class ThrowingStorageService : IStorageService
        {
            public Exception? ReadException { get; set; }

            public Task<string?> ReadTextAsync(string key, CancellationToken cancellationToken = default)
            {
                Exception? exception = ReadException;
                return exception != null
                    ? Task.FromException<string?>(exception)
                    : Task.FromResult<string?>(null);
            }

            public Task WriteTextAsync(string key, string value, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(false);
            }

            public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Storage whose read blocks until the test releases it, so a
        /// test can dispose the service while a load is in flight —
        /// deterministically, with no sleeps.
        /// </summary>
        private sealed class BlockingStorageService : IStorageService
        {
            private readonly TaskCompletionSource<bool> _release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<bool> ReadStarted { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public void ReleaseRead()
            {
                _release.TrySetResult(true);
            }

            public async Task<string?> ReadTextAsync(string key, CancellationToken cancellationToken = default)
            {
                ReadStarted.TrySetResult(true);
                await _release.Task.ConfigureAwait(false);
                return null;
            }

            public Task WriteTextAsync(string key, string value, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(false);
            }

            public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }
    }
}
