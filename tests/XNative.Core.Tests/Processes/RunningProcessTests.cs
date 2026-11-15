using System;
using XNative.Core.Processes;
using Xunit;

namespace XNative.Core.Tests.Processes
{
    public sealed class RunningProcessTests
    {
        private static readonly DateTime StartTimeUtc = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Constructor_ValidValues_StoresEveryProperty()
        {
            var process = new RunningProcess(42, @"C:\Games\Test\game.exe", StartTimeUtc, false, null);

            Assert.Equal(42, process.Id);
            Assert.Equal(@"C:\Games\Test\game.exe", process.FileName);
            Assert.Equal(StartTimeUtc, process.StartTimeUtc);
            Assert.False(process.HasExited);
            Assert.Null(process.ExitCode);
        }

        [Fact]
        public void Constructor_ExitedProcess_StoresExitCode()
        {
            var process = new RunningProcess(7, @"C:\Games\Test\game.exe", StartTimeUtc, true, 3);

            Assert.True(process.HasExited);
            Assert.Equal(3, process.ExitCode);
        }

        [Fact]
        public void Constructor_NullFileName_ThrowsArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new RunningProcess(1, null!, StartTimeUtc, false, null));

            Assert.Equal("fileName", exception.ParamName);
        }

        [Fact]
        public void Constructor_EmptyFileName_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new RunningProcess(1, "", StartTimeUtc, false, null));

            Assert.Equal("fileName", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhitespaceFileName_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new RunningProcess(1, "   ", StartTimeUtc, false, null));

            Assert.Equal("fileName", exception.ParamName);
        }
    }
}
