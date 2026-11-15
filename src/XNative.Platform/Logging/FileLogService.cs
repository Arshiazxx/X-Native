using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Options;
using XNative.Core.Abstractions;
using XNative.Core.Logging;

// ILogService's contract is "never throw", so this implementation must
// catch every exception from formatting and file IO by design.
#pragma warning disable CA1031 // Do not catch general exception types

namespace XNative.Platform.Logging
{
    /// <summary>
    /// Thread-safe <see cref="ILogService"/> that appends one formatted
    /// line per entry to a file: UTC timestamp, level, message and, when
    /// present, the exception. It honours
    /// <see cref="FileLogServiceOptions.MinimumLevel"/> and never throws:
    /// when file IO fails the formatted entry is kept in a bounded
    /// in-memory ring buffer exposed through <see cref="BufferedEntries"/>
    /// so diagnostics survive a broken log directory.
    /// </summary>
    public sealed class FileLogService : ILogService
    {
        private readonly FileLogServiceOptions _options;
        private readonly object _lock = new object();
        private readonly Queue<string> _buffer = new Queue<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="FileLogService"/> class.
        /// </summary>
        /// <param name="options">Log configuration options.</param>
        /// <exception cref="ArgumentException">Thrown when
        /// <see cref="FileLogServiceOptions.FileName"/> is not a plain
        /// file name.</exception>
        public FileLogService(IOptions<FileLogServiceOptions> options)
        {
            _options = options != null && options.Value != null
                ? options.Value
                : throw new ArgumentNullException(nameof(options));
            ValidateFileName(_options.FileName);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileLogService"/> class
        /// from plain options, for call sites without a DI container.
        /// </summary>
        /// <param name="options">Log configuration options.</param>
        /// <exception cref="ArgumentException">Thrown when
        /// <see cref="FileLogServiceOptions.FileName"/> is not a plain
        /// file name.</exception>
        public FileLogService(FileLogServiceOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            ValidateFileName(_options.FileName);
        }

        /// <inheritdoc />
        public LogLevel MinimumLevel
        {
            get { return _options.MinimumLevel; }
        }

        /// <summary>
        /// Formatted entries that could not be written to the log file,
        /// oldest first, capped at
        /// <see cref="FileLogServiceOptions.BufferCapacity"/>. Empty when
        /// file IO is healthy.
        /// </summary>
        public IReadOnlyList<string> BufferedEntries
        {
            get
            {
                lock (_lock)
                {
                    return new List<string>(_buffer);
                }
            }
        }

        /// <summary>
        /// Full path of the log file this service writes to.
        /// </summary>
        public string LogFilePath
        {
            get
            {
                string fileName = string.IsNullOrWhiteSpace(_options.FileName)
                    ? "xnative.log"
                    : _options.FileName;
                return string.IsNullOrWhiteSpace(_options.LogDirectory)
                    ? fileName
                    : Path.Combine(_options.LogDirectory, fileName);
            }
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel level)
        {
            return level >= _options.MinimumLevel;
        }

        /// <inheritdoc />
        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (!IsEnabled(level))
            {
                return;
            }

            string line;
            try
            {
                line = FormatLine(level, message, exception);
            }
            catch (Exception)
            {
                // Formatting must never take the caller down either.
                return;
            }

            lock (_lock)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(_options.LogDirectory))
                    {
                        Directory.CreateDirectory(_options.LogDirectory);
                    }

                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
                catch (Exception)
                {
                    Buffer(line);
                }
            }
        }

        private static void ValidateFileName(string? fileName)
        {
            // Null or whitespace keeps the documented fallback
            // ("xnative.log", see LogFilePath); anything else must be
            // a plain file name, held to the same rules the storage
            // service applies to its keys, so the log file can never
            // escape the configured log directory.
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            string name = fileName!;
            if (Path.IsPathRooted(name)
                || name.IndexOf('/') >= 0
                || name.IndexOf('\\') >= 0
                || name.IndexOf("..", StringComparison.Ordinal) >= 0
                || !string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "FileLogServiceOptions.FileName must be a plain file name "
                    + "without directory separators, a rooted path, or '..'.",
                    nameof(FileLogServiceOptions.FileName));
            }
        }

        private static string FormatLine(LogLevel level, string message, Exception? exception)
        {
            string timestamp = DateTime.UtcNow.ToString(
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture);
            string text = message ?? string.Empty;
            if (exception == null)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} [{1}] {2}",
                    timestamp,
                    level,
                    text);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} [{1}] {2} | {3}: {4}",
                timestamp,
                level,
                text,
                exception.GetType().FullName,
                exception.Message);
        }

        private void Buffer(string line)
        {
            int capacity = _options.BufferCapacity > 0 ? _options.BufferCapacity : 100;
            while (_buffer.Count >= capacity)
            {
                _buffer.Dequeue();
            }

            _buffer.Enqueue(line);
        }
    }
}
