using System;
using XNative.Core.Devices;

namespace XNative.Core.Games
{
    /// <summary>
    /// Represents an installed application that can be launched.
    /// </summary>
    public sealed class GameInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GameInfo"/> class.
        /// </summary>
        /// <param name="id">Unique identifier for this game entry.</param>
        /// <param name="name">Display name of the game.</param>
        /// <param name="executablePath">Full path to the executable file.</param>
        /// <param name="workingDirectory">Working directory for the executable.</param>
        /// <param name="executableType">Type of the executable.</param>
        /// <param name="metadata">Optional metadata about the game.</param>
        /// <param name="architecture">Processor architecture of the executable, when known.</param>
        /// <param name="launchArguments">Default arguments recorded for this game, for example from its manifest.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        /// <exception cref="ArgumentException">Thrown when required parameters are empty.</exception>
        public GameInfo(
            string id,
            string name,
            string executablePath,
            string workingDirectory,
            ExecutableType executableType,
            GameMetadata? metadata = null,
            ProcessorArchitecture architecture = ProcessorArchitecture.Unknown,
            string? launchArguments = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ExecutablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
            WorkingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
            ExecutableType = executableType;
            Metadata = metadata;
            Architecture = architecture;
            LaunchArguments = launchArguments;

            if (string.IsNullOrWhiteSpace(Id))
                throw new ArgumentException("Id cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("Name cannot be empty.", nameof(name));
            if (string.IsNullOrWhiteSpace(ExecutablePath))
                throw new ArgumentException("ExecutablePath cannot be empty.", nameof(executablePath));
            if (string.IsNullOrWhiteSpace(WorkingDirectory))
                throw new ArgumentException("WorkingDirectory cannot be empty.", nameof(workingDirectory));
        }

        /// <summary>
        /// Unique identifier for this game entry.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Display name of the game.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Full path to the executable file.
        /// </summary>
        public string ExecutablePath { get; }

        /// <summary>
        /// Working directory for the executable.
        /// </summary>
        public string WorkingDirectory { get; }

        /// <summary>
        /// Type of the executable.
        /// </summary>
        public ExecutableType ExecutableType { get; }

        /// <summary>
        /// Optional metadata about the game.
        /// </summary>
        public GameMetadata? Metadata { get; }

        /// <summary>
        /// Processor architecture of the executable, when known.
        /// </summary>
        public ProcessorArchitecture Architecture { get; }

        /// <summary>
        /// Default arguments recorded for this game, for example from
        /// its manifest, or <c>null</c> for none.
        /// </summary>
        public string? LaunchArguments { get; }
    }
}
