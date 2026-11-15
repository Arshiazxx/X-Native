using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Abstractions;
using XNative.Core.Logging;
using XNative.Core.Settings;

namespace XNative.Platform.Settings
{
    /// <summary>
    /// <see cref="ISettingsService"/> that persists
    /// <see cref="AppSettings"/> as JSON through an
    /// <see cref="IStorageService"/> under the key
    /// <see cref="StorageKey"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The on-disk shape is a private DTO, not
    /// <see cref="AppSettings"/> itself, so the stored format can evolve
    /// independently of the model. The log level is stored by name
    /// (for example <c>"Warning"</c>) rather than by number, and parsed
    /// case-insensitively: a stored name that is not a defined
    /// <see cref="LogLevel"/> falls back to
    /// <see cref="LogLevel.Information"/> for that field only, leaving
    /// the other stored fields intact.
    /// </para>
    /// <para>
    /// Corruption stance: if the stored text is not valid JSON, carries
    /// a format version other than <see cref="CurrentVersion"/>, or
    /// yields values that fail <see cref="AppSettings.Validate"/>,
    /// <see cref="GetAsync"/> returns the defaults and overwrites
    /// nothing — the bad data is left in storage for diagnostics. A
    /// stored version higher than <see cref="CurrentVersion"/> is a
    /// future format this build does not understand; it is treated the
    /// same way (defaults, no throw, no overwrite) so a newer build's
    /// settings never crash or get clobbered by an older build. A
    /// storage <em>read failure</em> (an
    /// <see cref="System.IO.IOException"/> or
    /// <see cref="UnauthorizedAccessException"/> from the storage
    /// backend, for example a permissions-broken app-data folder) is
    /// treated the same way again: the data is unusable either way, so
    /// <see cref="GetAsync"/> returns the defaults rather than
    /// crashing startup. Cancellation still propagates.
    /// </para>
    /// <para>
    /// Disposal: <see cref="Dispose"/> is idempotent, safe to call
    /// from several threads at once, and safe to call while an
    /// operation is in flight. An in-flight operation either runs to
    /// completion with its real result or observes the disposal; its
    /// result is never masked by the disposal. Operations started
    /// after disposal throw
    /// <see cref="ObjectDisposedException"/> naming this service.
    /// </para>
    /// <para>
    /// The first successful load is cached in a thread-safe manner;
    /// later <see cref="GetAsync"/> calls serve the cache without
    /// re-reading storage. <see cref="SaveAsync"/> and
    /// <see cref="ResetAsync"/> update the cache with what they wrote.
    /// Every value handed out is a clone, so callers cannot mutate the
    /// cache.
    /// </para>
    /// </remarks>
    public sealed class JsonSettingsService : ISettingsService, IDisposable
    {
        /// <summary>
        /// Storage key the settings JSON is stored under.
        /// </summary>
        public const string StorageKey = "settings.json";

        /// <summary>
        /// Version of the stored JSON format written by this build.
        /// </summary>
        public const int CurrentVersion = 1;

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };

        private readonly IStorageService _storage;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private AppSettings? _cached;

