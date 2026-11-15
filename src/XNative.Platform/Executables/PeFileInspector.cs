using System;
using System.IO;
using XNative.Core.Devices;

namespace XNative.Platform.Executables
{
    /// <summary>
    /// Parses the headers of a Windows portable executable (PE) image and
    /// reports what the image is: its target architecture, whether it is a
    /// DLL, whether it is a managed (.NET) assembly, and its subsystem.
    /// </summary>
    /// <remarks>
    /// The inspector reads only the DOS header, the PE signature, the COFF
    /// header and the optional header (including its data directories). Every
    /// offset is bounds-checked with overflow-safe arithmetic before it is
    /// dereferenced: malformed or truncated input always produces an invalid
    /// <see cref="PeInfo"/> and never throws. The only exception raised is
    /// <see cref="ArgumentNullException"/> for a <c>null</c> argument.
    /// Instances are stateless and safe to share between threads.
    /// </remarks>
    public sealed class PeFileInspector
    {
        // DOS header.
        private const ushort DosSignature = 0x5A4D; // "MZ"
        private const int LfanewOffset = 0x3C;
        private const int MinimumDosHeaderSize = 0x40;

        // PE signature "PE\0\0" as a little-endian uint.
        private const uint PeSignature = 0x00004550;

        // COFF header layout (20 bytes, immediately after the signature).
        private const int CoffHeaderSize = 20;
        private const int CoffMachineOffset = 0;
        private const int CoffNumberOfSectionsOffset = 2;
        private const int CoffSizeOfOptionalHeaderOffset = 16;
        private const int CoffCharacteristicsOffset = 18;
        private const ushort ImageFileDll = 0x2000;

        // COFF machine values.
        private const ushort MachineI386 = 0x014C;
        private const ushort MachineAmd64 = 0x8664;
        private const ushort MachineArm = 0x01C0;
        private const ushort MachineArmNt = 0x01C4;
        private const ushort MachineArm64 = 0xAA64;

        // Optional header layout.
        private const ushort OptionalMagicPe32 = 0x10B;
        private const ushort OptionalMagicPe32Plus = 0x20B;
        private const int OptionalSubsystemOffset = 68;
        private const int OptionalSubsystemSize = 2;
        private const int Pe32NumberOfRvaOffset = 92;
        private const int Pe32PlusNumberOfRvaOffset = 108;
        private const int Pe32DataDirectoryOffset = 96;
        private const int Pe32PlusDataDirectoryOffset = 112;
        private const int DataDirectoryEntrySize = 8;
        private const int ClrDirectoryIndex = 14;

        // Subsystem raw values.
        private const ushort SubsystemWindowsGui = 2;
        private const ushort SubsystemWindowsConsole = 3;

        // Sanity limit: real linkers never emit anywhere near this many
        // sections; the PE specification caps the field's usefulness well
        // below it. Zero sections is equally impossible for an image.
        private const int MaximumSections = 96;

        // Upper bound on how much of a stream Inspect(Stream) will read:
        // 1 MiB (1,048,576 bytes). The parser only ever reads header bytes,
        // and a DOS stub, the PE signature, the COFF header, the optional
        // header and the maximum 96 section headers (96 * 40 = 3,840 bytes)
        // fit comfortably inside this window. Anything beyond it is data
        // the parser never reads, so buffering it would only waste memory
        // — and an unbounded read would let a multi-gigabyte or
        // non-terminating stream exhaust the host's memory.
        private const int MaximumHeaderBytes = 1024 * 1024;

        /// <summary>
        /// Inspects an in-memory byte array as a PE image.
        /// </summary>
        /// <param name="data">The complete file contents to inspect.</param>
        /// <returns>The inspection result; invalid input yields
        /// <see cref="PeInfo.IsValid"/> = <c>false</c> with an error.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> is <c>null</c>.</exception>
        public PeInfo Inspect(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return Parse(data);
        }

