using System;

namespace XNative.Core.Processes
{
    /// <summary>
    /// A snapshot of one process started through
    /// <see cref="Abstractions.IProcessService"/>.
    /// </summary>
    public sealed class RunningProcess
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RunningProcess"/> class.
        /// </summary>
        /// <param name="id">Operating system process identifier.</param>
        /// <param name="fileName">Path of the executable the process was started from.</param>
        /// <param name="startTimeUtc">When the process started, in UTC.</param>
        /// <param name="hasExited">Whether the process has already exited.</param>
        /// <param name="exitCode">The exit code when the process has exited, otherwise <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileName"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is empty.</exception>
        public RunningProcess(int id, string fileName, DateTime startTimeUtc, bool hasExited, int? exitCode)
        {
            Id = id;
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            StartTimeUtc = startTimeUtc;
            HasExited = hasExited;
            ExitCode = exitCode;

            if (string.IsNullOrWhiteSpace(FileName))
                throw new ArgumentException("FileName cannot be empty.", nameof(fileName));
        }

        /// <summary>
        /// Operating system process identifier.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Path of the executable the process was started from.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// When the process started, in UTC.
        /// </summary>
        public DateTime StartTimeUtc { get; }

        /// <summary>
        /// Whether the process has already exited.
        /// </summary>
        public bool HasExited { get; }

        /// <summary>
        /// The exit code when the process has exited, otherwise <c>null</c>.
        /// </summary>
        public int? ExitCode { get; }
    }
}