        // 0 = alive, 1 = disposed. Manipulated only through Interlocked
        // / Volatile so Dispose can race an in-flight operation safely
        // (the same pattern LocalGameCatalog uses).
        private int _disposed;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="JsonSettingsService"/> class.
        /// </summary>
        /// <param name="storage">Storage backend the JSON is kept in.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="storage"/> is <c>null</c>.
        /// </exception>
        public JsonSettingsService(IStorageService storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <inheritdoc />
        public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            await WaitGateAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();

                if (_cached != null)
                {
                    return _cached.Clone();
                }

                AppSettings loaded = await LoadFromStorageAsync(cancellationToken).ConfigureAwait(false);
                _cached = loaded;
                return _cached.Clone();
            }
            finally
            {
                ReleaseGate();
            }
        }

        /// <inheritdoc />
        public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            // Validate before touching storage or the cache: an invalid
            // save writes nothing.
            settings.Validate();

            AppSettings snapshot = settings.Clone();
            string json = Serialize(snapshot);

            await WaitGateAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();

                await _storage.WriteTextAsync(StorageKey, json, cancellationToken).ConfigureAwait(false);
                _cached = snapshot;
            }
            finally
            {
                ReleaseGate();
            }
        }

        /// <inheritdoc />
        public async Task ResetAsync(CancellationToken cancellationToken = default)
        {
            var defaults = AppSettings.CreateDefault();
            string json = Serialize(defaults);

            await WaitGateAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();

                await _storage.WriteTextAsync(StorageKey, json, cancellationToken).ConfigureAwait(false);
                _cached = defaults;
            }
            finally
            {
                ReleaseGate();
            }
        }

        /// <summary>
        /// Releases the synchronization resources held by the service.
        /// Idempotent and thread-safe: only the first call disposes the
        /// gate, and calling it while an operation is in flight is
        /// safe — the operation completes with its real result or
        /// observes the disposal, never a masked one (see the class
        /// remarks).
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _gate.Dispose();
            GC.SuppressFinalize(this);
        }

        private async Task WaitGateAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            try
            {
                await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Lost the race with Dispose() while waiting for the
                // gate: surface the disposal deliberately, naming the
                // service rather than the semaphore.
                throw new ObjectDisposedException(nameof(JsonSettingsService));
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(JsonSettingsService));
            }
        }

        private void ReleaseGate()
        {
            try
            {
                _gate.Release();
            }
            catch (ObjectDisposedException)
            {
                // Dispose() ran while this operation was in flight and
                // disposed the semaphore underneath it; there is
                // nothing left to release into, and the operation's
                // own result must not be masked by the disposal.
            }
        }

        private async Task<AppSettings> LoadFromStorageAsync(CancellationToken cancellationToken)
        {
            string? text;
            try
            {
                text = await _storage.ReadTextAsync(StorageKey, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // A storage read failure leaves the data just as
                // unusable as corrupt content: degrade to the defaults
                // instead of crashing startup (see the class remarks).
                // Cancellation is neither exception type and still
                // propagates.
                return AppSettings.CreateDefault();
            }
            catch (UnauthorizedAccessException)
            {
                return AppSettings.CreateDefault();
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return AppSettings.CreateDefault();
            }

            SettingsDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize<SettingsDto>(text!, SerializerOptions);
            }
            catch (JsonException)
            {
                return AppSettings.CreateDefault();
            }
            catch (InvalidOperationException)
            {
                return AppSettings.CreateDefault();
            }
            catch (NotSupportedException)
            {
                return AppSettings.CreateDefault();
            }

            if (dto == null)
            {
                return AppSettings.CreateDefault();
            }

            // Any version other than the one this build writes —
            // including a higher, future version — is not ours to
            // interpret or overwrite.
            if (dto.Version != CurrentVersion)
            {
                return AppSettings.CreateDefault();
            }

            var settings = new AppSettings
            {
                GamesRootPath = dto.GamesRootPath,
                LogMinimumLevel = ParseLogLevel(dto.LogMinimumLevel),
                ScanMaxDepth = dto.ScanMaxDepth ?? AppSettings.DefaultScanMaxDepth,
                ExecutablePatterns = dto.ExecutablePatterns ?? new[] { "*.exe" },
            };

            try
            {
                settings.Validate();
            }
            catch (ArgumentException)
            {
                return AppSettings.CreateDefault();
            }

            return settings;
        }

        private static LogLevel ParseLogLevel(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return LogLevel.Information;
            }

            // Only an exact defined member name (case-insensitive) is
            // accepted. Enum.TryParse is deliberately NOT used here: it
            // also accepts numeric strings ("3") and comma-joined
            // composites ("Debug, Information"), which it ORs into a
            // different defined value — a corrupt stored level would
            // silently become a real but wrong level instead of
            // falling back for this field only, as documented.
            string trimmed = name!.Trim();
            foreach (LogLevel value in Enum.GetValues(typeof(LogLevel)))
            {
                if (string.Equals(value.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }

            return LogLevel.Information;
        }

        private static string Serialize(AppSettings settings)
        {
            var dto = new SettingsDto
            {
                Version = CurrentVersion,
                GamesRootPath = settings.GamesRootPath,
                LogMinimumLevel = settings.LogMinimumLevel.ToString(),
                ScanMaxDepth = settings.ScanMaxDepth,
                ExecutablePatterns = (string[])settings.ExecutablePatterns.Clone(),
            };

            return JsonSerializer.Serialize(dto, SerializerOptions);
        }

        /// <summary>
        /// Stored JSON shape. Mirrors <see cref="AppSettings"/> but is
        /// versioned and stores the log level by name.
        /// </summary>
        private sealed class SettingsDto
        {
            /// <summary>Stored format version.</summary>
            [JsonPropertyName("version")]
            public int Version { get; set; }

            /// <summary>Stored games root path, or <c>null</c>.</summary>
            [JsonPropertyName("gamesRootPath")]
            public string? GamesRootPath { get; set; }

            /// <summary>Stored log level name, for example <c>Warning</c>.</summary>
            [JsonPropertyName("logMinimumLevel")]
            public string? LogMinimumLevel { get; set; }

            /// <summary>Stored scan depth; <c>null</c> when absent.</summary>
            [JsonPropertyName("scanMaxDepth")]
            public int? ScanMaxDepth { get; set; }

            /// <summary>Stored executable patterns; <c>null</c> when absent.</summary>
            [JsonPropertyName("executablePatterns")]
            public string[]? ExecutablePatterns { get; set; }
        }
    }
}
