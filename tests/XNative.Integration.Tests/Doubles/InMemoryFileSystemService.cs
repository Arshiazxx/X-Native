using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Abstractions;

namespace XNative.Integration.Tests.Doubles
{
    /// <summary>
    /// Hand-rolled, dictionary-backed <see cref="IFileSystemService"/> double.
    /// Paths are normalised with <see cref="Path.GetFullPath(string)"/> so that
    /// <c>..</c> segments and rooted manifest paths resolve exactly the way a
    /// real file system would resolve them — that fidelity is what makes the
    /// manifest path-traversal gate tests meaningful. Adding a file implicitly
    /// registers every parent directory, like a real tree.
    /// </summary>
    public sealed class InMemoryFileSystemService : IFileSystemService
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _directories = new HashSet<string>(StringComparer.Ordinal);

        public InMemoryFileSystemService AddFile(string path, string content = "")
        {
            string full = Normalize(path);
            _files[full] = content;

            string? parent = Path.GetDirectoryName(full);
            while (!string.IsNullOrEmpty(parent))
            {
                _directories.Add(parent);
                parent = Path.GetDirectoryName(parent);
            }

            return this;
        }

        public InMemoryFileSystemService AddDirectory(string path)
        {
            _directories.Add(Normalize(path));
            return this;
        }

        public bool DirectoryExists(string path)
        {
            return _directories.Contains(Normalize(path));
        }

        public bool FileExists(string path)
        {
            return _files.ContainsKey(Normalize(path));
        }

        public string[] GetFiles(string directory, string pattern)
        {
            string dir = Normalize(directory);
            return _files.Keys
                .Where(file => string.Equals(Path.GetDirectoryName(file), dir, StringComparison.Ordinal))
                .Where(file => MatchesPattern(Path.GetFileName(file), pattern))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToArray();
        }

        public string[] GetDirectories(string directory)
        {
            string dir = Normalize(directory);
            return _directories
                .Where(candidate => string.Equals(Path.GetDirectoryName(candidate), dir, StringComparison.Ordinal))
                .OrderBy(candidate => candidate, StringComparer.Ordinal)
                .ToArray();
        }

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string full = Normalize(path);
            if (!_files.TryGetValue(full, out string? content))
            {
                // FileNotFoundException is an IOException, matching what a real
                // file system surfaces for a missing file.
                throw new FileNotFoundException("File not found in the in-memory file system.", full);
            }

            return Task.FromResult(content);
        }

        private static string Normalize(string path)
        {
            return Path.GetFullPath(path);
        }

        private static bool MatchesPattern(string fileName, string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || pattern == "*")
            {
                return true;
            }

            if (pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                string extension = pattern.Substring(1); // ".exe"
                return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
            }

            if (pattern.StartsWith("*", StringComparison.Ordinal))
            {
                return fileName.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);
        }
    }
}
