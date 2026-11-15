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
        /// An instance carrying no overrides. A new instance is created
        /// on every access, so a caller may freely mutate the returned
        /// instance (including its
        /// <see cref="EnvironmentVariables"/> dictionary) without
        /// affecting any other launch.
        /// </summary>
        public static LaunchOptions Default
        {
            get { return new LaunchOptions(); }
        }

        /// <summary>
        /// Command line arguments passed to the executable, already split
        /// and quoted exactly as they should reach the process, or
        /// <c>null</c> to pass no arguments.
        /// </summary>
        public string? Arguments { get; set; }

        /// <summary>
        /// Working directory to use instead of the game's catalogued
        /// working directory, or <c>null</c> to use the catalogued one.
        /// Reserved: no compatibility backend exists yet, so the current
        /// launch pipeline does not apply this value — supplying it is
        /// acknowledged in the launch log instead of being silently
        /// dropped.
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// Extra environment variables for the launched process. May be
        /// empty, never needs to be <c>null</c>-checked by consumers that
        /// only read it. Reserved: no compatibility backend exists yet,
        /// so the current launch pipeline does not apply these values —
        /// supplying them is acknowledged in the launch log instead of
        /// being silently dropped.
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
