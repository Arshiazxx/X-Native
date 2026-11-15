using System;

namespace XNative.Platform.Tests.Executables
{
    /// <summary>
    /// Builds minimal-but-structurally-valid PE byte arrays field by field,
    /// so inspector tests never depend on real binary fixtures.
    /// </summary>
    /// <remarks>
    /// The layout produced is: a 0x80-byte DOS stub (MZ + e_lfanew), the
    /// PE signature, a 20-byte COFF header, a standard-size optional header
    /// (224 bytes for PE32, 240 bytes for PE32+, each with 16 data
    /// directories) and one 40-byte section header per declared section.
    /// Defaults describe a native x64 PE32+ Windows GUI executable with a
    /// single section; fluent <c>With*</c> methods vary one field at a time.
    /// Corruption cases (bad signature, truncation, wild offsets) are best
    /// expressed by building a valid file and then mutating or truncating
    /// the returned array in the test itself.
    /// </remarks>
    public sealed class SyntheticPe
    {
        /// <summary>COFF machine value for 32-bit x86 (0x014C).</summary>
        public const ushort MachineX86 = 0x014C;

        /// <summary>COFF machine value for 64-bit x64 (0x8664).</summary>
        public const ushort MachineX64 = 0x8664;

        /// <summary>COFF machine value for 32-bit ARM (0x01C0).</summary>
        public const ushort MachineArm = 0x01C0;

        /// <summary>COFF machine value for ARM NT (0x01C4).</summary>
        public const ushort MachineArmNt = 0x01C4;

        /// <summary>COFF machine value for 64-bit ARM (0xAA64).</summary>
        public const ushort MachineArm64 = 0xAA64;

        /// <summary>COFF machine value for IA64 (0x0200); maps to Unknown.</summary>
        public const ushort MachineIa64 = 0x0200;

        /// <summary>Raw subsystem value for Windows GUI (2).</summary>
        public const ushort SubsystemGui = 2;

        /// <summary>Raw subsystem value for Windows console (3).</summary>
        public const ushort SubsystemConsole = 3;

        /// <summary>Offset of the PE header within the built file.</summary>
        public const int PeHeaderOffset = 0x80;

        private const ushort OptionalMagicPe32 = 0x10B;
        private const ushort OptionalMagicPe32Plus = 0x20B;
        private const int Pe32OptionalHeaderSize = 224;
        private const int Pe32PlusOptionalHeaderSize = 240;
        private const int SectionHeaderSize = 40;
        private const ushort ExecutableCharacteristics = 0x0022;
        private const ushort DllCharacteristic = 0x2000;

        private ushort _machine = MachineX64;
        private ushort _characteristics = ExecutableCharacteristics;
        private ushort _numberOfSections = 1;
        private bool _isPe32Plus = true;
        private ushort _subsystem = SubsystemGui;
        private uint _numberOfRvaAndSizes = 16;
        private uint _clrRva;
        private uint _clrSize;
        private int? _sizeOfOptionalHeaderOverride;

        /// <summary>Creates a builder with x64 PE32+ GUI executable defaults.</summary>
        /// <returns>A new builder.</returns>
        public static SyntheticPe Create()
        {
            return new SyntheticPe();
        }

        /// <summary>Builds a native x64 PE32+ Windows GUI executable.</summary>
        /// <returns>The PE file bytes.</returns>
        public static byte[] X64GuiExe()
        {
            return Create().Build();
        }

        /// <summary>Builds a native x86 PE32 Windows console executable.</summary>
        /// <returns>The PE file bytes.</returns>
        public static byte[] X86ConsoleExe()
        {
            return Create()
                .WithMachine(MachineX86)
                .WithPe32()
                .WithSubsystem(SubsystemConsole)
                .Build();
        }

        /// <summary>Builds a native x64 PE32+ DLL.</summary>
        /// <returns>The PE file bytes.</returns>
        public static byte[] X64Dll()
        {
            return Create().AsDll().Build();
        }

        /// <summary>Builds a managed x64 PE32+ executable (CLR directory set).</summary>
        /// <returns>The PE file bytes.</returns>
        public static byte[] ManagedX64Exe()
        {
            return Create().WithClrDirectory(0x2000, 0x48).Build();
        }

        /// <summary>Builds a native ARM64 PE32+ executable.</summary>
        /// <returns>The PE file bytes.</returns>
        public static byte[] Arm64Exe()
        {
            return Create().WithMachine(MachineArm64).Build();
        }

        /// <summary>Sets the COFF machine value.</summary>
        /// <param name="machine">Raw machine value, e.g. <see cref="MachineX64"/>.</param>
        /// <returns>This builder.</returns>
        public SyntheticPe WithMachine(ushort machine)
        {
            _machine = machine;
            return this;
        }

        /// <summary>Sets the full COFF characteristics value.</summary>
        /// <param name="characteristics">Raw characteristics flags.</param>
        /// <returns>This builder.</returns>
        public SyntheticPe WithCharacteristics(ushort characteristics)
        {
            _characteristics = characteristics;
            return this;
        }

        /// <summary>Adds the IMAGE_FILE_DLL (0x2000) characteristic.</summary>
        /// <returns>This builder.</returns>
        public SyntheticPe AsDll()
        {
            _characteristics = (ushort)(_characteristics | DllCharacteristic);
            return this;
        }

