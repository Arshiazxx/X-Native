using System;
using System.IO;
using System.Text;
using XNative.Core.Devices;
using XNative.Platform.Executables;
using Xunit;

namespace XNative.Platform.Tests.Executables
{
    public sealed class PeFileInspectorTests
    {
        private readonly PeFileInspector _inspector = new PeFileInspector();

        // ---- Valid images ---------------------------------------------------

        [Fact]
        public void Inspect_Pe32PlusX64GuiExe_ReturnsFullInfo()
        {
            var info = _inspector.Inspect(SyntheticPe.X64GuiExe());

            Assert.True(info.IsValid);
            Assert.Null(info.Error);
            Assert.Equal(ProcessorArchitecture.X64, info.Architecture);
            Assert.False(info.IsDll);
            Assert.False(info.IsManaged);
            Assert.Equal(PeSubsystem.WindowsGui, info.Subsystem);
            Assert.Equal((ushort)2, info.SubsystemRaw);
            Assert.Equal(SyntheticPe.MachineX64, info.MachineRaw);
            Assert.Equal(true, info.IsPe32Plus);
            Assert.Equal((ushort)1, info.NumberOfSections);
        }

        [Fact]
        public void Inspect_Pe32X86ConsoleExe_ReturnsFullInfo()
        {
            var info = _inspector.Inspect(SyntheticPe.X86ConsoleExe());

            Assert.True(info.IsValid);
            Assert.Null(info.Error);
            Assert.Equal(ProcessorArchitecture.X86, info.Architecture);
            Assert.False(info.IsDll);
            Assert.False(info.IsManaged);
            Assert.Equal(PeSubsystem.WindowsConsole, info.Subsystem);
            Assert.Equal((ushort)3, info.SubsystemRaw);
            Assert.Equal(SyntheticPe.MachineX86, info.MachineRaw);
            Assert.Equal(false, info.IsPe32Plus);
        }

        [Fact]
        public void Inspect_X64Dll_SetsIsDll()
        {
            var info = _inspector.Inspect(SyntheticPe.X64Dll());

            Assert.True(info.IsValid);
            Assert.True(info.IsDll);
            Assert.Equal(ProcessorArchitecture.X64, info.Architecture);
        }

        [Fact]
        public void Inspect_ManagedExe_SetsIsManaged()
        {
            var info = _inspector.Inspect(SyntheticPe.ManagedX64Exe());

            Assert.True(info.IsValid);
            Assert.True(info.IsManaged);
            Assert.False(info.IsDll);
        }

        [Fact]
        public void Inspect_ManagedPe32Exe_SetsIsManaged()
        {
            var data = SyntheticPe.Create()
                .WithMachine(SyntheticPe.MachineX86)
                .WithPe32()
                .WithClrDirectory(0x2000, 0x48)
                .Build();

            var info = _inspector.Inspect(data);

            Assert.True(info.IsValid);
            Assert.True(info.IsManaged);
            Assert.Equal(false, info.IsPe32Plus);
        }

        [Fact]
        public void Inspect_NativeExeWithClrRvaZero_IsNotManaged()
        {
            var data = SyntheticPe.Create().WithClrDirectory(0, 0).Build();

            var info = _inspector.Inspect(data);

            Assert.True(info.IsValid);
            Assert.False(info.IsManaged);
        }

        [Fact]
        public void Inspect_ClrEntryBeyondDeclaredDirectoryCount_IsNotManaged()
        {
            // The CLR bytes are physically present, but the header declares
            // only 14 data directories, so index 14 does not exist.
            var data = SyntheticPe.Create()
                .WithClrDirectory(0x2000, 0x48)
                .WithNumberOfRvaAndSizes(14)
                .Build();

            var info = _inspector.Inspect(data);

            Assert.True(info.IsValid);
            Assert.False(info.IsManaged);
        }

        [Fact]
        public void Inspect_Arm64Exe_ReturnsArm64()
        {
            var info = _inspector.Inspect(SyntheticPe.Arm64Exe());

            Assert.True(info.IsValid);
            Assert.Equal(ProcessorArchitecture.Arm64, info.Architecture);
            Assert.Equal(SyntheticPe.MachineArm64, info.MachineRaw);
        }

