using System;
using System.Collections.Generic;
using XNative.Core.Abstractions;
using XNative.Core.Logging;

namespace XNative.Plugins
{
    /// <summary>
    /// Initializes the plugins of an <see cref="IPluginCatalog"/> against
    /// an <see cref="IPluginHost"/>, isolating failures: one plugin
    /// throwing never stops the others, the failure is logged through
    /// <see cref="IPluginHost.Log"/> and collected into the returned
    /// <see cref="PluginInitializationReport"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Exactly-once semantics: a manager instance attempts each plugin
    /// (identified by <see cref="IPlugin.Id"/>, compared
    /// case-insensitively) at most once for its whole lifetime, whether
    /// the attempt succeeded or failed. A second
    /// <see cref="InitializeAll"/> call does not re-call
    /// <see cref="IPlugin.Initialize"/>; it reports those plugins as
    /// <see cref="PluginInitializationStatus.AlreadyInitialized"/>.
    /// Retrying a failed plugin means creating a new manager. This type
    /// is not thread-safe; call it once from startup code.
    /// </para>
    /// <para>
    /// Identity is per instance as well as per ID: when a
    /// <em>different</em> plugin instance claims an ID this manager has
    /// already attempted, that instance can never run under this
    /// manager, so it is reported as
    /// <see cref="PluginInitializationStatus.Failed"/> with an
    /// <see cref="InvalidOperationException"/> — never as
    /// <see cref="PluginInitializationStatus.AlreadyInitialized"/>,
    /// which is reserved for the same instance being seen again.
    /// (<see cref="InMemoryPluginCatalog"/> rejects duplicate IDs up
    /// front; this guard keeps the report honest for custom
    /// <see cref="IPluginCatalog"/> implementations that do not.)
    /// </para>
    /// <para>
    /// The isolation boundary covers the plugin's identity too: if
    /// reading <see cref="IPlugin.Id"/> itself throws, or the ID is
    /// <c>null</c>, empty or whitespace, the plugin is recorded as
    /// <see cref="PluginInitializationStatus.Failed"/> under its type
    /// name and is never initialized. Such a plugin has no usable
    /// identity to track, so a later <see cref="InitializeAll"/> call
    /// reports it as failed again rather than as already initialized.
    /// </para>
    /// <para>
    /// Logging is best-effort: if the host's <see cref="ILogService"/>
    /// throws while a failure is being logged, the exception is
    /// swallowed — the report entry already carries the original
    /// failure, and a broken log sink must never change the outcome.
    /// </para>
    /// </remarks>
    public sealed class PluginManager
    {
        private readonly IPluginCatalog _catalog;
        private readonly Dictionary<string, IPlugin> _attemptedPlugins =
            new Dictionary<string, IPlugin>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a manager over <paramref name="catalog"/>.
        /// </summary>
        /// <param name="catalog">The source of plugins to initialize.</param>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is <c>null</c>.</exception>
        public PluginManager(IPluginCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>
        /// Initializes every catalog plugin that this manager has not
        /// attempted yet, in catalog order.
        /// </summary>
        /// <param name="host">The environment handed to each plugin.</param>
        /// <returns>A report with one entry per catalog plugin.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="host"/> is <c>null</c>.</exception>
        public PluginInitializationReport InitializeAll(IPluginHost host)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            var results = new List<PluginInitializationResult>();
            foreach (IPlugin plugin in _catalog.GetPlugins())
            {
                results.Add(InitializePlugin(host, plugin));
            }

            return new PluginInitializationReport(results);
        }

        private PluginInitializationResult InitializePlugin(IPluginHost host, IPlugin plugin)
        {
            // The Id read is inside the isolation boundary: a plugin
            // whose identity cannot even be read is a failed plugin,
            // not a reason to abort the whole run without a report.
            string pluginId;
            try
            {
                pluginId = plugin.Id;
            }
            catch (Exception exception)
            {
                string fallbackId = DescribePlugin(plugin);
                LogFailure(host, fallbackId, exception);
                return new PluginInitializationResult(
                    fallbackId, PluginInitializationStatus.Failed, exception);
            }

            if (string.IsNullOrWhiteSpace(pluginId))
            {
                string fallbackId = DescribePlugin(plugin);
                var exception = new ArgumentException(
                    "The plugin of type '" + fallbackId + "' has no usable Id " +
                    "(null, empty or whitespace). Plugins are identified by Id, so this " +
                    "instance cannot be initialized or tracked.");
                LogFailure(host, fallbackId, exception);
                return new PluginInitializationResult(
                    fallbackId, PluginInitializationStatus.Failed, exception);
            }

            if (_attemptedPlugins.TryGetValue(pluginId, out IPlugin? attemptedPlugin))
            {
                if (ReferenceEquals(attemptedPlugin, plugin))
                {
                    return new PluginInitializationResult(
                        pluginId, PluginInitializationStatus.AlreadyInitialized, null);
                }

                // A different instance claiming an attempted ID can
                // never run under this manager. Reporting it as
                // AlreadyInitialized would claim an outcome it does not
                // own, so it is a failure instead.
                var duplicateException = new InvalidOperationException(
                    "A different plugin instance with Id '" + pluginId + "' was already " +
                    "attempted by this manager. This instance was not initialized; " +
                    "plugin Ids must be unique in the catalog.");
                LogFailure(host, pluginId, duplicateException);
                return new PluginInitializationResult(
                    pluginId, PluginInitializationStatus.Failed, duplicateException);
            }

            _attemptedPlugins.Add(pluginId, plugin);

            try
            {
                plugin.Initialize(host);
                return new PluginInitializationResult(
                    pluginId, PluginInitializationStatus.Succeeded, null);
            }
            catch (Exception exception)
            {
                // Isolation is the point: log, record, keep going.
                LogFailure(host, pluginId, exception);
                return new PluginInitializationResult(
                    pluginId, PluginInitializationStatus.Failed, exception);
            }
        }

        private static void LogFailure(IPluginHost host, string pluginId, Exception exception)
        {
            try
            {
                host.Log.Error(
                    "Plugin '" + pluginId + "' failed to initialize.", exception);
            }
            catch (Exception)
            {
                // Swallowed by design: the report entry already carries
                // the original exception, and a throwing log sink must
                // never abort initialization or change its outcome.
            }
        }

        private static string DescribePlugin(IPlugin? plugin)
        {
            if (plugin == null)
            {
                return "<null plugin>";
            }

            Type type = plugin.GetType();
            return type.FullName ?? type.Name;
        }
    }
}
