using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XNative.Platform.FileSystem;
using Xunit;

namespace XNative.Platform.Tests.FileSystem
{
    public sealed class SystemFileSystemServiceTests : IDisposable
    {
        private readonly string _root;
        private readonly SystemFileSystemService _service = new SystemFileSystemService();

        public SystemFileSystemServiceTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(),
                "xnative-fs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public async Task ReadAllTextAsync_RoundTripsWrittenText()
        {
            string path = Path.Combine(_root, "hello.txt");
            File.WriteAllText(path, "hello x-native");

            string text = await _service.ReadAllTextAsync(path);

            Assert.Equal("hello x-native", text);
        }

        [Fact]
        public async Task ReadAllTextAsync_RoundTripsLargeTextAcrossChunks()
        {
            string path = Path.Combine(_root, "large.txt");
            string expected = new string('é', 5000) + new string('x', 5000);
            File.WriteAllText(path, expected);

            string text = await _service.ReadAllTextAsync(path);

            Assert.Equal(expected, text);
        }

        [Fact]
        public async Task ReadAllTextAsync_PreCancelledToken_Throws()
        {
            string path = Path.Combine(_root, "cancel.txt");
            File.WriteAllText(path, "some text");

            using (var source = new CancellationTokenSource())
            {
                source.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => _service.ReadAllTextAsync(path, source.Token));
            }
        }

        [Fact]
        public void OpenRead_RoundTripsWrittenBytes()
        {
            string path = Path.Combine(_root, "bytes.bin");
            byte[] expected = { 0x4D, 0x5A, 0x00, 0x01, 0xFE, 0xFF };
            File.WriteAllBytes(path, expected);

            using (var stream = _service.OpenRead(path))
            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);

                Assert.Equal(expected, buffer.ToArray());
            }
        }

        [Fact]
        public void ExistsChecks_ReflectDisk()
        {
            string path = Path.Combine(_root, "a.txt");
            Assert.False(_service.FileExists(path));
            Assert.True(_service.DirectoryExists(_root));
            Assert.False(_service.DirectoryExists(path));

            File.WriteAllText(path, "x");

            Assert.True(_service.FileExists(path));
        }

        [Fact]
        public void GetFilesAndDirectories_ReturnMatches()
        {
            File.WriteAllText(Path.Combine(_root, "one.exe"), "x");
            File.WriteAllText(Path.Combine(_root, "two.txt"), "x");
            Directory.CreateDirectory(Path.Combine(_root, "sub"));

            Assert.Single(_service.GetFiles(_root, "*.exe"));
            Assert.Equal(2, _service.GetFiles(_root, "*.*").Length);
            Assert.Single(_service.GetDirectories(_root));
        }

        [Fact]
        public void GetFiles_ReturnsEmpty_WhenDirectoryMissing()
        {
            string missing = Path.Combine(_root, "nope");

            Assert.Empty(_service.GetFiles(missing, "*.exe"));
            Assert.Empty(_service.GetDirectories(missing));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetFiles_ReturnsEmpty_WhenPatternIsBlank(string pattern)
        {
            // Both files would match "*.*" — the pattern a blank
            // pattern used to be silently widened to. A blank pattern
            // must match nothing instead.
            File.WriteAllText(Path.Combine(_root, "one.exe"), "x");
            File.WriteAllText(Path.Combine(_root, "two.txt"), "x");

            Assert.Empty(_service.GetFiles(_root, pattern));
        }
    }
}