        [Theory]
        [InlineData(SyntheticPe.MachineArm)]
        [InlineData(SyntheticPe.MachineArmNt)]
        public void Inspect_ArmMachines_ReturnArm(ushort machine)
        {
            var data = SyntheticPe.Create().WithMachine(machine).WithPe32().Build();

            var info = _inspector.Inspect(data);

            Assert.True(info.IsValid);
            Assert.Equal(ProcessorArchitecture.Arm, info.Architecture);
            Assert.Equal(machine, info.MachineRaw);
        }

        [Fact]
        public void Inspect_Ia64Machine_IsValidWithUnknownArchitectureAndRawKept()
        {
            var data = SyntheticPe.Create().WithMachine(SyntheticPe.MachineIa64).Build();

            var info = _inspector.Inspect(data);

            Assert.True(info.IsValid);
            Assert.Equal(ProcessorArchitecture.Unknown, info.Architecture);
            Assert.Equal(SyntheticPe.MachineIa64, info.MachineRaw);
        }

        [Fact]
        public void Inspect_UnrecognizedMachine_IsValidWithUnknownArchitectureAndRawKept()
        {
            var data = SyntheticPe.Create().WithMachine(0x9999).Build();

            var info = _inspector.Inspect(data);

            Assert.True(info.IsValid);
            Assert.Equal(ProcessorArchitecture.Unknown, info.Architecture);
            Assert.Equal((ushort)0x9999, info.MachineRaw);
        }

        [Fact]
        public void Inspect_NativeSubsystem_MapsToOtherWithRawKept()
        {
            var data = SyntheticPe.Create().WithSubsystem(1).Build();

            var info = _inspector.Inspect(data);

            Assert.True(info.IsValid);
            Assert.Equal(PeSubsystem.Other, info.Subsystem);
            Assert.Equal((ushort)1, info.SubsystemRaw);
        }

        [Fact]
        public void Inspect_ZeroSubsystem_MapsToUnknown()
        {
            var data = SyntheticPe.Create().WithSubsystem(0).Build();

            var info = _inspector.Inspect(data);

            Assert.True(info.IsValid);
            Assert.Equal(PeSubsystem.Unknown, info.Subsystem);
            Assert.Equal((ushort)0, info.SubsystemRaw);
        }

        [Fact]
        public void Inspect_ManySectionsWithinLimit_IsValid()
        {
            var data = SyntheticPe.Create().WithNumberOfSections(96).Build();

            var info = _inspector.Inspect(data);

            Assert.True(info.IsValid);
            Assert.Equal((ushort)96, info.NumberOfSections);
        }

        // ---- Stream overload --------------------------------------------------

        [Fact]
        public void Inspect_Stream_ReturnsSameInfoAndLeavesStreamOpen()
        {
            var data = SyntheticPe.ManagedX64Exe();
            using (var stream = new MemoryStream(data))
            {
                var info = _inspector.Inspect(stream);

                Assert.True(info.IsValid);
                Assert.True(info.IsManaged);
                Assert.Equal(ProcessorArchitecture.X64, info.Architecture);

                // The inspector must not close the caller's stream.
                Assert.True(stream.CanRead);
                Assert.Equal(data.Length, stream.Length);
            }
        }

        [Fact]
        public void Inspect_StreamWithInvalidContent_ReturnsInvalid()
        {
            using (var stream = new MemoryStream(Encoding.ASCII.GetBytes("not a pe file, just text")))
            {
                var info = _inspector.Inspect(stream);

                Assert.False(info.IsValid);
                Assert.False(string.IsNullOrEmpty(info.Error));
            }
        }

        // ---- Null arguments ---------------------------------------------------

        [Fact]
        public void Inspect_NullArray_ThrowsArgumentNullException()
        {
            byte[] data = null!;
            Assert.Throws<ArgumentNullException>(() => _inspector.Inspect(data));
        }

        [Fact]
        public void Inspect_NullStream_ThrowsArgumentNullException()
        {
            Stream stream = null!;
            Assert.Throws<ArgumentNullException>(() => _inspector.Inspect(stream));
        }

        // ---- Malformed input: never throws, always invalid --------------------

        [Fact]
        public void Inspect_EmptyArray_IsInvalid()
        {
            var info = _inspector.Inspect(Array.Empty<byte>());

            AssertInvalid(info);
        }

