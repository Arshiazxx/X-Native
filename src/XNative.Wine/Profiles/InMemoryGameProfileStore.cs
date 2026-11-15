using System;
using System.Collections.Generic;
using XNative.Core.Abstractions;
using XNative.Core.Games;

namespace XNative.Wine.Profiles
{
    /// <summary>
    /// An <see cref="IGameProfileStore"/> backed by a fixed in-memory
    /// list of profiles supplied at construction time. Matching is by
    /// executable file name, case-insensitively, and ignores any
    /// directory portion of either the game's path or the profile's
    /// executable name, so Windows-style paths (<c>C:\Games\game.exe</c>)
    /// match correctly on any host operating system.
    /// </summary>
    /// <remarks>
    /// Profile IDs must be unique: two profiles sharing an ID are
    /// rejected at construction time instead of silently shadowing each
    /// other. IDs are compared case-insensitively, matching how the
    /// launch pipeline resolves a requested
    /// <see cref="LaunchOptions.ProfileId"/> against profile IDs.
    /// Executable names must be unique too, under the same
    /// normalisation name matching uses (any directory portion ignored,
    /// compared case-insensitively): two profiles for the same
    /// executable name are rejected instead of the later one being
    /// silently unreachable by name matching.
    /// </remarks>
    public sealed class InMemoryGameProfileStore : IGameProfileStore
    {
        private readonly List<GameProfile> _profiles;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="InMemoryGameProfileStore"/> class with no profiles.
        /// </summary>
        public InMemoryGameProfileStore()
            : this(new GameProfile[0])
        {
        }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="InMemoryGameProfileStore"/> class.
        /// </summary>
        /// <param name="profiles">The profiles the store knows about.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="profiles"/> or one of its entries is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when two profiles share the same ID, compared case-insensitively, or the same executable name, compared case-insensitively and ignoring any directory portion.</exception>
        public InMemoryGameProfileStore(IEnumerable<GameProfile> profiles)
        {
            if (profiles == null)
            {
                throw new ArgumentNullException(nameof(profiles));
            }

            _profiles = new List<GameProfile>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenExecutableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GameProfile profile in profiles)
            {
                if (profile == null)
                {
                    throw new ArgumentNullException(nameof(profiles), "The profile collection must not contain null entries.");
                }

                if (!seenIds.Add(profile.Id))
                {
                    throw new ArgumentException(
                        "Duplicate profile ID '" + profile.Id + "'. Profile IDs must be unique " +
                        "(compared case-insensitively); a duplicate would silently shadow the earlier profile.",
                        nameof(profiles));
                }

                if (!seenExecutableNames.Add(GetFileName(profile.ExecutableName)))
                {
                    throw new ArgumentException(
                        "Duplicate executable name '" + profile.ExecutableName + "'. Executable names must be unique " +
                        "(compared case-insensitively, ignoring any directory portion); a duplicate would " +
                        "silently shadow the earlier profile in name matching.",
                        nameof(profiles));
                }

                _profiles.Add(profile);
            }
        }

        /// <inheritdoc />
        public IReadOnlyCollection<GameProfile> GetProfiles()
        {
            return new List<GameProfile>(_profiles);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="game"/> is <c>null</c>.</exception>
        public GameProfile? FindForGame(GameInfo game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            string executableName = GetFileName(game.ExecutablePath);
            foreach (GameProfile profile in _profiles)
            {
                if (string.Equals(
                    GetFileName(profile.ExecutableName),
                    executableName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts the file-name portion of a path that may use either
        /// <c>\</c> or <c>/</c> as its directory separator.
        /// </summary>
        /// <param name="path">The path to extract from.</param>
        /// <returns>The file name, or the whole string when it contains no separator.</returns>
        internal static string GetFileName(string path)
        {
            int index = path.LastIndexOfAny(new char[] { '\\', '/' });
            return index >= 0 ? path.Substring(index + 1) : path;
        }
    }
}
