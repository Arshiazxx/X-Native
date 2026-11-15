using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using XNative.Core.Abstractions;
using XNative.Core.Games;

namespace XNative.Platform.Games
{
    /// <summary>
    /// Configuration options for the local game catalog.
    /// </summary>
    public sealed class LocalGameCatalogOptions
    {
        /// <summary>
        /// The root directory to scan for games.
        /// </summary>
        public string RootPath { get; set; } = string.Empty;

        /// <summary>
        /// File patterns to consider as game executables.
        /// </summary>
        public string[] ExecutablePatterns { get; set; } = { "*.exe" };

        /// <summary>
        /// Maximum directory depth to scan, counted in directory levels
        /// below <see cref="RootPath"/>. The root directory itself is
        /// depth 0, so a game stored directly in <see cref="RootPath"/>
        /// is at depth 0 and is found whenever <see cref="MaxDepth"/> is
        /// zero or greater; a game in an immediate child directory is at
        /// depth 1, and so on. Directories whose depth is greater than
        /// <see cref="MaxDepth"/> are not entered, so games deeper than
        /// <see cref="MaxDepth"/> are not found.
        /// </summary>
        public int MaxDepth { get; set; } = 3;
    }

    /// <summary>
    /// Represents a game manifest file (game.json).
    /// </summary>
    public sealed class GameManifest
    {
        /// <summary>
        /// Display name of the game.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Executable file name relative to the manifest directory.
        /// </summary>
        public string? Executable { get; set; }

        /// <summary>
        /// Optional working directory relative to the manifest directory.
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// Optional description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Optional publisher.
        /// </summary>
        public string? Publisher { get; set; }

        /// <summary>
        /// Optional version.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Optional default command line arguments for the game
        /// (manifest v2). Recorded on the resulting
        /// <see cref="GameInfo.LaunchArguments"/>.
        /// </summary>
        public string? Arguments { get; set; }

        /// <summary>
        /// Optional icon file path relative to the manifest directory
        /// (manifest v2). Recorded on the resulting
        /// <see cref="GameMetadata.IconPath"/> only when the resolved
        /// path stays inside the manifest directory tree and the file
        /// exists; otherwise it is ignored.
        /// </summary>
        public string? Icon { get; set; }
    }

    /// <summary>
    /// Local file system based game catalog implementation.
    /// </summary>
    /// <remarks>
    /// Game identifiers are stable across processes and restarts: they
    /// are derived from an FNV-1a 64-bit hash of the normalized full
    /// executable path, never from <see cref="object.GetHashCode"/>
    /// (which is randomized per process for strings). Refreshes are
    /// single-flight and the published cache is an immutable snapshot,
    /// so concurrent readers always observe a consistent catalog.
    /// </remarks>
    public sealed class LocalGameCatalog : IGameCatalog, IDisposable
    {
        private readonly LocalGameCatalogOptions _options;
        private readonly IFileSystemService _fileSystem;
        private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private volatile IReadOnlyCollection<GameInfo>? _cachedGames;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalGameCatalog"/> class.
        /// </summary>
        /// <param name="options">Catalog configuration options.</param>
        /// <param name="fileSystem">File system abstraction.</param>
        public LocalGameCatalog(IOptions<LocalGameCatalogOptions> options, IFileSystemService fileSystem)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>
        /// Releases the synchronization resources held by the catalog.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the catalog and,
        /// when <paramref name="disposing"/> is <c>true</c>, its managed
        /// resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed
        /// and unmanaged resources; <c>false</c> to release only
        /// unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshLock.Dispose();
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<GameInfo>> GetGamesAsync(CancellationToken cancellationToken = default)
        {
            var cached = _cachedGames;
            if (cached != null)
            {
                return cached;
            }

            await RefreshAsync(cancellationToken);
            return _cachedGames ?? Array.Empty<GameInfo>();
        }

