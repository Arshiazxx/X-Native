using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Abstractions;

namespace XNative.Platform.FileSystem
{
    /// <summary>
    /// <see cref="IFileSystemService"/> backed by <see cref="System.IO"/>.
    /// Enumeration methods never throw for a missing directory: they
    /// return an empty array, matching the forgiving contract the game
    /// catalog relies on while scanning.
    /// </summary>
    public sealed class SystemFileSystemService : IFileSystemService
    {
        /// <inheritdoc />
        public bool DirectoryExists(string path)
        {
            return !string.IsNullOrEmpty(path) && Directory.Exists(path);
        }

        /// <inheritdoc />
        public bool FileExists(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        /// <inheritdoc />
        public string[] GetFiles(string directory, string pattern)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }

            try
            {
                return Directory.GetFiles(directory, string.IsNullOrEmpty(pattern) ? "*.*" : pattern);
            }
            catch (IOException)
            {
                return Array.Empty<string>();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        /// <inheritdoc />
        public string[] GetDirectories(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }

            try
            {
                return Directory.GetDirectories(directory);
            }
            catch (IOException)
            {
                return Array.Empty<string>();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        /// <inheritdoc />
        public Stream OpenRead(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return File.OpenRead(path);
        }

        /// <inheritdoc />
        public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            cancellationToken.ThrowIfCancellationRequested();

            // netstandard2.0 has no File.ReadAllTextAsync; use streams.
            using (var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true))
            using (var reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }
    }
}
