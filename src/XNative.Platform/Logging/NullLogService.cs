using System;
using XNative.Core.Abstractions;
using XNative.Core.Logging;

namespace XNative.Platform.Logging
{
    /// <summary>
    /// An <see cref="ILogService"/> that discards every entry. Useful as
    /// a safe default and in tests.
    /// </summary>
    public sealed class NullLogService : ILogService
    {
        /// <summary>
        /// A shared instance; the service is stateless.
        /// </summary>
        public static NullLogService Instance { get; } = new NullLogService();

        /// <inheritdoc />
        public LogLevel MinimumLevel
        {
            get { return LogLevel.Trace; }
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel level)
        {
            return false;
        }

        /// <inheritdoc />
        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            // Intentionally discards everything; never throws.
        }
    }
}
