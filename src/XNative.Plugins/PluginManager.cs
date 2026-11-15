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
    /// Exactly-once semantics: a manager instance attempts each plugin
    /// (identified by <see cref="IPlugin.Id"/>, compared
    /// case-insensitively) at most once for its whole lifetime, whether
    /// the attempt succeeded or failed. A second
    /// <see cref="InitializeAll"/> call does not re-call
    /// <see cref="IPlugin.Initialize"/>; it reports those plugins as
    /// <see cref="PluginInitializationStatus.AlreadyInitialized"/>.
    /// Retrying a failed plugin means creating a new manager. This type
    /// is not thread-safe; call it once from startup code.
    /// </remarks>
    public sealed class PluginManager
    {
        private readonly IPluginCatalog _catalog;
        private readonly HashSet<string> _attemptedPluginIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                string pluginId = plugin.Id ?? string.Empty;
                if (!_attemptedPluginIds.Add(pluginId))
                {
                    results.Add(new PluginInitializationResult(
                        pluginId, PluginInitializationStatus.AlreadyInitialized, null));
                    continue;
                }

                try
                {
                    plugin.Initialize(host);
                    results.Add(new PluginInitializationResult(
                        pluginId, PluginInitializationStatus.Succeeded, null));
                }
                catch (Exception exception)
                {
                    // Isolation is the point: log, record, keep going.
                    host.Log.Error(
                        "Plugin '" + pluginId + "' failed to initialize.", exception);
                    results.Add(new PluginInitializationResult(
                        pluginId, PluginInitializationStatus.Failed, exception));
                }
            }

            return new PluginInitializationReport(results);
        }
    }
}
