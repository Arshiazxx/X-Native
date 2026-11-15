namespace XNative.Platform.Executables
{
    /// <summary>
    /// Normalized view of the PE optional-header subsystem field.
    /// The raw subsystem value from the header is preserved separately on
    /// <see cref="PeInfo.SubsystemRaw"/> so no information is lost for
    /// subsystem values that this enum does not model directly.
    /// </summary>
    public enum PeSubsystem
    {
        /// <summary>
        /// The subsystem is unknown, absent, or the header carried the
        /// <c>IMAGE_SUBSYSTEM_UNKNOWN</c> value (0).
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Windows graphical user interface application
        /// (<c>IMAGE_SUBSYSTEM_WINDOWS_GUI</c>, raw value 2).
        /// </summary>
        WindowsGui = 1,

        /// <summary>
        /// Windows console application
        /// (<c>IMAGE_SUBSYSTEM_WINDOWS_CUI</c>, raw value 3).
        /// </summary>
        WindowsConsole = 2,

        /// <summary>
        /// Any other subsystem (native, EFI, Xbox, POSIX, and so on).
        /// Inspect <see cref="PeInfo.SubsystemRaw"/> for the exact value.
        /// </summary>
        Other = 3,
    }
}
