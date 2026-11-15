using System;
using XNative.Core.Devices;
using XNative.Core.Games;
using XNative.Platform.Executables;
using Xunit;

namespace XNative.Platform.Tests.Executables
{
    public sealed class ExecutableClassifierTests
    {
        private readonly PeFileInspector _inspector = new PeFileInspector();

        [Fact]
        public void Classify_ValidX64Exe_ReturnsWindowsExecutableAndX64()
        {
            var info = _inspector.Inspect(SyntheticPe.X64GuiExe());

            var (executableType, architecture) = ExecutableClassifier.Classify(info);

            Assert.Equal(ExecutableType.WindowsExecutable, executableType);
            Assert.Equal(ProcessorArchitecture.X64, architecture);
        }

        [Fact]
        public void Classify_ValidX86Exe_ReturnsWindowsExecutableAndX86()
        {
            var info = _inspector.Inspect(SyntheticPe.X86ConsoleExe());

            var (executableType, architecture) = ExecutableClassifier.Classify(info);

            Assert.Equal(ExecutableType.WindowsExecutable, executableType);
            Assert.Equal(ProcessorArchitecture.X86, architecture);
        }

        [Fact]
        public void Classify_ValidArm64Exe_ReturnsWindowsExecutableAndArm64()
        {
            var info = _inspector.Inspect(SyntheticPe.Arm64Exe());

            var (executableType, architecture) = ExecutableClassifier.Classify(info);

            Assert.Equal(ExecutableType.WindowsExecutable, executableType);
            Assert.Equal(ProcessorArchitecture.Arm64, architecture);
        }

        [Fact]
        public void Classify_ValidDll_ReturnsWindowsExecutable()
        {
            // ExecutableType has no DLL member: a valid PE library still
            // classifies as a Windows executable image with its architecture.
            var info = _inspector.Inspect(SyntheticPe.X64Dll());

            var (executableType, architecture) = ExecutableClassifier.Classify(info);

            Assert.Equal(ExecutableType.WindowsExecutable, executableType);
            Assert.Equal(ProcessorArchitecture.X64, architecture);
        }

        [Fact]
        public void Classify_ValidUnknownMachine_ReturnsWindowsExecutableAndUnknown()
        {
            var info = _inspector.Inspect(SyntheticPe.Create().WithMachine(SyntheticPe.MachineIa64).Build());

            var (executableType, architecture) = ExecutableClassifier.Classify(info);

            Assert.Equal(ExecutableType.WindowsExecutable, executableType);
            Assert.Equal(ProcessorArchitecture.Unknown, architecture);
        }

        [Fact]
        public void Classify_InvalidInfo_ReturnsUnknownAndUnknown()
        {
            var info = _inspector.Inspect(Array.Empty<byte>());

            var (executableType, architecture) = ExecutableClassifier.Classify(info);

            Assert.Equal(ExecutableType.Unknown, executableType);
            Assert.Equal(ProcessorArchitecture.Unknown, architecture);
        }

        [Fact]
        public void Classify_Null_ThrowsArgumentNullException()
        {
            PeInfo info = null!;
            Assert.Throws<ArgumentNullException>(() => ExecutableClassifier.Classify(info));
        }
    }
}
