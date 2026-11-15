using System;
using System.Collections.Generic;
using XNative.Core.Abstractions;
using XNative.Core.Logging;

namespace XNative.Wine.Tests.Fakes
{
    public sealed class FakeLogService : ILogService
    {
        public sealed class Entry
        {
            public Entry(LogLevel level, string message, Exception? exception)
            {
                Level = level;
                Message = message;
                Exception = exception;
            }

            public LogLevel Level { get; }

            public string Message { get; }

            public Exception? Exception { get; }
        }

        public List<Entry> Entries { get; } = new List<Entry>();

        public LogLevel MinimumLevel => LogLevel.Trace;

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            Entries.Add(new Entry(level, message, exception));
        }

        public bool IsEnabled(LogLevel level)
        {
            return level >= MinimumLevel;
        }

        public bool Contains(string substring)
        {
            foreach (Entry entry in Entries)
            {
                if (entry.Message.IndexOf(substring, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
