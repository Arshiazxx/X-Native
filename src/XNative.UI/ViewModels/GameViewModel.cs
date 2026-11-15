using System;
using XNative.Core.Devices;
using XNative.Core.Games;
using XNative.UI.Mvvm;

namespace XNative.UI.ViewModels
{
    /// <summary>
    /// View model representing a single game in the library.
    /// Metadata-backed properties (<see cref="Description"/>,
    /// <see cref="Publisher"/> and <see cref="Version"/>) are null-safe:
    /// when the game carries no metadata, or the individual metadata field
    /// is absent, they return <see cref="string.Empty"/> so views can bind
    /// to them without null checks.
    /// </summary>
    public sealed class GameViewModel : ObservableObject
    {
        private readonly GameInfo _gameInfo;

        /// <summary>
        /// Initializes a new instance of the <see cref="GameViewModel"/> class.
        /// </summary>
        /// <param name="gameInfo">The game information.</param>
        public GameViewModel(GameInfo gameInfo)
        {
            _gameInfo = gameInfo ?? throw new ArgumentNullException(nameof(gameInfo));
        }

        /// <summary>
        /// The underlying catalog entry this view model presents. Exposed so
        /// launch code can hand the exact entry back to a game runner.
        /// </summary>
        public GameInfo GameInfo => _gameInfo;

        /// <summary>
        /// Unique identifier of the game entry. Stable across rescans, so
        /// it can be used to preserve selection.
        /// </summary>
        public string Id => _gameInfo.Id;

        /// <summary>
        /// The game's display name.
        /// </summary>
        public string Name => _gameInfo.Name;

        /// <summary>
        /// The full path to the executable.
        /// </summary>
        public string ExecutablePath => _gameInfo.ExecutablePath;

        /// <summary>
        /// The working directory of the executable.
        /// </summary>
        public string WorkingDirectory => _gameInfo.WorkingDirectory;

        /// <summary>
        /// Default launch arguments recorded for the game, or <c>null</c>
        /// when the game defines none.
        /// </summary>
        public string? LaunchArguments => _gameInfo.LaunchArguments;

        /// <summary>
        /// Description of the game from its metadata, or
        /// <see cref="string.Empty"/> when no description is available.
        /// </summary>
        public string Description => _gameInfo.Metadata?.Description ?? string.Empty;

        /// <summary>
        /// Publisher of the game from its metadata, or
        /// <see cref="string.Empty"/> when no publisher is available.
        /// </summary>
        public string Publisher => _gameInfo.Metadata?.Publisher ?? string.Empty;

        /// <summary>
        /// Version of the game from its metadata, or
        /// <see cref="string.Empty"/> when no version is available.
        /// </summary>
        public string Version => _gameInfo.Metadata?.Version ?? string.Empty;

        /// <summary>
        /// Processor architecture of the executable rendered as short text:
        /// "x64", "x86", "ARM64", "ARM", or "Unknown" when it could not be
        /// determined.
        /// </summary>
        public string ArchitectureText => _gameInfo.Architecture switch
        {
            ProcessorArchitecture.X64 => "x64",
            ProcessorArchitecture.X86 => "x86",
            ProcessorArchitecture.Arm64 => "ARM64",
            ProcessorArchitecture.Arm => "ARM",
            _ => "Unknown",
        };

        /// <summary>
        /// The runner identifier (e.g., "wine", "uwp", "native").
        /// </summary>
        public string Runner => _gameInfo.ExecutableType switch
        {
            ExecutableType.WindowsExecutable => "wine",
            ExecutableType.UwpApplication => "uwp",
            _ => "unknown"
        };

        /// <summary>
        /// The executable type.
        /// </summary>
        public ExecutableType ExecutableType => _gameInfo.ExecutableType;

        /// <summary>
        /// Display text combining name and runner.
        /// </summary>
        public string DisplayText => $"{Name} ({Runner})";

        /// <summary>
        /// Detailed text for tooltips or expanded view.
        /// </summary>
        public string DetailText => $"Path: {ExecutablePath}\nRunner: {Runner}\nArchitecture: {ArchitectureText}";
    }
}
