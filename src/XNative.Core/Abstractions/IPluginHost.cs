namespace XNative.Core.Abstractions
{
    /// <summary>
    /// Environment handed to a plugin by the application. Implemented in
    /// <c>XNative.Plugins</c>. The host deliberately exposes only the
    /// small set of services a plugin may rely on, so a plugin can never
    /// reach around the module boundaries.
    /// </summary>
    public interface IPluginHost
    {
        /// <summary>
        /// The application log sink, shared with every module.
        /// </summary>
        ILogService Log { get; }

        /// <summary>
        /// Directory the plugins were loaded from, so a plugin can find
        /// its own companion files.
        /// </summary>
        string PluginDirectory { get; }
    }
}
