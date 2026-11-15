using System;
using XNative.Core.Abstractions;

namespace XNative.Core.Logging
{
    /// <summary>
    /// Convenience wrappers over <see cref="ILogService.Log"/> so call
    /// sites read as <c>log.Warning("...")</c> instead of repeating the
    /// level. These add no behaviour beyond forwarding.
    /// </summary>
    public static class LogServiceExtensions
    {
        /// <summary>
        /// Records a <see cref="LogLevel.Trace"/> entry.
        /// </summary>
        /// <param name="log">The sink to write to.</param>
        /// <param name="message">The message to record.</param>
        public static void Trace(this ILogService log, string message)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            log.Log(LogLevel.Trace, message);
        }

        /// <summary>
        /// Records a <see cref="LogLevel.Debug"/> entry.
        /// </summary>
        /// <param name="log">The sink to write to.</param>
        /// <param name="message">The message to record.</param>
        public static void Debug(this ILogService log, string message)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            log.Log(LogLevel.Debug, message);
        }

        /// <summary>
        /// Records a <see cref="LogLevel.Information"/> entry.
        /// </summary>
        /// <param name="log">The sink to write to.</param>
        /// <param name="message">The message to record.</param>
        public static void Information(this ILogService log, string message)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            log.Log(LogLevel.Information, message);
        }

        /// <summary>
        /// Records a <see cref="LogLevel.Warning"/> entry.
        /// </summary>
        /// <param name="log">The sink to write to.</param>
        /// <param name="message">The message to record.</param>
        /// <param name="exception">The failure the entry describes, when there is one.</param>
        public static void Warning(this ILogService log, string message, Exception? exception = null)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            log.Log(LogLevel.Warning, message, exception);
        }

        /// <summary>
        /// Records a <see cref="LogLevel.Error"/> entry.
        /// </summary>
        /// <param name="log">The sink to write to.</param>
        /// <param name="message">The message to record.</param>
        /// <param name="exception">The failure the entry describes, when there is one.</param>
        public static void Error(this ILogService log, string message, Exception? exception = null)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            log.Log(LogLevel.Error, message, exception);
        }
    }
}
