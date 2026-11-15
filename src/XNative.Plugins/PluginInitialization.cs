using System;
using System.Collections.Generic;

namespace XNative.Plugins
{
    /// <summary>
    /// Outcome of one plugin's initialization attempt.
    /// </summary>
    public enum PluginInitializationStatus
    {
        /// <summary>
        /// The plugin's <see cref="IPlugin.Initialize"/> returned normally.
        /// </summary>
        Succeeded = 0,

        /// <summary>
        /// The plugin's <see cref="IPlugin.Initialize"/> threw.
        /// </summary>
        Failed = 1,

        /// <summary>
        /// The plugin was not called because this manager already
        /// attempted this same plugin instance (in this or an earlier
        /// <see cref="PluginManager.InitializeAll"/> call). A different
        /// instance claiming an already-attempted ID is
        /// <see cref="Failed"/> instead: it was never initialized and
        /// does not own the earlier instance's outcome.
        /// </summary>
        AlreadyInitialized = 2,
    }

    /// <summary>
    /// Per-plugin entry of a <see cref="PluginInitializationReport"/>.
    /// </summary>
    public sealed class PluginInitializationResult
    {
        /// <summary>
        /// Creates the result entry.
        /// </summary>
        /// <param name="pluginId">The plugin's <see cref="IPlugin.Id"/>.</param>
        /// <param name="status">What happened.</param>
        /// <param name="error">The exception a failed plugin threw; otherwise <c>null</c>.</param>
        public PluginInitializationResult(string pluginId, PluginInitializationStatus status, Exception? error)
        {
            PluginId = pluginId;
            Status = status;
            Error = error;
        }

        /// <summary>
        /// The plugin's <see cref="IPlugin.Id"/>.
        /// </summary>
        public string PluginId { get; }

        /// <summary>
        /// What happened when the manager processed this plugin.
        /// </summary>
        public PluginInitializationStatus Status { get; }

        /// <summary>
        /// The exception the plugin threw, when <see cref="Status"/> is
        /// <see cref="PluginInitializationStatus.Failed"/>; otherwise <c>null</c>.
        /// </summary>
        public Exception? Error { get; }

        /// <summary>
        /// Whether the plugin initialized successfully.
        /// </summary>
        public bool Succeeded => Status == PluginInitializationStatus.Succeeded;
    }

    /// <summary>
    /// Result of one <see cref="PluginManager.InitializeAll"/> call: one
    /// entry per catalog plugin, in catalog order.
    /// </summary>
    public sealed class PluginInitializationReport
    {
        /// <summary>
        /// Creates the report.
        /// </summary>
        /// <param name="results">The per-plugin entries.</param>
        /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
        public PluginInitializationReport(IReadOnlyList<PluginInitializationResult> results)
        {
            Results = results ?? throw new ArgumentNullException(nameof(results));
        }

        /// <summary>
        /// One entry per plugin, in catalog order.
        /// </summary>
        public IReadOnlyList<PluginInitializationResult> Results { get; }

        /// <summary>
        /// How many plugins initialized successfully in this call.
        /// </summary>
        public int SucceededCount
        {
            get
            {
                int count = 0;
                foreach (PluginInitializationResult result in Results)
                {
                    if (result.Status == PluginInitializationStatus.Succeeded)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// How many plugins threw in this call.
        /// </summary>
        public int FailedCount
        {
            get
            {
                int count = 0;
                foreach (PluginInitializationResult result in Results)
                {
                    if (result.Status == PluginInitializationStatus.Failed)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Whether every plugin succeeded in this call (vacuously true
        /// for an empty catalog; already-initialized entries do not make
        /// it false, they mean an earlier call owns their outcome).
        /// </summary>
        public bool AllSucceeded => FailedCount == 0;
    }
}
