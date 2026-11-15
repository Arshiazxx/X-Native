namespace XNative.Platform.Storage
{
    /// <summary>
    /// Configuration options for <see cref="FileStorageService"/>.
    /// Mirrors the options pattern used by
    /// <c>LocalGameCatalogOptions</c>.
    /// </summary>
    public sealed class FileStorageServiceOptions
    {
        /// <summary>
        /// Root directory every key is stored under. Created lazily on
        /// first use, never at registration time.
        /// </summary>
        public string RootPath { get; set; } = string.Empty;
    }
}
