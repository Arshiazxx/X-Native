using System.Collections.Generic;
using XNative.Core.Games;

namespace XNative.Core.Abstractions
{
    /// <summary>
    /// Read access to the compatibility database: the set of
    /// <see cref="GameProfile"/> records X-Native ships and the user
    /// can extend. Implemented in <c>XNative.Wine</c>.
    /// </summary>
    public interface IGameProfileStore
    {
        /// <summary>
        /// Every profile currently known.
        /// </summary>
        /// <returns>The known profiles, never <c>null</c>.</returns>
        IReadOnlyCollection<GameProfile> GetProfiles();

        /// <summary>
        /// Finds the profile that applies to a game, matching on the
        /// game's executable file name, case-insensitively.
        /// </summary>
        /// <param name="game">The game to look up.</param>
        /// <returns>The matching profile, or <c>null</c> when nobody has profiled this game yet.</returns>
        GameProfile? FindForGame(GameInfo game);
    }
}
