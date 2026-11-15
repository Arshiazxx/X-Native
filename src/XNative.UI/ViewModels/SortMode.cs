namespace XNative.UI.ViewModels
{
    /// <summary>
    /// Ordering applied to the visible games of the game library.
    /// </summary>
    public enum SortMode
    {
        /// <summary>
        /// Games are ordered by name, A to Z (case-insensitive).
        /// </summary>
        NameAscending = 0,

        /// <summary>
        /// Games are ordered by name, Z to A (case-insensitive).
        /// </summary>
        NameDescending = 1,
    }
}