        /// <summary>
        /// Inspects a stream as a PE image. At most 1 MiB (1,048,576 bytes)
        /// is read from the stream's current position: the parser only ever
        /// reads header bytes, and the DOS stub, PE signature, COFF header,
        /// optional header and the maximum 96 section headers fit
        /// comfortably inside that window, so anything beyond it is data
        /// the parser never reads. Capping the read also means a
        /// multi-gigabyte or non-terminating stream cannot exhaust memory;
        /// if the stream is longer than the window, only the window is
        /// parsed and the existing bounds checks report truncation as a
        /// clean invalid result. No further read is attempted once the
        /// window is full. The stream is <b>not</b> closed or disposed;
        /// ownership stays with the caller.
        /// </summary>
        /// <param name="stream">The stream to read the header window from.</param>
        /// <returns>The inspection result; invalid input yields
        /// <see cref="PeInfo.IsValid"/> = <c>false</c> with an error.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <c>null</c>.</exception>
        public PeInfo Inspect(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            // Read incrementally into a single fixed-size buffer. Stream.Read
            // is allowed to return fewer bytes than requested, so loop until
            // the buffer is full or Read returns 0 (end of stream). When the
            // buffer fills, the loop condition ends the reading with no
            // extra probe read, so exactly MaximumHeaderBytes — never more —
            // are consumed from a longer stream.
            var buffer = new byte[MaximumHeaderBytes];
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                if (read <= 0)
                {
                    break;
                }

                totalRead += read;
            }

            return Parse(buffer, totalRead);
        }

        private static PeInfo Parse(byte[] data)
        {
            return Parse(data, data.Length);
        }

        private static PeInfo Parse(byte[] data, int length)
        {
            // ---- DOS header -------------------------------------------------
            if (length < MinimumDosHeaderSize)
            {
                return PeInfo.Invalid("File is too small to contain a DOS header.");
            }

            if (ReadUInt16(data, 0) != DosSignature)
            {
                return PeInfo.Invalid("Missing DOS 'MZ' signature.");
            }

            // e_lfanew is a signed 32-bit file offset to the PE header. It is
            // attacker-controlled, so validate it as a long before use.
            int peHeaderOffset = ReadInt32(data, LfanewOffset);
            if (peHeaderOffset < 0)
            {
                return PeInfo.Invalid("DOS e_lfanew value is negative.");
            }

            // The PE signature (4 bytes) plus the COFF header (20 bytes)
            // must fit starting at e_lfanew.
            if ((long)peHeaderOffset + 4 + CoffHeaderSize > length)
            {
                return PeInfo.Invalid("DOS e_lfanew points past the end of the file.");
            }

            // ---- PE signature ----------------------------------------------
            if (ReadUInt32(data, peHeaderOffset) != PeSignature)
            {
                return PeInfo.Invalid("Missing PE signature.");
            }

            // ---- COFF header ------------------------------------------------
            int coffOffset = peHeaderOffset + 4;
            ushort machineRaw = ReadUInt16(data, coffOffset + CoffMachineOffset);
            ushort numberOfSections = ReadUInt16(data, coffOffset + CoffNumberOfSectionsOffset);
            ushort sizeOfOptionalHeader = ReadUInt16(data, coffOffset + CoffSizeOfOptionalHeaderOffset);
            ushort characteristics = ReadUInt16(data, coffOffset + CoffCharacteristicsOffset);

            if (numberOfSections == 0 || numberOfSections > MaximumSections)
            {
                return PeInfo.Invalid(
                    "COFF number of sections is out of range.",
                    machineRaw,
                    numberOfSections);
            }

            // ---- Optional header -------------------------------------------
            int optionalOffset = coffOffset + CoffHeaderSize;
            if (sizeOfOptionalHeader < 2)
            {
                return PeInfo.Invalid(
                    "Optional header is missing or too small.",
                    machineRaw,
                    numberOfSections);
            }

            if ((long)optionalOffset + sizeOfOptionalHeader > length)
            {
                return PeInfo.Invalid(
                    "Optional header is truncated.",
                    machineRaw,
                    numberOfSections);
            }

            ushort magic = ReadUInt16(data, optionalOffset);
            bool isPe32Plus;
            if (magic == OptionalMagicPe32)
            {
                isPe32Plus = false;
            }
            else if (magic == OptionalMagicPe32Plus)
            {
                isPe32Plus = true;
            }
            else
            {
                return PeInfo.Invalid(
                    "Unknown optional header magic value.",
                    machineRaw,
                    numberOfSections);
            }

            // The subsystem field sits at offset 68 in both PE32 and PE32+.
            ushort subsystemRaw = 0;
            if (sizeOfOptionalHeader >= OptionalSubsystemOffset + OptionalSubsystemSize)
            {
                subsystemRaw = ReadUInt16(data, optionalOffset + OptionalSubsystemOffset);
            }

            bool isManaged = ReadIsManaged(data, optionalOffset, sizeOfOptionalHeader, isPe32Plus);

            return new PeInfo(
                true,
                null,
                MapMachine(machineRaw),
                (characteristics & ImageFileDll) != 0,
                isManaged,
                MapSubsystem(subsystemRaw),
                subsystemRaw,
                machineRaw,
                isPe32Plus,
                numberOfSections);
        }

