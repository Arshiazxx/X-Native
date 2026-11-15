using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Games;

namespace XNative.Core.Abstractions
{
    /// <summary>
    /// Contract for a compatibility layer able to host a foreign
    /// application. Implemented in <c>XNative.Wine</c>. A layer must be
    /// honest about its own limits: <see cref="CanRun"/> is a cheap,
    /// side-effect free prediction, and <see cref="RunAsync"/> reports
    /// failure through <see cref="LaunchResult"/> with a precise
    /// <see cref="LaunchFailureReason"/> instead of claiming success it
    /// did not achieve.
    /// </summary>
    public interface ICompatibilityLayer
    {
        /// <summary>
        /// Stable identifier of the layer, for example <c>wine</c>.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Human readable name of the layer.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Predicts whether this layer can attempt the game at all,
        /// without starting anything.
        /// </summary>
        /// <param name="game">The game to evaluate.</param>
        /// <returns>The known compatibility of the game with this layer.</returns>
        CompatibilityRating CanRun(GameInfo game);

        /// <summary>
        /// Attempts to run the game through this layer.
        /// </summary>
        /// <param name="game">The game to run.</param>
        /// <param name="options">Per-launch settings.</param>
        /// <param name="cancellationToken">Token to cancel the launch.</param>
        /// <returns>The outcome of the attempt.</returns>
        Task<LaunchResult> RunAsync(GameInfo game, LaunchOptions options, CancellationToken cancellationToken = default);
    }
}
