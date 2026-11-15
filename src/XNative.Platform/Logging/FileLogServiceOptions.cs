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
    }
}
