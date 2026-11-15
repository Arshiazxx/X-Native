using System;
using System.Collections.Generic;

namespace XNative.Plugins
{
    /// <summary>
    /// <see cref="IPluginCatalog"/> backed by a fixed, in-code list of
    /// plugin instances. This is the only catalog shape that works on
    /// every target: on Xbox / UWP the sandbox cannot load arbitrary
    /// plugin assemblies from a folder at runtime, so plugins ship
    /// in-package and are registered in code at composition time.
    /// </summary>
    /// <remarks>
    /// Plugin IDs must be unique: two plugins sharing an ID are
    /// rejected at construction time instead of silently shadowing each
    /// other. IDs are compared case-insensitively, matching how
    /// <see cref="PluginManager"/> identifies the plugins it has
    /// already attempted.
    /// </remarks>
    public sealed class InMemoryPluginCatalog : IPluginCatalog
    {
        private readonly List<IPlugin> _plugins;

        /// <summary>
        /// Creates an empty catalog.
        /// </summary>
        public InMemoryPluginCatalog()
            : this(null)
        {
        }

        /// <summary>
        /// Creates a catalog holding a copy of <paramref name="plugins"/>.
        /// Later changes to the source sequence do not affect the catalog.
        /// </summary>
        /// <param name="plugins">The plugins to hold; <c>null</c> means an empty catalog.</param>
        /// <exception cref="ArgumentException">
        /// The sequence contains a <c>null</c> plugin, a plugin whose
        /// <see cref="IPlugin.Id"/> is <c>null</c>, empty or whitespace,
        /// or two plugins sharing the same <see cref="IPlugin.Id"/>
        /// (compared case-insensitively).
        /// </exception>
        public InMemoryPluginCatalog(IEnumerable<IPlugin>? plugins)
        {
            _plugins = new List<IPlugin>();
            if (plugins == null)
            {
                return;
            }

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IPlugin plugin in plugins)
            {
                if (plugin == null)
                {
                    throw new ArgumentException("The plugin sequence must not contain null entries.", nameof(plugins));
                }

                if (string.IsNullOrWhiteSpace(plugin.Id))
                {
                    throw new ArgumentException("A plugin must have a non-empty Id.", nameof(plugins));
                }

                if (!seenIds.Add(plugin.Id))
                {
                    throw new ArgumentException(
                        "Duplicate plugin ID '" + plugin.Id + "'. Plugin IDs must be unique " +
                        "(compared case-insensitively); a duplicate would silently shadow the earlier plugin.",
                        nameof(plugins));
                }

                _plugins.Add(plugin);
            }
        }

        /// <inheritdoc />
        public IReadOnlyCollection<IPlugin> GetPlugins()
        {
            // Snapshot copy: callers can enumerate or even cast and mutate
            // the returned list without changing the catalog.
            return new List<IPlugin>(_plugins);
        }
    }
}
