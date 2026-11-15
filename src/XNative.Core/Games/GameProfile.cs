using System;

namespace XNative.Core.Games
{
    /// <summary>
    /// A compatibility profile: everything X-Native knows about how one
    /// game (or one executable name pattern) behaves, and the settings
    /// that make it work best. Profiles live in the compatibility
    /// database and are matched to catalogued games by
    /// <see cref="ExecutableName"/>.
    /// </summary>
    public sealed class GameProfile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GameProfile"/> class.
        /// </summary>
        /// <param name="id">Stable identifier of the profile.</param>
        /// <param name="executableName">File name of the executable this profile applies to, for example <c>game.exe</c>.</param>
        /// <param name="rating">How well the game is known to work.</param>
        /// <param name="notes">Free-form notes for players and contributors.</param>
        /// <param name="recommendedArguments">Arguments recommended for this game, or <c>null</c> for none.</param>
        public GameProfile(
            string id,
            string executableName,
            CompatibilityRating rating,
            string? notes = null,
            string? recommendedArguments = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            ExecutableName = executableName ?? throw new ArgumentNullException(nameof(executableName));
            Rating = rating;
            Notes = notes;
            RecommendedArguments = recommendedArguments;

            if (string.IsNullOrWhiteSpace(Id))
                throw new ArgumentException("Id cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(ExecutableName))
                throw new ArgumentException("ExecutableName cannot be empty.", nameof(executableName));
        }

        /// <summary>
        /// Stable identifier of the profile.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// File name of the executable this profile applies to.
        /// </summary>
        public string ExecutableName { get; }

        /// <summary>
        /// How well the game is known to work.
        /// </summary>
        public CompatibilityRating Rating { get; }

        /// <summary>
        /// Free-form notes for players and contributors.
        /// </summary>
        public string? Notes { get; }

        /// <summary>
        /// Arguments recommended for this game, or <c>null</c> for none.
        /// </summary>
        public string? RecommendedArguments { get; }
    }
}
