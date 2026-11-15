using System;
using XNative.Core.Settings;
using XNative.Platform.Games;
using XNative.Platform.Logging;

namespace XNative.Platform.Settings
{
    /// <summary>
    /// Pure helpers that apply a loaded <see cref="AppSettings"/> onto
    /// the options objects the platform services consume, so a
    /// composition root can load settings once and then configure the
    /// catalog and the log service from them:
    /// <c>var s = await settingsService.GetAsync(); SettingsOptionsApplier.ApplyToCatalog(s, catalogOptions);</c>
    /// </summary>
    /// <remarks>
    /// These helpers never mutate the <see cref="AppSettings"/> they
    /// are given, and the pattern array is copied into the catalog
    /// options so later mutation of either side cannot affect the
    /// other. Both helpers validate the settings first
    /// (<see cref="AppSettings.Validate"/>) and apply nothing when
    /// validation fails: the applier is the boundary between persisted
    /// settings and the services, so an invalid instance — an
    /// out-of-range scan depth, a <c>null</c> pattern array — must
    /// fail loudly here instead of flowing into the catalog, where it
    /// would surface as a scan crash or an unbounded scan.
    /// </remarks>
    public static class SettingsOptionsApplier
    {
        /// <summary>
        /// Applies <paramref name="settings"/> onto catalog options.
        /// Only a non-<c>null</c> <see cref="AppSettings.GamesRootPath"/>
        /// overrides <see cref="LocalGameCatalogOptions.RootPath"/>;
        /// <see cref="AppSettings.ScanMaxDepth"/> and
        /// <see cref="AppSettings.ExecutablePatterns"/> always apply.
        /// </summary>
        /// <param name="settings">The settings to apply.</param>
        /// <param name="options">The catalog options to update.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when either argument is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="settings"/> fails
        /// <see cref="AppSettings.Validate"/>; nothing is applied in
        /// that case.
        /// </exception>
        public static void ApplyToCatalog(AppSettings settings, LocalGameCatalogOptions options)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            settings.Validate();

            if (settings.GamesRootPath != null)
            {
                options.RootPath = settings.GamesRootPath;
            }

            options.MaxDepth = settings.ScanMaxDepth;
            options.ExecutablePatterns = (string[])settings.ExecutablePatterns.Clone();
        }

        /// <summary>
        /// Applies <paramref name="settings"/> onto log options:
        /// <see cref="AppSettings.LogMinimumLevel"/> becomes
        /// <see cref="FileLogServiceOptions.MinimumLevel"/>. No other
        /// log option is touched.
        /// </summary>
        /// <param name="settings">The settings to apply.</param>
        /// <param name="options">The log options to update.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when either argument is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="settings"/> fails
        /// <see cref="AppSettings.Validate"/>; nothing is applied in
        /// that case.
        /// </exception>
        public static void ApplyToLog(AppSettings settings, FileLogServiceOptions options)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            settings.Validate();

            options.MinimumLevel = settings.LogMinimumLevel;
        }

        /// <summary>
        /// Overload of
        /// <see cref="ApplyToCatalog(AppSettings, LocalGameCatalogOptions)"/>
        /// for call sites that prefer a generic name.
        /// </summary>
        /// <param name="settings">The settings to apply.</param>
        /// <param name="options">The catalog options to update.</param>
        public static void Apply(AppSettings settings, LocalGameCatalogOptions options)
        {
            ApplyToCatalog(settings, options);
        }

        /// <summary>
        /// Overload of
        /// <see cref="ApplyToLog(AppSettings, FileLogServiceOptions)"/>
        /// for call sites that prefer a generic name.
        /// </summary>
        /// <param name="settings">The settings to apply.</param>
        /// <param name="options">The log options to update.</param>
        public static void Apply(AppSettings settings, FileLogServiceOptions options)
        {
            ApplyToLog(settings, options);
        }
    }
}
