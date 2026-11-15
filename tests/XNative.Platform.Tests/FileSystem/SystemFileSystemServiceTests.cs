using System;
using System.IO;
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
    }
}
