using System;
using System.Collections.Generic;
using XNative.Core.Abstractions;
using XNative.Core.Graphics;

namespace XNative.Graphics
{
    /// <summary>
    /// In-memory registry of the <see cref="IGraphicsBackend"/> instances
    /// available to the application. Backends are registered in code at
    /// composition time; there is deliberately no dynamic discovery here.
    /// </summary>
    /// <remarks>
    /// On Xbox / UWP the app runs in a sandbox that cannot load arbitrary
    /// assemblies at runtime, so a real backend can only ever be one that
    /// shipped inside the app package and was registered explicitly by a
    /// module. This registry is that explicit registration point. It does
    /// not render anything itself; it only holds and resolves backends.
    /// Registration order is preserved and is the deterministic
    /// tie-break used by <see cref="GraphicsBackendSelector"/>.
    /// This type is not thread-safe; register everything at startup
    /// before resolving backends.
    /// </remarks>
    public sealed class GraphicsBackendRegistry
    {
        private readonly List<IGraphicsBackend> _backends = new List<IGraphicsBackend>();
        private readonly Dictionary<string, IGraphicsBackend> _byId =
            new Dictionary<string, IGraphicsBackend>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The number of registered backends.
        /// </summary>
        public int Count => _backends.Count;

        /// <summary>
        /// Registers a backend.
        /// </summary>
        /// <param name="backend">The backend to register.</param>
        /// <exception cref="ArgumentNullException"><paramref name="backend"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// The backend has no usable <see cref="IGraphicsBackend.Id"/>, or a
        /// backend with the same id (compared case-insensitively) is
        /// already registered.
        /// </exception>
        public void Register(IGraphicsBackend backend)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            if (string.IsNullOrWhiteSpace(backend.Id))
            {
                throw new ArgumentException("A graphics backend must have a non-empty Id.", nameof(backend));
            }

            if (_byId.ContainsKey(backend.Id))
            {
                throw new ArgumentException(
                    "A graphics backend with id '" + backend.Id + "' is already registered.",
                    nameof(backend));
            }

            _byId.Add(backend.Id, backend);
            _backends.Add(backend);
        }

        /// <summary>
        /// Looks a backend up by its <see cref="IGraphicsBackend.Id"/>,
        /// compared case-insensitively.
        /// </summary>
        /// <param name="id">The backend id to find.</param>
        /// <param name="backend">The backend when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> when a backend with that id is registered.</returns>
        public bool TryGetById(string id, out IGraphicsBackend? backend)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                backend = null;
                return false;
            }

            return _byId.TryGetValue(id, out backend);
        }

        /// <summary>
        /// Looks a backend up by id, returning <c>null</c> instead of
        /// throwing when it is not registered.
        /// </summary>
        /// <param name="id">The backend id to find.</param>
        /// <returns>The backend, or <c>null</c> when no backend has that id.</returns>
        public IGraphicsBackend? FindById(string id)
        {
            return TryGetById(id, out IGraphicsBackend? backend) ? backend : null;
        }

        /// <summary>
        /// Returns every registered backend, in registration order.
        /// </summary>
        /// <returns>A snapshot copy; mutating it does not affect the registry.</returns>
        public IReadOnlyList<IGraphicsBackend> GetAll()
        {
            return new List<IGraphicsBackend>(_backends);
        }

        /// <summary>
        /// Returns every backend that can translate all of the APIs in
        /// <paramref name="api"/>: a backend matches when
        /// <c>(SupportedApis &amp; api) == api</c>. Results are in
        /// registration order. Asking for <see cref="GraphicsApi.None"/>
        /// matches every backend, because every backend trivially
        /// supports "no API".
        /// </summary>
        /// <param name="api">The API flags a backend must fully cover.</param>
        /// <returns>The matching backends, possibly empty.</returns>
        public IReadOnlyList<IGraphicsBackend> FindSupporting(GraphicsApi api)
        {
            var matches = new List<IGraphicsBackend>();
            foreach (IGraphicsBackend backend in _backends)
            {
                if ((backend.SupportedApis & api) == api)
                {
                    matches.Add(backend);
                }
            }

            return matches;
        }
    }
}
