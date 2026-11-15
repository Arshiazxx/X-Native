using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XNative.Core.Abstractions
{
    /// <summary>
    /// Abstraction over the file system reachable by the host process.
    /// Implemented in <c>XNative.Platform</c> so that the core stays free of
    /// any assumption about the sandbox rules of the running platform.
    /// </summary>
    public interface IFileSystemService
    {
        /// <summary>
        /// Checks if a directory exists.
        /// </summary>
        /// <param name="path">The directory path.</param>
        /// <returns><c>true</c> if the directory exists; otherwise <c>false</c>.</returns>
        bool DirectoryExists(string path);

        /// <summary>
        /// Checks if a file exists.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns><c>true</c> if the file exists; otherwise <c>false</c>.</returns>
        bool FileExists(string path);

        /// <summary>
        /// Gets files matching a pattern in a directory.
        /// </summary>
        /// <param name="directory">The directory to search.</param>
        /// <param name="pattern">The search pattern (e.g., "*.exe").</param>
        /// <returns>An array of matching file paths.</returns>
        string[] GetFiles(string directory, string pattern);

        /// <summary>
        /// Gets subdirectories in a directory.
        /// </summary>
        /// <param name="directory">The directory to search.</param>
        /// <returns>An array of subdirectory paths.</returns>
        string[] GetDirectories(string directory);

        /// <summary>
        /// Opens a file for reading.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>A read-only stream over the file contents. The caller
        /// owns the returned stream and is responsible for disposing it.</returns>
        Stream OpenRead(string path);

        /// <summary>
        /// Reads all text from a file asynchronously.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The file contents as a string.</returns>
        Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    }
}