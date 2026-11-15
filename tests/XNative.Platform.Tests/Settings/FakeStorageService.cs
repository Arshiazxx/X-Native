using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Abstractions;

namespace XNative.Platform.Tests.Settings
{
    /// <summary>
    /// Hand-rolled in-memory <see cref="IStorageService"/> fake that
    /// counts reads and writes so tests can prove cache behaviour and
    /// that failed saves write nothing.
    /// </summary>
    public sealed class FakeStorageService : IStorageService
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        public int ReadCount { get; private set; }

        public int WriteCount { get; private set; }

        public int DeleteCount { get; private set; }

        public void Seed(string key, string value)
        {
            _values[key] = value;
        }

        public string? Peek(string key)
        {
            return _values.TryGetValue(key, out string? value) ? value : null;
        }

        public Task<string?> ReadTextAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            return Task.FromResult(Peek(key));
        }

        public Task WriteTextAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCount++;
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_values.ContainsKey(key));
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeleteCount++;
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
