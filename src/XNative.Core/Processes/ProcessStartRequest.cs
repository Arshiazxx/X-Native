using System;
using System.Collections.Generic;

namespace XNative.Core.Processes
{
    /// <summary>
    /// Everything needed to start one external process, expressed
    /// without any platform type so the core stays portable.
    /// </summary>
    public sealed class ProcessStartRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessStartRequest"/> class.
        /// </summary>
        /// <param name="fileName">Path of the executable to start.</param>
        /// <param name="arguments">Command line arguments, or <c>null</c> for none.</param>
        /// <param name="workingDirectory">Working directory for the process, or <c>null</c> to inherit.</param>
        public ProcessStartRequest(string fileName, string? arguments = null, string? workingDirectory = null)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            Arguments = arguments;
            WorkingDirectory = workingDirectory;

            if (string.IsNullOrWhiteSpace(FileName))
                throw new ArgumentException("FileName cannot be empty.", nameof(fileName));
        }

        /// <summary>
        /// Path of the executable to start.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// Command line arguments, or <c>null</c> for none.
        /// </summary>
        public string? Arguments { get; }

        /// <summary>
        /// Working directory for the process, or <c>null</c> to inherit.
        /// </summary>
        public string? WorkingDirectory { get; }

        /// <summary>
        /// Extra environment variables for the new process.
        /// </summary>
        public IDictionary<string, string> EnvironmentVariables { get; set; }
            = new Dictionary<string, string>();
    }
}
