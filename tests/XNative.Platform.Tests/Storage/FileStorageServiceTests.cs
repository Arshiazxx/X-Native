using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using XNative.Platform.Storage;
using Xunit;

namespace XNative.Platform.Tests.Storage
{
    public sealed class FileStorageServiceTests : IDisposable
    {
        private readonly string _root;
        private readonly FileStorageService _service;

        public FileStorageServiceTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(),
                "xnative-storage-" + Guid.NewGuid().ToString("N"));
            _service = new FileStorageService(
                Options.Create(new FileStorageServiceOptions { RootPath = _root }));
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
        public async Task WriteThenRead_RoundTrips_AndCreatesRootLazily()
        {
            Assert.False(Directory.Exists(_root));

            await _service.WriteTextAsync("settings.json", "{\"a\":1}");
            string? value = await _service.ReadTextAsync("settings.json");

            Assert.Equal("{\"a\":1}", value);
            Assert.True(await _service.ExistsAsync("settings.json"));
        }

        [Fact]
        public async Task Write_OverwritesAtomically_AndLeavesNoTempFiles()
        {
            await _service.WriteTextAsync("k.txt", "old");
            await _service.WriteTextAsync("k.txt", "new-value");

            Assert.Equal("new-value", await _service.ReadTextAsync("k.txt"));
            Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        }

        [Fact]
        public async Task Read_MissingKey_ReturnsNull()
        {
            Assert.Null(await _service.ReadTextAsync("missing.txt"));
            Assert.False(await _service.ExistsAsync("missing.txt"));
        }

        [Fact]
        public async Task Delete_MissingKey_IsNotAnError()
        {
            await _service.DeleteAsync("missing.txt");

            await _service.WriteTextAsync("k.txt", "v");
            await _service.DeleteAsync("k.txt");

            Assert.False(await _service.ExistsAsync("k.txt"));
        }

        [Theory]
        [InlineData("../evil")]
        [InlineData("..")]
        [InlineData("a/b")]
        [InlineData("a\\b")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("a..b")]
        public async Task Write_RejectsTraversalAndMalformedKeys(string key)
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => _service.WriteTextAsync(key, "x"));
            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => _service.ReadTextAsync(key));
        }

        [Fact]
        public async Task Write_RejectsRootedKey()
        {
            string rooted = Path.GetFullPath(Path.Combine(_root, "evil.txt"));

            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => _service.WriteTextAsync(rooted, "x"));
        }

        [Fact]
        public async Task Write_RejectsNullKey()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _service.WriteTextAsync(null!, "x"));
        }
    }
}