        private static bool ReadIsManaged(byte[] data, int optionalOffset, ushort sizeOfOptionalHeader, bool isPe32Plus)
        {
            int numberOfRvaOffset = isPe32Plus ? Pe32PlusNumberOfRvaOffset : Pe32NumberOfRvaOffset;
            int directoryOffset = isPe32Plus ? Pe32PlusDataDirectoryOffset : Pe32DataDirectoryOffset;

            // The NumberOfRvaAndSizes field itself must be inside the
            // declared optional header.
            if (sizeOfOptionalHeader < numberOfRvaOffset + 4)
            {
                return false;
            }

            uint numberOfDirectories = ReadUInt32(data, optionalOffset + numberOfRvaOffset);
            if (numberOfDirectories <= ClrDirectoryIndex)
            {
                return false;
            }

            // The full CLR directory entry (index 14) must be inside the
            // declared optional header. Long arithmetic guards against a
            // hostile NumberOfRvaAndSizes-adjacent overflow; the entry
            // offset itself is a small constant, but the check stays
            // uniform with the rest of the parser.
            long clrEntryEnd = (long)directoryOffset + ((ClrDirectoryIndex + 1) * DataDirectoryEntrySize);
            if (clrEntryEnd > sizeOfOptionalHeader)
            {
                return false;
            }

            int clrEntryOffset = optionalOffset + directoryOffset + (ClrDirectoryIndex * DataDirectoryEntrySize);
            uint clrRva = ReadUInt32(data, clrEntryOffset);
            return clrRva != 0;
        }

        private static ProcessorArchitecture MapMachine(ushort machineRaw)
        {
            switch (machineRaw)
            {
                case MachineI386:
                    return ProcessorArchitecture.X86;
                case MachineAmd64:
                    return ProcessorArchitecture.X64;
                case MachineArm:
                case MachineArmNt:
                    return ProcessorArchitecture.Arm;
                case MachineArm64:
                    return ProcessorArchitecture.Arm64;
                default:
                    // Includes IA64 (0x0200) and every unrecognized value:
                    // the raw value on PeInfo keeps them distinguishable.
                    return ProcessorArchitecture.Unknown;
            }
        }

        private static PeSubsystem MapSubsystem(ushort subsystemRaw)
        {
            switch (subsystemRaw)
            {
                case SubsystemWindowsGui:
                    return PeSubsystem.WindowsGui;
                case SubsystemWindowsConsole:
                    return PeSubsystem.WindowsConsole;
                case 0:
                    return PeSubsystem.Unknown;
                default:
                    return PeSubsystem.Other;
            }
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            // Callers guarantee offset + 2 <= the validated length.
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            // Callers guarantee offset + 4 <= the validated length.
            return data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24);
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            // Callers guarantee offset + 4 <= the validated length.
            return (uint)ReadInt32(data, offset);
        }
    }
}
