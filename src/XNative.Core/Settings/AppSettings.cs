using System;
using XNative.Core.Logging;

namespace XNative.Core.Settings
{
    /// <summary>
    /// Mutable, persisted application settings. A new instance carries
    /// the sane defaults; <see cref="Validate"/> must pass before an
    /// instance is persisted or applied.
    /// </summary>
    /// <remarks>
    /// <see cref="GamesRootPath"/> is <c>null</c> by default, meaning
    /// "use the platform default root". That matters on Xbox, where
    /// games live on external or removable storage rather than under
    /// the user's documents folder, so no documents-folder path is
    /// baked in here.
    /// </remarks>
    public sealed class AppSettings
    {
        /// <summary>
        /// Minimum permitted value for <see cref="ScanMaxDepth"/>.
        /// </summary>
        public const int MinScanMaxDepth = 0;

        /// <summary>
        /// Maximum permitted value for <see cref="ScanMaxDepth"/>.
        /// </summary>
        public const int MaxScanMaxDepth = 10;

        /// <summary>
        /// Default value for <see cref="ScanMaxDepth"/>.
        /// </summary>
        public const int DefaultScanMaxDepth = 3;

        /// <summary>
        /// Maximum permitted number of entries in
        /// <see cref="ExecutablePatterns"/>. The catalog issues one
        /// directory enumeration per pattern per directory during a
        /// scan, so the count is capped to keep a corrupt or hostile
        /// settings file from turning every scan into thousands of
        /// enumerations. 32 is far above any plausible hand-written
        /// pattern list (the default is one pattern).
        /// </summary>
        public const int MaxExecutablePatterns = 32;

        /// <summary>
        /// Root directory the game catalog scans. <c>null</c> (the
        /// default) means the platform default root is used and this
        /// setting does not override it.
        /// </summary>
        public string? GamesRootPath { get; set; }

        /// <summary>
        /// Lowest log level that is recorded. Defaults to
        /// <see cref="LogLevel.Information"/>.
        /// </summary>
        public LogLevel LogMinimumLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Maximum directory depth the game catalog scans below
        /// <see cref="GamesRootPath"/>. Defaults to 3 and must lie in
        /// the range 0..10 (see <see cref="Validate"/>).
        /// </summary>
        public int ScanMaxDepth { get; set; } = DefaultScanMaxDepth;

        /// <summary>
        /// File-name patterns considered game executables during a
        /// catalog scan (for example <c>*.exe</c>). Defaults to a
        /// single <c>*.exe</c> pattern. A valid instance has between
        /// one and <see cref="MaxExecutablePatterns"/> patterns, none
        /// of them blank and none containing a directory separator —
        /// patterns name files, not paths (see <see cref="Validate"/>).
        /// A bare <c>*</c> or <c>*.*</c> is accepted deliberately: it
        /// costs the same single enumeration per directory as any
        /// other pattern, and matching every file is a legitimate
        /// choice when executables carry no consistent extension —
        /// the catalog's PE inspection decides what is actually a
        /// game.
        /// </summary>
        public string[] ExecutablePatterns { get; set; } = new[] { "*.exe" };

        /// <summary>
        /// Creates a new instance populated with the default values.
        /// Equivalent to <c>new AppSettings()</c>; named for call sites
        /// that want the intent to be explicit.
        /// </summary>
        /// <returns>A new default settings instance.</returns>
        public static AppSettings CreateDefault()
        {
            return new AppSettings();
        }

        /// <summary>
        /// Validates this instance.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when a property holds an invalid value. The exception
        /// names the offending property both in its message and in
        /// <see cref="ArgumentException.ParamName"/>.
        /// </exception>
        public void Validate()
        {
            if (GamesRootPath != null && string.IsNullOrWhiteSpace(GamesRootPath))
            {
                throw new ArgumentException(
                    "GamesRootPath must be null (platform default) or a non-empty path.",
                    nameof(GamesRootPath));
            }

            if (!Enum.IsDefined(typeof(LogLevel), LogMinimumLevel))
            {
                throw new ArgumentException(
                    "LogMinimumLevel must be a defined LogLevel value.",
                    nameof(LogMinimumLevel));
            }

            if (ScanMaxDepth < MinScanMaxDepth || ScanMaxDepth > MaxScanMaxDepth)
            {
                throw new ArgumentException(
                    "ScanMaxDepth must be between "
                    + MinScanMaxDepth.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " and "
                    + MaxScanMaxDepth.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " inclusive.",
                    nameof(ScanMaxDepth));
            }

            if (ExecutablePatterns == null)
            {
                throw new ArgumentException(
                    "ExecutablePatterns must not be null.",
                    nameof(ExecutablePatterns));
            }

            if (ExecutablePatterns.Length == 0)
            {
                throw new ArgumentException(
                    "ExecutablePatterns must contain at least one pattern.",
                    nameof(ExecutablePatterns));
            }

            if (ExecutablePatterns.Length > MaxExecutablePatterns)
            {
                throw new ArgumentException(
                    "ExecutablePatterns must contain at most "
                    + MaxExecutablePatterns.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " patterns; each pattern costs one directory enumeration per "
                    + "scanned directory, so an unbounded list makes scans arbitrarily "
                    + "expensive.",
                    nameof(ExecutablePatterns));
            }

            for (int i = 0; i < ExecutablePatterns.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(ExecutablePatterns[i]))
                {
                    throw new ArgumentException(
                        "ExecutablePatterns must not contain null, empty, or whitespace patterns.",
                        nameof(ExecutablePatterns));
                }

                if (ExecutablePatterns[i].IndexOf('/') >= 0
                    || ExecutablePatterns[i].IndexOf('\\') >= 0)
                {
                    throw new ArgumentException(
                        "ExecutablePatterns entries are file-name patterns and must not "
                        + "contain directory separators ('/' or '\\').",
                        nameof(ExecutablePatterns));
                }
            }
        }

        /// <summary>
        /// Returns a deep copy of this instance. The
        /// <see cref="ExecutablePatterns"/> array is copied, so mutating
        /// the clone's array does not affect this instance.
        /// </summary>
        /// <returns>An independent copy with the same values.</returns>
        public AppSettings Clone()
        {
            return new AppSettings
            {
                GamesRootPath = GamesRootPath,
                LogMinimumLevel = LogMinimumLevel,
                ScanMaxDepth = ScanMaxDepth,
                ExecutablePatterns = ExecutablePatterns == null
                    ? null!
                    : (string[])ExecutablePatterns.Clone(),
            };
        }
    }
}
