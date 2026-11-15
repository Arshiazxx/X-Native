using System.Threading;
using System.Threading.Tasks;

namespace XNative.Core.Abstractions
{
    /// <summary>
    /// Abstraction over persisted application state such as settings and
    /// container definitions. Implemented in <c>XNative.Platform</c>.
    /// Keys are short, opaque names chosen by the caller (for example
    /// <c>settings.json</c>); the implementation decides where they live
    /// on the running platform and is responsible for keeping them
    /// inside its own storage area. Values are text; callers serialise
    /// richer state themselves.
    /// </summary>
    public interface IStorageService
    {
        /// <summary>
        /// Reads the value stored under <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The key to read.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The stored text, or <c>null</c> when the key has never been written.</returns>
        Task<string?> ReadTextAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores <paramref name="value"/> under <paramref name="key"/>,
        /// replacing any previous value. Writes are atomic from the
        /// reader's point of view: a reader sees the old value or the
        /// new one, never a mixture.
        /// </summary>
        /// <param name="key">The key to write.</param>
        /// <param name="value">The text to store.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the write.</returns>
        Task WriteTextAsync(string key, string value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reports whether a value exists under <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The key to test.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns><c>true</c> when a value exists.</returns>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the value stored under <paramref name="key"/>.
        /// Deleting a key that does not exist is not an error.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the delete.</returns>
        Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    }
}
