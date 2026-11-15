using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Games;

namespace XNative.Core.Abstractions
{
    /// <summary>
    /// Launches a game using the appropriate compatibility layer.
    /// </summary>
    public interface IGameRunner
    {
        /// <summary>
        /// Launches the specified game with default options.
        /// </summary>
        /// <param name="game">The game to launch.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The result of the launch operation.</returns>
        Task<LaunchResult> LaunchAsync(GameInfo game, CancellationToken cancellationToken = default);

        /// <summary>
        /// Launches the specified game with per-launch options.
        /// </summary>
        /// <param name="game">The game to launch.</param>
        /// <param name="options">Per-launch settings.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The result of the launch operation.</returns>
        Task<LaunchResult> LaunchAsync(GameInfo game, LaunchOptions options, CancellationToken cancellationToken = default);
    }
}
