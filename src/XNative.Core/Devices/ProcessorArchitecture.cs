namespace XNative.Core.Devices
{
    /// <summary>
    /// Processor architecture of a device or of a portable executable.
    /// The numeric values deliberately mirror the PE COFF machine types
    /// where a direct equivalent exists, but this enum is the platform
    /// independent vocabulary of the core and must not be treated as a
    /// PE header value.
    /// </summary>
    public enum ProcessorArchitecture
    {
        /// <summary>
        /// The architecture could not be determined.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 32-bit Intel x86.
        /// </summary>
        X86 = 1,

        /// <summary>
        /// 64-bit AMD/Intel x64.
        /// </summary>
        X64 = 2,

        /// <summary>
        /// 32-bit ARM.
        /// </summary>
        Arm = 3,

        /// <summary>
        /// 64-bit ARM.
        /// </summary>
        Arm64 = 4,
    }
}
