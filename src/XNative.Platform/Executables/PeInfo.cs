using System;
using XNative.Core.Devices;

namespace XNative.Platform.Executables
{
    /// <summary>
    /// Immutable result of inspecting a byte sequence as a Windows portable
    /// executable (PE) image. Instances are produced by
    /// <see cref="PeFileInspector"/>; malformed input yields an instance with
    /// <see cref="IsValid"/> set to <c>false</c> and <see cref="Error"/>
    /// describing the first problem found, never an exception.
    /// </summary>
    public sealed class PeInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PeInfo"/> class.
        /// </summary>
        /// <param name="isValid">Whether the input is a structurally valid PE image.</param>
        /// <param name="error">Description of the validation failure, or <c>null</c> when valid.</param>
        /// <param name="architecture">Architecture decoded from the COFF machine field.</param>
        /// <param name="isDll">Whether the image is a dynamic-link library.</param>
        /// <param name="isManaged">Whether the image carries a CLR (COM descriptor) data directory.</param>
        /// <param name="subsystem">Normalized subsystem classification.</param>
        /// <param name="subsystemRaw">Raw subsystem value from the optional header.</param>
        /// <param name="machineRaw">Raw machine value from the COFF header.</param>
        /// <param name="isPe32Plus">Whether the optional header is PE32+; <c>null</c> when no optional header was parsed.</param>
        /// <param name="numberOfSections">Number of sections declared in the COFF header.</param>
        public PeInfo(
            bool isValid,
            string? error,
            ProcessorArchitecture architecture,
            bool isDll,
            bool isManaged,
            PeSubsystem subsystem,
            ushort subsystemRaw,
            ushort machineRaw,
            bool? isPe32Plus,
            ushort numberOfSections)
        {
            IsValid = isValid;
            Error = error;
            Architecture = architecture;
            IsDll = isDll;
            IsManaged = isManaged;
            Subsystem = subsystem;
            SubsystemRaw = subsystemRaw;
            MachineRaw = machineRaw;
            IsPe32Plus = isPe32Plus;
            NumberOfSections = numberOfSections;
        }

        /// <summary>
        /// Gets a value indicating whether the input parsed as a structurally
        /// valid PE image (DOS header, PE signature, COFF header and optional
        /// header all present and self-consistent).
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets a human-readable description of why the input is not a valid
        /// PE image, or <c>null</c> when <see cref="IsValid"/> is <c>true</c>.
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// Gets the processor architecture decoded from the COFF machine
        /// field. <see cref="ProcessorArchitecture.Unknown"/> when the machine
        /// value has no core equivalent or the input was invalid before the
        /// COFF header could be read.
        /// </summary>
        public ProcessorArchitecture Architecture { get; }

        /// <summary>
        /// Gets a value indicating whether the image is a dynamic-link
        /// library (<c>IMAGE_FILE_DLL</c> characteristic, 0x2000).
        /// </summary>
        public bool IsDll { get; }

        /// <summary>
        /// Gets a value indicating whether the image is a managed (.NET)
        /// assembly, detected by a non-zero RVA in data directory index 14
        /// (COM descriptor / CLR header).
        /// </summary>
        public bool IsManaged { get; }

        /// <summary>
        /// Gets the normalized subsystem classification.
        /// </summary>
        public PeSubsystem Subsystem { get; }

        /// <summary>
        /// Gets the raw subsystem value exactly as stored in the optional
        /// header, or 0 when no subsystem field was present.
        /// </summary>
        public ushort SubsystemRaw { get; }

        /// <summary>
        /// Gets the raw machine value exactly as stored in the COFF header,
        /// or 0 when the COFF header could not be read. Preserved even for
        /// machine values that map to
        /// <see cref="ProcessorArchitecture.Unknown"/> (for example IA64,
        /// 0x0200) so callers can distinguish them from a missing header.
        /// </summary>
        public ushort MachineRaw { get; }

        /// <summary>
        /// Gets a value indicating whether the optional header uses the
        /// PE32+ (64-bit) format as declared by its magic value
        /// (0x20B = PE32+, 0x10B = PE32). <c>null</c> when the input was
        /// rejected before the optional-header magic could be read.
        /// The value is taken from the magic alone: a mismatch between the
        /// magic and the COFF machine field does not invalidate the image.
        /// </summary>
        public bool? IsPe32Plus { get; }

        /// <summary>
        /// Gets the number of sections declared in the COFF header,
        /// or 0 when the COFF header could not be read.
        /// </summary>
        public ushort NumberOfSections { get; }

        /// <summary>
        /// Creates an invalid result carrying only an error message.
        /// </summary>
        /// <param name="error">Description of the validation failure.</param>
        /// <returns>An invalid <see cref="PeInfo"/>.</returns>
        internal static PeInfo Invalid(string error)
        {
            return Invalid(error, 0, 0);
        }

        /// <summary>
        /// Creates an invalid result that preserves header fields that were
        /// already read before the failure was detected.
        /// </summary>
        /// <param name="error">Description of the validation failure.</param>
        /// <param name="machineRaw">Raw COFF machine value, or 0 if unread.</param>
        /// <param name="numberOfSections">Declared section count, or 0 if unread.</param>
        /// <returns>An invalid <see cref="PeInfo"/>.</returns>
        internal static PeInfo Invalid(string error, ushort machineRaw, ushort numberOfSections)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            return new PeInfo(
                false,
                error,
                ProcessorArchitecture.Unknown,
                false,
                false,
                PeSubsystem.Unknown,
                0,
                machineRaw,
                null,
                numberOfSections);
        }
    }
}
