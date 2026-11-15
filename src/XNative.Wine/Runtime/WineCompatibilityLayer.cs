using System;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Abstractions;
using XNative.Core.Games;

namespace XNative.Wine.Runtime
{
    /// <summary>
    /// The Wine research compatibility layer. <see cref="CanRun"/> is a
    /// cheap prediction taken straight from the compatibility database —
    /// a game with no profile is honestly
    /// <see cref="CompatibilityRating.Unknown"/> — and
    /// <see cref="RunAsync"/> delegates to the same launch pipeline as
    /// <see cref="CompatibilityGameRunner"/>, so the layer and the runner
    /// can never disagree about an outcome. No backend exists yet, so
    /// <see cref="RunAsync"/> never reports success.
    /// </summary>
    public sealed class WineCompatibilityLayer : ICompatibilityLayer
    {
        private readonly IFileSystemService? _fileSystem;
        private readonly IGameProfileStore? _profileStore;
        private readonly ILogService? _log;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="WineCompatibilityLayer"/> class with no
        /// dependencies. <see cref="CanRun"/> then always returns
        /// <see cref="CompatibilityRating.Unknown"/> and
        /// <see cref="RunAsync"/> reproduces the legacy stub results.
        /// </summary>
        public WineCompatibilityLayer()
        {
        }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="WineCompatibilityLayer"/> class with only a profile
        /// store, for prediction and profile-aware launch attempts without
        /// file-system or logging services.
        /// </summary>
        /// <param name="profileStore">Compatibility database consulted by this layer.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="profileStore"/> is <c>null</c>.</exception>
        public WineCompatibilityLayer(IGameProfileStore profileStore)
        {
            _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="WineCompatibilityLayer"/> class with the services
        /// the full launch pipeline needs.
        /// </summary>
        /// <param name="fileSystem">File system used to verify the executable exists.</param>
        /// <param name="profileStore">Compatibility database consulted by this layer.</param>
        /// <param name="log">Log sink receiving every launch outcome.</param>
        /// <exception cref="ArgumentNullException">Thrown when any dependency is <c>null</c>.</exception>
        public WineCompatibilityLayer(IFileSystemService fileSystem, IGameProfileStore profileStore, ILogService log)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <inheritdoc />
        public string Id => "wine";

        /// <inheritdoc />
        public string DisplayName => "Wine (research)";

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="game"/> is <c>null</c>.</exception>
        public CompatibilityRating CanRun(GameInfo game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            if (_profileStore == null)
            {
                return CompatibilityRating.Unknown;
            }

            GameProfile? profile = _profileStore.FindForGame(game);
            return profile != null ? profile.Rating : CompatibilityRating.Unknown;
        }

        /// <inheritdoc />
        public Task<LaunchResult> RunAsync(GameInfo game, LaunchOptions options, CancellationToken cancellationToken = default)
        {
            return CompatibilityLaunchPipeline.LaunchAsync(_fileSystem, _profileStore, _log, game, options, cancellationToken);
        }
    }
}
