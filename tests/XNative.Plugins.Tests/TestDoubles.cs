using System;
using System.Collections.Generic;
using XNative.Core.Abstractions;
using XNative.Core.Logging;

namespace XNative.Plugins.Tests
{
    internal sealed class RecordingLogService : ILogService
    {
        internal List<string> Errors { get; } = new List<string>();

        public LogLevel MinimumLevel => LogLevel.Trace;

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (level == LogLevel.Error)
            {
                Errors.Add(message);
            }
        }

        public bool IsEnabled(LogLevel level)
        {
            return level >= MinimumLevel;
        }
    }

    internal sealed class FakePlugin : IPlugin
    {
        private readonly Exception? _failure;

        internal FakePlugin(string id, Exception? failure = null)
        {
            Id = id;
            _failure = failure;
        }

        public string Id { get; }

        public string DisplayName => Id;

        internal int InitializeCount { get; private set; }

        internal IPluginHost? LastHost { get; private set; }

        public void Initialize(IPluginHost host)
        {
            InitializeCount++;
            LastHost = host;
            if (_failure != null)
            {
                throw _failure;
            }
        }
    }
}
