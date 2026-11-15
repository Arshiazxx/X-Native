namespace XNative.Core.Logging
{
    /// <summary>
    /// Severity of a log entry, in increasing order of importance.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Verbose diagnostic detail, only useful while developing.
        /// </summary>
        Trace = 0,

        /// <summary>
        /// Diagnostic detail useful when investigating a problem.
        /// </summary>
        Debug = 1,

        /// <summary>
        /// A normal, noteworthy event in the life of the application.
        /// </summary>
        Information = 2,

        /// <summary>
        /// Something unexpected happened, but the application recovered.
        /// </summary>
        Warning = 3,

        /// <summary>
        /// An operation failed.
        /// </summary>
        Error = 4,

        /// <summary>
        /// A failure the application cannot recover from.
        /// </summary>
        Critical = 5,
    }
}
