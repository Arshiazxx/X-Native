using System;
using System.IO;
using Microsoft.Extensions.Options;
using XNative.Core.Logging;
using XNative.Platform.Logging;
using Xunit;

namespace XNative.Platform.Tests.Logging
{
    public sealed class FileLogServiceTests : IDisposable
    {
        private readonly string _root;

        public FileLogServiceTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(),
                "xnative-log-" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }

                if (File.Exists(_root))
                {
                    File.Delete(_root);
                }
            }
            catch (IOException)
            {
            }
        }

        private FileLogService Create(
            LogLevel minimum = LogLevel.Trace,
            string? directory = null,
            long maximumFileSizeBytes = 5 * 1024 * 1024)
        {
            return new FileLogService(Options.Create(new FileLogServiceOptions
            {
                LogDirectory = directory ?? _root,
                MinimumLevel = minimum,
                MaximumFileSizeBytes = maximumFileSizeBytes,
            }));
        }

        [Fact]
        public void Log_WritesFormattedLineWithLevelMessageAndException()
        {
            FileLogService log = Create();

            log.Log(LogLevel.Warning, "disk almost full", new InvalidOperationException("boom"));

            string path = Path.Combine(_root, "xnative.log");
            Assert.True(File.Exists(path));
            string line = File.ReadAllText(path);
            Assert.Contains("[Warning]", line);
            Assert.Contains("disk almost full", line);
            Assert.Contains("InvalidOperationException", line);
            Assert.Contains("boom", line);
            Assert.Empty(log.BufferedEntries);
        }

        [Fact]
        public void Log_BelowMinimumLevel_IsDropped()
        {
            FileLogService log = Create(minimum: LogLevel.Warning);

            Assert.False(log.IsEnabled(LogLevel.Information));
            Assert.True(log.IsEnabled(LogLevel.Error));
            Assert.Equal(LogLevel.Warning, log.MinimumLevel);

            log.Log(LogLevel.Information, "should not appear");

            Assert.False(File.Exists(Path.Combine(_root, "xnative.log")));
            Assert.Empty(log.BufferedEntries);
        }

        [Fact]
        public void Log_NeverThrows_WhenDirectoryIsAnExistingFile()
        {
            // Root path is a FILE, so CreateDirectory / append must fail.
            File.WriteAllText(_root, "not a directory");
            FileLogService log = Create(directory: _root);

            Exception? ex = Record.Exception(
                () => log.Log(LogLevel.Error, "cannot write this"));

            Assert.Null(ex);
            Assert.Single(log.BufferedEntries);
            Assert.Contains("cannot write this", log.BufferedEntries[0]);
        }

        [Theory]
        [InlineData("../evil.log")]
        [InlineData("..\\evil.log")]
        [InlineData("sub/evil.log")]
        [InlineData("sub\\evil.log")]
        [InlineData("a..b.log")]
        public void Constructor_RejectsFileNameWithSeparatorsOrTraversal(string fileName)
        {
            Assert.Throws<ArgumentException>(
                () => new FileLogService(new FileLogServiceOptions
                {
                    LogDirectory = _root,
                    FileName = fileName,
                }));
            Assert.Throws<ArgumentException>(
                () => new FileLogService(Options.Create(new FileLogServiceOptions
                {
                    LogDirectory = _root,
                    FileName = fileName,
                })));
        }

        [Fact]
        public void Constructor_RejectsRootedFileName()
        {
            string rooted = Path.GetFullPath(Path.Combine(_root, "evil.log"));

            Assert.Throws<ArgumentException>(
                () => new FileLogService(new FileLogServiceOptions
                {
                    LogDirectory = _root,
                    FileName = rooted,
                }));
        }

        [Fact]
        public void Constructor_AcceptsPlainFileName_AndLogFilePathCombinesIt()
        {
            var log = new FileLogService(new FileLogServiceOptions
            {
                LogDirectory = _root,
                FileName = "custom.log",
            });

            Assert.Equal(Path.Combine(_root, "custom.log"), log.LogFilePath);
        }

        [Fact]
        public void Log_RotatesToBackup_WhenFileReachesMaximumSize()
        {
            FileLogService log = Create(maximumFileSizeBytes: 200);
            string path = Path.Combine(_root, "xnative.log");
            string backup = Path.Combine(_root, "xnative.1.log");
            string first = new string('a', 100);
            string second = new string('b', 100);
            string third = new string('c', 100);

            log.Log(LogLevel.Information, first);
            log.Log(LogLevel.Information, second);
            Assert.False(File.Exists(backup));

            // The file is now over the threshold, so this write rolls
            // it first: the backup holds the first two entries and the
            // fresh active file holds only the third.
            log.Log(LogLevel.Information, third);

            Assert.True(File.Exists(backup));
            string backupText = File.ReadAllText(backup);
            Assert.Contains(first, backupText);
            Assert.Contains(second, backupText);
            string activeText = File.ReadAllText(path);
            Assert.Contains(third, activeText);
            Assert.DoesNotContain(first, activeText);
            Assert.Empty(log.BufferedEntries);
        }

        [Fact]
        public void Log_RotationKeepsOnlyOneBackup()
        {
            FileLogService log = Create(maximumFileSizeBytes: 200);
            string backup = Path.Combine(_root, "xnative.1.log");
            string first = new string('a', 100);
            string third = new string('c', 100);
            string fifth = new string('e', 100);

            log.Log(LogLevel.Information, first);
            log.Log(LogLevel.Information, new string('b', 100));
            log.Log(LogLevel.Information, third);
            log.Log(LogLevel.Information, new string('d', 100));
            log.Log(LogLevel.Information, fifth);

            // Two rolls happened; exactly the active file and one
            // backup exist, and the backup holds the second
            // generation (third entry), not the first.
            Assert.Equal(2, Directory.GetFiles(_root).Length);
            string backupText = File.ReadAllText(backup);
            Assert.Contains(third, backupText);
            Assert.DoesNotContain(first, backupText);
            Assert.Contains(fifth, File.ReadAllText(Path.Combine(_root, "xnative.log")));
        }

        [Fact]
        public void Log_PreExistingOversizedFile_RollsOnFirstWrite()
        {
            Directory.CreateDirectory(_root);
            string path = Path.Combine(_root, "xnative.log");
            string oldContent = new string('o', 500);
            File.WriteAllText(path, oldContent);
            FileLogService log = Create(maximumFileSizeBytes: 200);

            log.Log(LogLevel.Information, "fresh entry");

            string backupText = File.ReadAllText(Path.Combine(_root, "xnative.1.log"));
            Assert.Contains(oldContent, backupText);
            string activeText = File.ReadAllText(path);
            Assert.Contains("fresh entry", activeText);
            Assert.DoesNotContain(oldContent, activeText);
        }

        [Fact]
        public void Log_DoesNotRotate_WhenMaximumSizeIsZero()
        {
            FileLogService log = Create(maximumFileSizeBytes: 0);

            log.Log(LogLevel.Information, new string('a', 500));
            log.Log(LogLevel.Information, new string('b', 500));

            Assert.False(File.Exists(Path.Combine(_root, "xnative.1.log")));
            Assert.Single(Directory.GetFiles(_root));
        }

        [Fact]
        public void NullLogService_DiscardsEverythingAndNeverThrows()
        {
            var log = new NullLogService();

            Assert.False(log.IsEnabled(LogLevel.Critical));
            Exception? ex = Record.Exception(
                () => log.Log(LogLevel.Critical, "ignored", new Exception("x")));
            Assert.Null(ex);
        }
    }
}
