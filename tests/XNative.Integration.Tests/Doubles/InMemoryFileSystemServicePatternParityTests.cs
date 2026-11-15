using System;
using System.IO;
using System.Linq;
using XNative.Platform.FileSystem;
using Xunit;

namespace XNative.Integration.Tests.Doubles
{
    /// <summary>
    /// Parity gate between <see cref="InMemoryFileSystemService"/> and the
    /// real <see cref="SystemFileSystemService"/>: for every pattern shape
    /// the codebase can produce, the double's <c>GetFiles</c> must return
    /// exactly the files the real service returns over the same file set.
    /// The real service delegates to <see cref="Directory.GetFiles(string, string)"/>
    /// (Win32 matching), whose DOS-era quirks the double reproduces —
    /// <c>*.*</c> also matches extensionless files, <c>game*.exe</c>
    /// matches by prefix + suffix, a <c>?</c> cannot match a <c>.</c> but
    /// can match zero characters at one, and casing follows the platform
    /// default. Without this gate the integration tests could pass
    /// against pattern behaviour the real service does not have.
    /// </summary>
    public sealed class InMemoryFileSystemServicePatternParityTests : IDisposable
    {
        private static readonly string[] FileNames =
        {
            "game.exe",
            "gameone.exe",
            "game.exe.config",
            "GameTwo.exe",
            "GAME.EXE",
            "g.exe",
            "game",
            "notes.txt",
            "game.txt",
            "tool.executable",
            "abc",
            "a.c",
            "x.ex",
            "x.exe2",
            "ae",
            "ab",
            "a.b.exe",
            "a.b.c",
            "x.exe.exe",
            "a.exe.bxe",
            "foo",
            "foo.txt",
        };

        private readonly string _directory;
        private readonly InMemoryFileSystemService _double;
        private readonly SystemFileSystemService _real = new SystemFileSystemService();

        public InMemoryFileSystemServicePatternParityTests()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "xnative-pattern-parity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);

            _double = new InMemoryFileSystemService();
            foreach (string name in FileNames)
            {
                File.WriteAllText(Path.Combine(_directory, name), string.Empty);
                _double.AddFile(Path.Combine(_directory, name));
            }
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        [Theory]
        [InlineData("*")]
        [InlineData("*.*")]
        [InlineData("*.")]
        [InlineData("*.exe")]
        [InlineData("*.EXE")]
        [InlineData("*.txt")]
        [InlineData("*.config")]
        [InlineData("*.e?e")]
        [InlineData("*.e*")]
        [InlineData("*.*.exe")]
        [InlineData("game.exe")]
        [InlineData("GAME.EXE")]
        [InlineData("gametwo.exe")]
        [InlineData("notes.txt")]
        [InlineData("game.")]
        [InlineData("game*")]
        [InlineData("g*")]
        [InlineData("game*.exe")]
        [InlineData("game.*")]
        [InlineData("game?.exe")]
        [InlineData("game?exe")]
        [InlineData("g?me.exe")]
        [InlineData("a?c")]
        [InlineData("ab?")]
        [InlineData("?")]
        [InlineData(".")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void GetFiles_ReturnsExactlyWhatTheRealServiceReturns(string pattern)
        {
            string[] expected = Names(_real.GetFiles(_directory, pattern));
            string[] actual = Names(_double.GetFiles(_directory, pattern));

            Assert.Equal(expected, actual);
        }

        private static string[] Names(string[] paths)
        {
            return paths
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()!;
        }
    }
}
