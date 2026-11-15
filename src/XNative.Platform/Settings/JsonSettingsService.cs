using System;
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
    /// settings never crash or get clobbered by an older build.
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
        private bool _disposed;

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
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
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
                _gate.Release();
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

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _storage.WriteTextAsync(StorageKey, json, cancellationToken).ConfigureAwait(false);
                _cached = snapshot;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <inheritdoc />
        public async Task ResetAsync(CancellationToken cancellationToken = default)
        {
            var defaults = AppSettings.CreateDefault();
            string json = Serialize(defaults);

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _storage.WriteTextAsync(StorageKey, json, cancellationToken).ConfigureAwait(false);
                _cached = defaults;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Releases the synchronization resources held by the service.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the service and,
        /// when <paramref name="disposing"/> is <c>true</c>, its managed
        /// resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed
        /// and unmanaged resources; <c>false</c> to release only
        /// unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _gate.Dispose();
            }

            _disposed = true;
        }

        private async Task<AppSettings> LoadFromStorageAsync(CancellationToken cancellationToken)
        {
            string? text = await _storage.ReadTextAsync(StorageKey, cancellationToken).ConfigureAwait(false);

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

            // Unknown names fall back to the default for this field
            // only. Enum.TryParse also accepts numeric strings such as
            // "99", so the parsed value is checked for definedness as
            // well; numeric names are not part of the stored format.
            if (Enum.TryParse(name, ignoreCase: true, out LogLevel parsed)
                && Enum.IsDefined(typeof(LogLevel), parsed)
                && !IsNumericName(name!))
            {
                return parsed;
            }

            return LogLevel.Information;
        }

        private static bool IsNumericName(string name)
        {
            string trimmed = name.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            int start = (trimmed[0] == '-' || trimmed[0] == '+') ? 1 : 0;
            if (start >= trimmed.Length)
            {
                return false;
            }

            for (int i = start; i < trimmed.Length; i++)
            {
                if (trimmed[i] < '0' || trimmed[i] > '9')
                {
                    return false;
                }
            }

            return true;
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
