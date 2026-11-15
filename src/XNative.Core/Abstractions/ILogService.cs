using System;
using XNative.Core.Logging;

namespace XNative.Core.Abstractions
{
    /// <summary>
    /// Diagnostic sink for the whole application. Implemented in
    /// <c>XNative.Platform</c>. Implementations must be safe to call
    /// from any thread and must never throw: a logging failure must not
    /// take the application down with it.
    /// </summary>
    public interface ILogService
    {
        /// <summary>
        /// The lowest level this sink records. Entries below it are
        /// dropped without being written anywhere.
        /// </summary>
        LogLevel MinimumLevel { get; }

        /// <summary>
        /// Records one entry.
        /// </summary>
        /// <param name="level">Severity of the entry.</param>
        /// <param name="message">The message to record.</param>
        /// <param name="exception">The failure the entry describes, when there is one.</param>
        void Log(LogLevel level, string message, Exception? exception = null);

        /// <summary>
        /// Reports whether an entry at <paramref name="level"/> would be
        /// recorded, so callers can skip building expensive messages.
        /// </summary>
        /// <param name="level">The level to test.</param>
        /// <returns><c>true</c> when the level is at or above <see cref="MinimumLevel"/>.</returns>
        bool IsEnabled(LogLevel level);
    }
}
