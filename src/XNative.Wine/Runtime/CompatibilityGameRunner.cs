using System;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Abstractions;
using XNative.Core.Games;

namespace XNative.Wine.Runtime
{
    /// <summary>
    /// <see cref="IGameRunner"/> backed by the shared compatibility launch
    /// pipeline (see <see cref="CompatibilityLaunchPipeline"/>). The runner
    /// validates the executable, consults the compatibility database and
    /// logs every outcome, then reports
    /// <see cref="LaunchFailureReason.BackendNotImplemented"/>: no
    /// compatibility backend exists yet and the UWP sandbox forbids
    /// loading foreign executables, so this runner never claims a launch
    /// succeeded when no backend ran the game.
    /// </summary>
    /// <remarks>
    /// The parameterless constructor preserves the original stub
    /// behaviour exactly, including its messages: a Windows executable
    /// fails with <see cref="LaunchFailureReason.BackendNotImplemented"/>
    /// and any other executable type fails with
    /// <see cref="LaunchFailureReason.UnsupportedExecutableType"/>.
    /// </remarks>
    public sealed class CompatibilityGameRunner : IGameRunner
    {
        private readonly IFileSystemService? _fileSystem;
        private readonly IGameProfileStore? _profileStore;
        private readonly ILogService? _log;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="CompatibilityGameRunner"/> class with no
        /// dependencies, preserving the legacy stub behaviour.
        /// </summary>
        public CompatibilityGameRunner()
        {
        }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="CompatibilityGameRunner"/> class with the services
        /// the full launch pipeline needs.
        /// </summary>
        /// <param name="fileSystem">File system used to verify the executable exists.</param>
        /// <param name="profileStore">Compatibility database consulted before launch.</param>
        /// <param name="log">Log sink receiving every launch outcome.</param>
        /// <exception cref="ArgumentNullException">Thrown when any dependency is <c>null</c>.</exception>
        public CompatibilityGameRunner(IFileSystemService fileSystem, IGameProfileStore profileStore, ILogService log)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <inheritdoc />
        public Task<LaunchResult> LaunchAsync(GameInfo game, CancellationToken cancellationToken = default)
        {
            return CompatibilityLaunchPipeline.LaunchAsync(_fileSystem, _profileStore, _log, game, null, cancellationToken);
        }

        /// <inheritdoc />
        public Task<LaunchResult> LaunchAsync(GameInfo game, LaunchOptions options, CancellationToken cancellationToken = default)
        {
            return CompatibilityLaunchPipeline.LaunchAsync(_fileSystem, _profileStore, _log, game, options, cancellationToken);
        }
    }
}
