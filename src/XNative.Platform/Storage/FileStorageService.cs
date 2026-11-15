using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using XNative.Core.Abstractions;

namespace XNative.Platform.Storage
{
    /// <summary>
    /// <see cref="IStorageService"/> backed by one file per key under a
    /// configured root directory. Keys are deliberately restricted to
    /// plain file names: any key containing a directory separator, a
    /// <c>..</c> traversal segment, a rooted path, or only whitespace is
    /// rejected with <see cref="ArgumentException"/> so a caller can
    /// never escape the storage root. Keys that would alias a different
    /// file on Windows are rejected too: reserved DOS device names
    /// (<c>CON</c>, <c>NUL</c>, <c>COM1</c>, …) and names ending in a
    /// dot or a space, which the Windows file system strips. Writes are
    /// atomic: the value is written to a temporary file in the same
    /// directory and then swapped into place.
    /// </summary>
    public sealed class FileStorageService : IStorageService
    {
        private static readonly HashSet<string> ReservedDeviceNames =
            BuildReservedDeviceNames();

        private readonly FileStorageServiceOptions _options;
        private readonly object _writeLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="FileStorageService"/> class.
        /// </summary>
        /// <param name="options">Storage configuration options.</param>
        public FileStorageService(IOptions<FileStorageServiceOptions> options)
        {
            _options = options != null && options.Value != null
                ? options.Value
                : throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileStorageService"/> class
        /// from plain options, for call sites without a DI container.
        /// </summary>
        /// <param name="options">Storage configuration options.</param>
        public FileStorageService(FileStorageServiceOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public async Task<string?> ReadTextAsync(string key, CancellationToken cancellationToken = default)
        {
            string path = GetPathForKey(key);
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(path))
            {
                return null;
            }

            using (var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                // Read in chunks with the token checked per chunk:
                // ReadToEndAsync takes no cancellation token on
                // netstandard2.0, so a single unbounded read would
                // run to completion even after cancellation.
                var builder = new StringBuilder();
                var buffer = new char[4096];
                int read;
                while ((read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    builder.Append(buffer, 0, read);
                }

                return builder.ToString();
            }
        }

        /// <inheritdoc />
        public async Task WriteTextAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            string path = GetPathForKey(key);
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            cancellationToken.ThrowIfCancellationRequested();
            EnsureRoot();

            string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: true))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    await writer.WriteAsync(value).ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Same-directory replace/move is atomic on the volumes
                // this targets; serialise the swap so concurrent writers
                // to the same key cannot interleave it.
                lock (_writeLock)
                {
                    if (File.Exists(path))
                    {
                        File.Replace(tempPath, path, null);
                    }
                    else
                    {
                        File.Move(tempPath, path);
                    }
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (IOException)
                {
                    // A leftover temp file is harmless; the write itself
                    // has either succeeded or already reported its error.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        /// <inheritdoc />
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            string path = GetPathForKey(key);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(File.Exists(path));
        }

        /// <inheritdoc />
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            string path = GetPathForKey(key);
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        private void EnsureRoot()
        {
            if (string.IsNullOrWhiteSpace(_options.RootPath))
            {
                throw new InvalidOperationException(
                    "FileStorageServiceOptions.RootPath must be configured before use.");
            }

            Directory.CreateDirectory(_options.RootPath);
        }

        private string GetPathForKey(string key)
        {
            ValidateKey(key);

            if (string.IsNullOrWhiteSpace(_options.RootPath))
            {
                throw new InvalidOperationException(
                    "FileStorageServiceOptions.RootPath must be configured before use.");
            }

            return Path.Combine(_options.RootPath, key);
        }

        private static void ValidateKey(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Storage key cannot be empty or whitespace.", nameof(key));
            }

            if (Path.IsPathRooted(key))
            {
                throw new ArgumentException("Storage key cannot be a rooted path.", nameof(key));
            }

            if (key.IndexOf('/') >= 0 || key.IndexOf('\\') >= 0
                || key.IndexOf(Path.DirectorySeparatorChar) >= 0
                || key.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                throw new ArgumentException("Storage key cannot contain directory separators.", nameof(key));
            }

            if (key.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                throw new ArgumentException("Storage key cannot contain '..'.", nameof(key));
            }

            if (!string.Equals(Path.GetFileName(key), key, StringComparison.Ordinal))
            {
                throw new ArgumentException("Storage key must be a plain file name.", nameof(key));
            }

            // Windows strips trailing dots and spaces from file names,
            // so such a key would silently address a different file
            // than the one it names.
            if (key.EndsWith(".", StringComparison.Ordinal)
                || key.EndsWith(" ", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Storage key cannot end with a dot or a space.", nameof(key));
            }

            // Reserved DOS device names alias devices instead of files
            // on Windows, with or without an extension ("con",
            // "NUL.txt"). Only COM1-9 / LPT1-9 are reserved; names that
            // merely start alike ("console.log", "com10.txt") are fine.
            string baseName = key;
            int extensionIndex = key.IndexOf('.');
            if (extensionIndex >= 0)
            {
                baseName = key.Substring(0, extensionIndex);
            }

            if (ReservedDeviceNames.Contains(baseName))
            {
                throw new ArgumentException(
                    "Storage key cannot be a reserved device name.", nameof(key));
            }
        }

        private static HashSet<string> BuildReservedDeviceNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CON",
                "PRN",
                "AUX",
                "NUL",
            };

            for (int i = 1; i <= 9; i++)
            {
                names.Add("COM" + i.ToString(CultureInfo.InvariantCulture));
                names.Add("LPT" + i.ToString(CultureInfo.InvariantCulture));
            }

            return names;
        }
    }
}