        [Fact]
        public void Inspect_PlainText_IsInvalid()
        {
            var info = _inspector.Inspect(Encoding.ASCII.GetBytes("This is a plain text file, definitely not an executable."));

            AssertInvalid(info);
        }

        [Fact]
        public void Inspect_AllZeroBytes_IsInvalid()
        {
            var info = _inspector.Inspect(new byte[512]);

            AssertInvalid(info);
        }

        [Fact]
        public void Inspect_MzOnlyTruncated_IsInvalid()
        {
            var data = new byte[32];
            data[0] = (byte)'M';
            data[1] = (byte)'Z';

            var info = _inspector.Inspect(data);

            AssertInvalid(info);
        }

        [Fact]
        public void Inspect_BadPeSignature_IsInvalid()
        {
            var data = SyntheticPe.X64GuiExe();
            data[SyntheticPe.PeHeaderOffset] = (byte)'X';

            var info = _inspector.Inspect(data);

            AssertInvalid(info);
        }

        [Fact]
        public void Inspect_LfanewPointingPastEndOfFile_IsInvalid()
        {
            var data = SyntheticPe.X64GuiExe();
            WriteInt32(data, 0x3C, data.Length + 0x1000);

            var info = _inspector.Inspect(data);

            AssertInvalid(info);
        }

        [Fact]
        public void Inspect_LfanewAtIntMax_IsInvalidAndDoesNotThrow()
        {
            var data = SyntheticPe.X64GuiExe();
            WriteInt32(data, 0x3C, int.MaxValue);

            var info = _inspector.Inspect(data);

            AssertInvalid(info);
        }

        [Fact]
        public void Inspect_LfanewNegative_IsInvalid()
        {
            var data = SyntheticPe.X64GuiExe();
            WriteInt32(data, 0x3C, -1);

            var info = _inspector.Inspect(data);

            AssertInvalid(info);
        }

        [Fact]
        public void Inspect_ZeroSections_IsInvalid()
        {
            var data = SyntheticPe.Create().WithNumberOfSections(0).Build();

            var info = _inspector.Inspect(data);

            AssertInvalid(info);
            Assert.Equal(SyntheticPe.MachineX64, info.MachineRaw);
        }

        [Fact]
        public void Inspect_TooManySections_IsInvalid()
        {
            var data = SyntheticPe.Create().WithNumberOfSections(97).Build();

            var info = _inspector.Inspect(data);

            AssertInvalid(info);
        }

        [Fact]
        public void Inspect_TruncatedOptionalHeader_IsInvalid()
        {
            var data = SyntheticPe.X64GuiExe();
            int optionalOffset = SyntheticPe.PeHeaderOffset + 4 + 20;
            var truncated = new byte[optionalOffset + 100];
            Array.Copy(data, truncated, truncated.Length);

            var info = _inspector.Inspect(truncated);

            AssertInvalid(info);
        }

        [Fact]
        public void Inspect_TruncatedInsideCoffHeader_IsInvalid()
        {
            var data = SyntheticPe.X64GuiExe();
            var truncated = new byte[SyntheticPe.PeHeaderOffset + 10];
            Array.Copy(data, truncated, truncated.Length);

            var info = _inspector.Inspect(truncated);

            AssertInvalid(info);
        }

        [Fact]
        public void Inspect_UnknownOptionalHeaderMagic_IsInvalid()
        {
            var data = SyntheticPe.X64GuiExe();
            int optionalOffset = SyntheticPe.PeHeaderOffset + 4 + 20;
            data[optionalOffset] = 0x99;
            data[optionalOffset + 1] = 0x09;

            var info = _inspector.Inspect(data);

            AssertInvalid(info);
        }

        [Fact]
        public void Inspect_MissingOptionalHeader_IsInvalid()
        {
            var data = SyntheticPe.Create().WithSizeOfOptionalHeader(0).Build();

            var info = _inspector.Inspect(data);

            AssertInvalid(info);
        }

        private static void AssertInvalid(PeInfo info)
        {
            Assert.False(info.IsValid);
            Assert.False(string.IsNullOrEmpty(info.Error));
            Assert.Equal(ProcessorArchitecture.Unknown, info.Architecture);
            Assert.False(info.IsDll);
            Assert.False(info.IsManaged);
        }

        private static void WriteInt32(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
