using XNative.Core.Logging;

namespace XNative.Platform.Logging
{
    /// <summary>
    /// Configuration options for <see cref="FileLogService"/>.
    /// Mirrors the options pattern used by
    /// <c>LocalGameCatalogOptions</c>.
    /// </summary>
    public sealed class FileLogServiceOptions
    {
        /// <summary>
        /// Directory the log file lives in. Created on first write when
        /// possible; when it cannot be created the service buffers
        /// entries in memory instead of throwing.
        /// </summary>
        public string LogDirectory { get; set; } = string.Empty;

        /// <summary>
        /// File name (not a full path) of the log file inside
        /// <see cref="LogDirectory"/>. Must be a plain file name:
        /// <see cref="FileLogService"/> rejects a name containing
        /// directory separators, a rooted path, or <c>..</c> at
        /// construction time, so the log file cannot escape
        /// <see cref="LogDirectory"/>.
        /// </summary>
        public string FileName { get; set; } = "xnative.log";

        /// <summary>
        /// Lowest level that is recorded. Entries below it are dropped.
        /// </summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Maximum number of formatted entries kept in the in-memory
        /// fallback buffer used when file IO fails.
        /// </summary>
        public int BufferCapacity { get; set; } = 100;

        /// <summary>
        /// Maximum size of the active log file, in bytes, before it
        /// is rolled over (default 5 MiB). When a write finds the
        /// active file at or over this size, the file is first
        /// renamed to a single backup — <c>name.1.log</c> for
        /// <c>name.log</c> — replacing any previous backup, and the
        /// entry starts a fresh file, so on-disk log data stays
        /// bounded at roughly twice this size. Rotation is
        /// best-effort: a failed roll never loses the entry being
        /// logged and never throws. A value of zero or less disables
        /// rotation entirely.
        /// </summary>
        public long MaximumFileSizeBytes { get; set; } = 5 * 1024 * 1024;
    }
}
