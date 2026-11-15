using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Abstractions;

namespace XNative.Wine.Tests.Fakes
{
    public sealed class FakeFileSystemService : IFileSystemService
    {
        private readonly HashSet<string> _files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public FakeFileSystemService(params string[] existingFiles)
        {
            foreach (string file in existingFiles)
            {
                _files.Add(file);
            }
        }

        public bool DirectoryExists(string path)
        {
            return false;
        }

        public bool FileExists(string path)
        {
            return _files.Contains(path);
        }

        public string[] GetFiles(string directory, string pattern)
        {
            return new string[0];
        }

        public string[] GetDirectories(string directory)
        {
            return new string[0];
        }

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(string.Empty);
        }
    }
}
