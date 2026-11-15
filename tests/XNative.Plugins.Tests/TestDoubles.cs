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

    internal sealed class ThrowingLogService : ILogService
    {
        public LogLevel MinimumLevel => LogLevel.Trace;

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            throw new InvalidOperationException("The log sink is broken.");
        }

        public bool IsEnabled(LogLevel level)
        {
            return true;
        }
    }

    /// <summary>
    /// An <see cref="IPluginCatalog"/> that hands its plugins to the
    /// manager verbatim, with none of
    /// <see cref="InMemoryPluginCatalog"/>'s construction-time
    /// validation — it stands in for a custom catalog that skips
    /// validation, so manager-side behaviour can be tested against
    /// duplicate IDs, unusable IDs and null entries.
    /// </summary>
    internal sealed class StubPluginCatalog : IPluginCatalog
    {
        private readonly List<IPlugin> _plugins;

        internal StubPluginCatalog(IEnumerable<IPlugin> plugins)
        {
            _plugins = new List<IPlugin>(plugins);
        }

        public IReadOnlyCollection<IPlugin> GetPlugins()
        {
            return new List<IPlugin>(_plugins);
        }
    }

    internal sealed class ThrowingIdPlugin : IPlugin
    {
        private readonly Exception _idFailure;

        internal ThrowingIdPlugin(Exception idFailure)
        {
            _idFailure = idFailure;
        }

        public string Id => throw _idFailure;

        public string DisplayName => "Throwing-Id Plugin";

        internal int InitializeCount { get; private set; }

        public void Initialize(IPluginHost host)
        {
            InitializeCount++;
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
