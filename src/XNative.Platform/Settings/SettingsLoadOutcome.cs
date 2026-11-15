namespace XNative.Platform.Settings
{
    /// <summary>
    /// What became of the stored settings during a load: whether they
    /// were used, were not there, or had to be rejected.
    /// </summary>
    /// <remarks>
    /// This is the diagnostic counterpart to the deliberate collapse
    /// in <see cref="XNative.Core.Abstractions.ISettingsService.GetAsync"/>:
    /// that contract returns the defaults for absent, unreadable and
    /// corrupt stored data alike, so a caller holding only the
    /// returned settings cannot tell "the user never stored anything"
    /// from "the user's stored file was ignored". The distinction
    /// matters to exactly one kind of caller — diagnostics such as a
    /// <c>settings show</c> command — so it is exposed as a
    /// side channel (<see cref="ISettingsLoadOutcomeProvider"/>) rather
    /// than by changing the load contract itself.
    /// </remarks>
    public enum StoredSettingsState
    {
        /// <summary>
        /// Stored settings existed and were used (or were written by
        /// a save/reset, which leaves valid stored settings behind).
        /// </summary>
        Loaded = 0,

        /// <summary>
        /// Nothing was stored: the defaults apply because there was
        /// no stored data to apply, not because any was rejected.
        /// </summary>
        Absent = 1,

        /// <summary>
        /// Stored data existed but could not be used; the defaults
        /// were substituted. See
        /// <see cref="SettingsLoadOutcome.RejectionReason"/>.
        /// </summary>
        Rejected = 2,
    }

    /// <summary>
    /// Why stored settings were rejected during a load. Only
    /// meaningful when <see cref="SettingsLoadOutcome.State"/> is
    /// <see cref="StoredSettingsState.Rejected"/>; a category, never a
    /// raw error — it is safe to show to a user as-is.
    /// </summary>
    public enum SettingsRejectionReason
    {
        /// <summary>No rejection: the stored settings were used or absent.</summary>
        None = 0,

        /// <summary>
        /// The stored text is not settings data this build can make
        /// sense of: malformed JSON, or JSON of the wrong shape.
        /// </summary>
        CorruptData = 1,

        /// <summary>
        /// The stored format version is not the one this build
        /// understands (including a newer, future version).
        /// </summary>
        UnsupportedVersion = 2,

        /// <summary>
        /// The stored values parsed but fail
        /// <see cref="XNative.Core.Settings.AppSettings.Validate"/>.
        /// </summary>
        InvalidValues = 3,

        /// <summary>
        /// The storage backend could not read the stored data at all
        /// (for example an I/O error or a permissions failure).
        /// </summary>
        ReadFailure = 4,
    }

    /// <summary>
    /// The outcome of a settings load as observed by the settings
    /// layer: the <see cref="State"/> of the stored settings and, for
    /// a rejection, the <see cref="RejectionReason"/> category.
    /// </summary>
    public sealed class SettingsLoadOutcome
    {
        internal SettingsLoadOutcome(StoredSettingsState state, SettingsRejectionReason rejectionReason)
        {
            State = state;
            RejectionReason = rejectionReason;
        }

        /// <summary>
        /// Gets what became of the stored settings during the load.
        /// </summary>
        public StoredSettingsState State { get; }

        /// <summary>
        /// Gets why the stored settings were rejected, or
        /// <see cref="SettingsRejectionReason.None"/> when
        /// <see cref="State"/> is not
        /// <see cref="StoredSettingsState.Rejected"/>.
        /// </summary>
        public SettingsRejectionReason RejectionReason { get; }
    }

    /// <summary>
    /// Optional side channel implemented by settings services that can
    /// report what became of the stored settings on their last load
    /// (see <see cref="SettingsLoadOutcome"/>). Consumers that only
    /// need the settings themselves use
    /// <see cref="XNative.Core.Abstractions.ISettingsService"/> as
    /// usual; diagnostic consumers additionally check whether the
    /// service implements this interface.
    /// </summary>
    public interface ISettingsLoadOutcomeProvider
    {
        /// <summary>
        /// Gets the outcome of the most recent load, save or reset
        /// performed by this service, or <c>null</c> when the service
        /// has not interacted with storage yet. A save or reset
        /// reports <see cref="StoredSettingsState.Loaded"/>: it leaves
        /// valid stored settings behind. The value is retained across
        /// cache hits — it describes the stored data, not the most
        /// recent call.
        /// </summary>
        SettingsLoadOutcome? LastLoadOutcome { get; }
    }
}
