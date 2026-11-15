using System;
using XNative.Core.Abstractions;

namespace XNative.Plugins
{
    /// <summary>
    /// Default <see cref="IPluginHost"/>: hands a plugin the shared log
    /// sink and the directory plugins conceptually live in, and nothing
    /// else, so plugins cannot reach around the module boundaries.
    /// </summary>
    /// <remarks>
    /// Honesty note on loading: this host does not load assemblies. On
    /// Xbox / UWP the sandbox blocks loading arbitrary plugin assemblies
    /// from disk at runtime, so X-Native plugins are in-process only:
    /// they ship inside the app package (or are registered in code at
    /// composition time) and are handed to an
    /// <see cref="IPluginCatalog"/> implementation such as
    /// <see cref="InMemoryPluginCatalog"/>. <see cref="PluginDirectory"/>
    /// therefore names where a plugin's companion files live, not a
    /// folder that gets scanned for DLLs.
    /// </remarks>
    public sealed class PluginHost : IPluginHost
    {
        /// <summary>
        /// Creates the host.
        /// </summary>
        /// <param name="log">The shared application log sink.</param>
        /// <param name="pluginDirectory">Directory a plugin can find its companion files in.</param>
        /// <exception cref="ArgumentNullException"><paramref name="log"/> or <paramref name="pluginDirectory"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="pluginDirectory"/> is empty or whitespace.</exception>
        public PluginHost(ILogService log, string pluginDirectory)
        {
            Log = log ?? throw new ArgumentNullException(nameof(log));
            if (pluginDirectory == null)
            {
                throw new ArgumentNullException(nameof(pluginDirectory));
            }

            if (string.IsNullOrWhiteSpace(pluginDirectory))
            {
                throw new ArgumentException("The plugin directory must not be empty.", nameof(pluginDirectory));
            }

            PluginDirectory = pluginDirectory;
        }

        /// <inheritdoc />
        public ILogService Log { get; }

        /// <inheritdoc />
        public string PluginDirectory { get; }
    }
}
