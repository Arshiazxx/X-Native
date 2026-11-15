using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Abstractions;
using XNative.Core.Games;

namespace XNative.Integration.Tests.Doubles
{
    /// <summary>
    /// Hand-rolled <see cref="IGameCatalog"/> double whose
    /// <see cref="GetGamesAsync"/> behaviour is supplied by the test, so one
    /// double covers the success, throwing and cancelling cases.
    /// </summary>
    public sealed class StubGameCatalog : IGameCatalog
    {
        private readonly Func<CancellationToken, Task<IReadOnlyCollection<GameInfo>>> _getGames;

        public StubGameCatalog(Func<CancellationToken, Task<IReadOnlyCollection<GameInfo>>> getGames)
        {
            _getGames = getGames ?? throw new ArgumentNullException(nameof(getGames));
        }

        public static StubGameCatalog Returning(params GameInfo[] games)
        {
            return new StubGameCatalog(_ => Task.FromResult<IReadOnlyCollection<GameInfo>>(games));
        }

        public static StubGameCatalog Throwing(Exception exception)
        {
            return new StubGameCatalog(_ => Task.FromException<IReadOnlyCollection<GameInfo>>(exception));
        }

        public Task<IReadOnlyCollection<GameInfo>> GetGamesAsync(CancellationToken cancellationToken = default)
        {
            return _getGames(cancellationToken);
        }

        public Task<GameInfo?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<GameInfo?>(null);
        }

        public Task<GameInfo?> FindByExecutablePathAsync(string executablePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<GameInfo?>(null);
        }

        public Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