        /// <summary>Sets the declared number of sections.</summary>
        /// <param name="numberOfSections">Section count written to the COFF header and laid out in the file.</param>
        /// <returns>This builder.</returns>
        public SyntheticPe WithNumberOfSections(ushort numberOfSections)
        {
            _numberOfSections = numberOfSections;
            return this;
        }

        /// <summary>Selects the PE32 (32-bit) optional header format.</summary>
        /// <returns>This builder.</returns>
        public SyntheticPe WithPe32()
        {
            _isPe32Plus = false;
            return this;
        }

        /// <summary>Selects the PE32+ (64-bit) optional header format.</summary>
        /// <returns>This builder.</returns>
        public SyntheticPe WithPe32Plus()
        {
            _isPe32Plus = true;
            return this;
        }

        /// <summary>Sets the raw subsystem value.</summary>
        /// <param name="subsystem">Raw subsystem value, e.g. <see cref="SubsystemGui"/>.</param>
        /// <returns>This builder.</returns>
        public SyntheticPe WithSubsystem(ushort subsystem)
        {
            _subsystem = subsystem;
            return this;
        }

        /// <summary>Sets the NumberOfRvaAndSizes field of the optional header.</summary>
        /// <param name="numberOfRvaAndSizes">Declared data directory count.</param>
        /// <returns>This builder.</returns>
        public SyntheticPe WithNumberOfRvaAndSizes(uint numberOfRvaAndSizes)
        {
            _numberOfRvaAndSizes = numberOfRvaAndSizes;
            return this;
        }

        /// <summary>
        /// Sets data directory index 14 (COM descriptor / CLR header), which
        /// is how a managed (.NET) image is recognized.
        /// </summary>
        /// <param name="rva">CLR header RVA; non-zero marks the image managed.</param>
        /// <param name="size">CLR header size.</param>
        /// <returns>This builder.</returns>
        public SyntheticPe WithClrDirectory(uint rva, uint size)
        {
            _clrRva = rva;
            _clrSize = size;
            return this;
        }

        /// <summary>
        /// Overrides the optional header size. The declared COFF
        /// SizeOfOptionalHeader and the laid-out optional header region both
        /// use this value; fields that no longer fit are omitted.
        /// </summary>
        /// <param name="sizeOfOptionalHeader">Optional header size in bytes.</param>
        /// <returns>This builder.</returns>
        public SyntheticPe WithSizeOfOptionalHeader(int sizeOfOptionalHeader)
        {
            _sizeOfOptionalHeaderOverride = sizeOfOptionalHeader;
            return this;
        }

        /// <summary>Builds the PE file bytes.</summary>
        /// <returns>A new byte array containing the synthetic PE file.</returns>
        public byte[] Build()
        {
            int optionalHeaderSize = _sizeOfOptionalHeaderOverride
                ?? (_isPe32Plus ? Pe32PlusOptionalHeaderSize : Pe32OptionalHeaderSize);
            int coffOffset = PeHeaderOffset + 4;
            int optionalOffset = coffOffset + 20;
            int totalSize = optionalOffset + optionalHeaderSize + (_numberOfSections * SectionHeaderSize);
            var data = new byte[totalSize];

            // DOS header: MZ magic and e_lfanew pointing at the PE header.
            WriteUInt16(data, 0, 0x5A4D);
            WriteInt32(data, 0x3C, PeHeaderOffset);

            // PE signature.
            WriteUInt32(data, PeHeaderOffset, 0x00004550);

            // COFF header.
            WriteUInt16(data, coffOffset, _machine);
            WriteUInt16(data, coffOffset + 2, _numberOfSections);
            WriteUInt16(data, coffOffset + 16, (ushort)optionalHeaderSize);
            WriteUInt16(data, coffOffset + 18, _characteristics);

            // Optional header: magic, subsystem, directory count, CLR entry.
            // Each field is written only when it fits the (possibly
            // overridden) optional header size, mirroring a real short header.
            WriteUInt16(data, optionalOffset, _isPe32Plus ? OptionalMagicPe32Plus : OptionalMagicPe32);
            if (optionalHeaderSize >= 70)
            {
                WriteUInt16(data, optionalOffset + 68, _subsystem);
            }

            int numberOfRvaOffset = _isPe32Plus ? 108 : 92;
            int directoryOffset = _isPe32Plus ? 112 : 96;
            if (optionalHeaderSize >= numberOfRvaOffset + 4)
            {
                WriteUInt32(data, optionalOffset + numberOfRvaOffset, _numberOfRvaAndSizes);
            }

            int clrEntryOffset = directoryOffset + (14 * 8);
            if (optionalHeaderSize >= clrEntryOffset + 8)
            {
                WriteUInt32(data, optionalOffset + clrEntryOffset, _clrRva);
                WriteUInt32(data, optionalOffset + clrEntryOffset + 4, _clrSize);
            }

            // Section headers: zeroed except a ".text" name on the first one.
            if (_numberOfSections > 0)
            {
                int sectionOffset = optionalOffset + optionalHeaderSize;
                byte[] name = { (byte)'.', (byte)'t', (byte)'e', (byte)'x', (byte)'t', 0, 0, 0 };
                Array.Copy(name, 0, data, sectionOffset, name.Length);
            }

            return data;
        }

        private static void WriteUInt16(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteInt32(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            WriteInt32(data, offset, unchecked((int)value));
        }
    }
}
