using System;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Settings;

namespace XNative.Core.Abstractions
{
    /// <summary>
    /// Access to the persisted <see cref="AppSettings"/>. Implemented
    /// in <c>XNative.Platform</c> on top of <see cref="IStorageService"/>.
    /// </summary>
    /// <remarks>
    /// The contract deliberately exposes no change events: callers load
    /// settings once at startup, apply them to the relevant options,
    /// and save explicitly. That keeps the composition-root wiring
    /// simple and predictable.
    /// </remarks>
    public interface ISettingsService
    {
        /// <summary>
        /// Returns the current settings, loading them from storage on
        /// first use.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The stored settings, or a new default
        /// <see cref="AppSettings"/> when nothing has been stored yet.
        /// This method never returns <c>null</c> and never throws
        /// because stored data is corrupt or cannot be read:
        /// unreadable JSON, a storage read failure (for example an
        /// I/O error from a permissions-broken storage folder), values
        /// that fail <see cref="AppSettings.Validate"/>, or a stored
        /// format version newer than the implementation understands
        /// all yield the defaults instead, so corruption can never
        /// crash startup. Corrupt stored data is left untouched
        /// (nothing is overwritten) so it remains available for
        /// diagnostics. Cancellation requested through
        /// <paramref name="cancellationToken"/> still propagates as
        /// <see cref="System.OperationCanceledException"/>.
        /// </returns>
        Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates and persists <paramref name="settings"/>, replacing
        /// any previously stored values.
        /// </summary>
        /// <param name="settings">The settings to persist.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the save.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="settings"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="settings"/> fails
        /// <see cref="AppSettings.Validate"/>; nothing is written in
        /// that case.
        /// </exception>
        Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores the defaults, persisting them so the reset itself
        /// survives a restart, and discarding any customised values.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the reset.</returns>
        Task ResetAsync(CancellationToken cancellationToken = default);
    }
}
