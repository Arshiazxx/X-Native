namespace XNative.Core.Games
{
    /// <summary>
    /// How well a game is known to work through X-Native, in the style
    /// of the ProtonDB / Wine AppDB rating scales. Ratings are community
    /// knowledge, not a promise: <see cref="Unknown"/> is the honest
    /// default for anything nobody has tested yet.
    /// </summary>
    public enum CompatibilityRating
    {
        /// <summary>
        /// Nobody has reported this game yet.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The game does not start, or crashes immediately.
        /// </summary>
        Broken = 1,

        /// <summary>
        /// The game starts but cannot really be played.
        /// </summary>
        Starts = 2,

        /// <summary>
        /// The game can be played, with noticeable issues.
        /// </summary>
        Playable = 3,

        /// <summary>
        /// The game works as well as on a PC.
        /// </summary>
        Perfect = 4,
    }
}
