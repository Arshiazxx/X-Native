using System.Collections.Generic;

namespace XNative.Core.Games
{
    /// <summary>
    /// Optional, per-launch settings that modify how a game is started
    /// without changing the game entry itself. Every member is optional;
    /// a default instance means "launch exactly as catalogued".
    /// </summary>
    public sealed class LaunchOptions
    {
        /// <summary>
        /// A shared instance carrying no overrides. Callers must not
        /// mutate it; create a new instance to customise a launch.
        /// </summary>
        public static LaunchOptions Default { get; } = new LaunchOptions();

        /// <summary>
        /// Command line arguments passed to the executable, already split
        /// and quoted exactly as they should reach the process, or
        /// <c>null</c> to pass no arguments.
        /// </summary>
        public string? Arguments { get; set; }

        /// <summary>
        /// Working directory to use instead of the game's catalogued
        /// working directory, or <c>null</c> to use the catalogued one.
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// Extra environment variables for the launched process. May be
        /// empty, never needs to be <c>null</c>-checked by consumers that
        /// only read it.
        /// </summary>
        public IDictionary<string, string> EnvironmentVariables { get; set; }
            = new Dictionary<string, string>();

        /// <summary>
        /// Identifier of the compatibility profile to apply, or
        /// <c>null</c> to let the runner choose a profile for the game.
        /// </summary>
        public string? ProfileId { get; set; }
    }
}