        /// <inheritdoc />
        public async Task<GameInfo?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var games = await GetGamesAsync(cancellationToken);
            return games.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc />
        public async Task<GameInfo?> FindByExecutablePathAsync(string executablePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(executablePath))
            {
                return null;
            }

            var games = await GetGamesAsync(cancellationToken);
            return games.FirstOrDefault(g => string.Equals(g.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc />
        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                var games = new List<GameInfo>();

                if (!string.IsNullOrWhiteSpace(_options.RootPath)
                    && _fileSystem.DirectoryExists(_options.RootPath))
                {
                    await ScanDirectoryAsync(_options.RootPath, 0, games, cancellationToken);
                }

                // Publish a fully built, immutable snapshot in a single
                // volatile write so concurrent readers never observe a
                // partially updated cache.
                _cachedGames = games.ToArray();
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private async Task ScanDirectoryAsync(string directory, int depth, List<GameInfo> games, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (depth > _options.MaxDepth)
            {
                return;
            }

            // First, check for a game.json manifest. A failure here (for
            // example the directory vanishing mid-scan) must not prevent
            // the executable scan or the recursion below.
            bool manifestProcessed = false;
            try
            {
                var manifestPath = Path.Combine(directory, "game.json");
                if (_fileSystem.FileExists(manifestPath))
                {
                    var gameInfo = await TryCreateGameFromManifestAsync(manifestPath, directory, cancellationToken);
                    if (gameInfo != null)
                    {
                        AddIfNew(games, gameInfo);
                        manifestProcessed = true;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip manifests we can't access.
            }
            catch (IOException)
            {
                // Skip manifests with IO errors.
            }

            // Then scan for executables, but skip if a manifest was
            // successfully processed. Each pattern is attempted
            // independently so one failing pattern does not lose the rest.
            if (!manifestProcessed)
            {
                foreach (var pattern in _options.ExecutablePatterns)
                {
                    string[] executables;
                    try
                    {
                        executables = _fileSystem.GetFiles(directory, pattern);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }
                    catch (IOException)
                    {
                        continue;
                    }

                    if (executables == null)
                    {
                        continue;
                    }

                    foreach (var exePath in executables)
                    {
                        var gameInfo = CreateGameFromExecutable(exePath);
                        if (gameInfo != null)
                        {
                            AddIfNew(games, gameInfo);
                        }
                    }
                }
            }

            // Recurse into subdirectories. A failure to enumerate them
            // (for example the directory vanishing mid-scan) ends this
            // branch of the scan instead of failing the whole refresh.
            string[] subDirs;
            try
            {
                subDirs = _fileSystem.GetDirectories(directory);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }

            if (subDirs == null)
            {
                return;
            }

            foreach (var subDir in subDirs)
            {
                await ScanDirectoryAsync(subDir, depth + 1, games, cancellationToken);
            }
        }

        private async Task<GameInfo?> TryCreateGameFromManifestAsync(string manifestPath, string directory, CancellationToken cancellationToken)
        {
            try
            {
                var json = await _fileSystem.ReadAllTextAsync(manifestPath, cancellationToken);
                var manifest = JsonSerializer.Deserialize<GameManifest>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.Executable))
                {
                    return null;
                }

                // Path traversal guard: the resolved executable must stay
                // inside the manifest's own directory tree.
                var exePath = Path.Combine(directory, manifest.Executable);
                if (!IsPathInsideDirectory(exePath, directory))
                {
                    return null;
                }

                if (!_fileSystem.FileExists(exePath))
                {
                    return null;
                }

                var workingDir = string.IsNullOrWhiteSpace(manifest.WorkingDirectory)
                    ? directory
                    : Path.Combine(directory, manifest.WorkingDirectory);

                // The working directory is held to the same guard.
                if (!IsPathInsideDirectory(workingDir, directory))
                {
                    return null;
                }

                var iconPath = ResolveIconPath(manifest, directory);

                var metadata = new GameMetadata(
                    manifest.Description,
                    manifest.Publisher,
                    manifest.Version,
                    null,
                    iconPath);

                var name = manifest.Name ?? Path.GetFileNameWithoutExtension(manifest.Executable);
                var id = GenerateId(name, exePath);
                return new GameInfo(
                    id,
                    name,
                    exePath,
                    workingDir,
                    ExecutableType.WindowsExecutable,
                    metadata,
                    launchArguments: string.IsNullOrWhiteSpace(manifest.Arguments) ? null : manifest.Arguments);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        private string? ResolveIconPath(GameManifest manifest, string directory)
        {
            if (string.IsNullOrWhiteSpace(manifest.Icon))
            {
                return null;
            }

            var iconPath = Path.Combine(directory, manifest.Icon);

            // Same traversal guard as the executable: an icon outside the
            // manifest's directory tree is ignored rather than recorded.
            if (!IsPathInsideDirectory(iconPath, directory))
            {
                return null;
            }

            // Recorded only when the file actually exists; a missing icon
            // never invalidates the game itself.
            return _fileSystem.FileExists(iconPath) ? iconPath : null;
        }

        private GameInfo? CreateGameFromExecutable(string exePath)
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(exePath);
                var workingDir = Path.GetDirectoryName(exePath) ?? string.Empty;
                var id = GenerateId(name, exePath);

                return new GameInfo(
                    id,
                    name,
                    exePath,
                    workingDir,
                    ExecutableType.WindowsExecutable,
                    null);
            }
            catch
            {
                return null;
            }
        }

        private static void AddIfNew(List<GameInfo> games, GameInfo gameInfo)
        {
            if (!games.Any(g => string.Equals(g.ExecutablePath, gameInfo.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
            {
                games.Add(gameInfo);
            }
        }

        /// <summary>
        /// Determines whether <paramref name="path"/> resolves to a
        /// location inside <paramref name="directory"/>, using full-path
        /// normalization so that <c>..</c> segments cannot escape the
        /// directory tree. The directory itself counts as inside.
        /// </summary>
        /// <param name="path">The candidate path (relative or absolute).</param>
        /// <param name="directory">The directory that must contain it.</param>
        /// <returns><c>true</c> when the normalized path is the directory
        /// itself or lies beneath it; otherwise <c>false</c>.</returns>
        private static bool IsPathInsideDirectory(string path, string directory)
        {
            string fullPath;
            string fullDirectory;
            try
            {
                fullPath = Path.GetFullPath(path);
                fullDirectory = Path.GetFullPath(directory);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (PathTooLongException)
            {
                return false;
            }

            if (string.Equals(fullPath, fullDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var directoryPrefix = fullDirectory.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds a stable game identifier of the form
        /// <c>&lt;slug&gt;-&lt;hash&gt;</c>, where the hash is the
        /// FNV-1a 64-bit hash (offset basis 14695981039346656037, prime
        /// 1099511628211) of the UTF-8 bytes of the normalized full
        /// executable path (<see cref="Path.GetFullPath(string)"/>),
        /// rendered as 16 lowercase hexadecimal characters. Unlike
        /// <see cref="object.GetHashCode"/>, this value is identical in
        /// every process, so an identifier survives restarts.
        /// </summary>
        /// <param name="name">Display name used for the slug portion.</param>
        /// <param name="exePath">Executable path to hash.</param>
        /// <returns>The stable identifier.</returns>
        private static string GenerateId(string name, string exePath)
        {
            var normalizedPath = Path.GetFullPath(exePath);
            var bytes = Encoding.UTF8.GetBytes(normalizedPath);

            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offsetBasis;
            unchecked
            {
                foreach (var b in bytes)
                {
                    hash ^= b;
                    hash *= prime;
                }
            }

            var pathHash = hash.ToString("x16", CultureInfo.InvariantCulture);
            return $"{name.ToLowerInvariant().Replace(" ", "-")}-{pathHash}";
        }
    }
}
