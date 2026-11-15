using System.Collections.Generic;

namespace XNative.Plugins
{
    /// <summary>
    /// Source of the plugins available to the application. Plugin
    /// discovery is not yet implemented: no component scans the file
    /// system or loads plugin assemblies automatically, so catalogues
    /// are populated in code, or by the host application, with the
    /// plugins it wants to expose.
    /// </summary>
    public interface IPluginCatalog
    {
        /// <summary>
        /// Returns the plugins currently known to the catalog.
        /// </summary>
        /// <returns>The known plugins.</returns>
        IReadOnlyCollection<IPlugin> GetPlugins();
    }
}
