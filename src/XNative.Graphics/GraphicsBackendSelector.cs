using System;
using XNative.Core.Abstractions;
using XNative.Core.Graphics;

namespace XNative.Graphics
{
    /// <summary>
    /// Picks the best registered backend for a required set of graphics
    /// APIs. Selection is deterministic: among the backends that cover
    /// every required API, the one whose
    /// <see cref="IGraphicsBackend.SupportedApis"/> adds the fewest extra
    /// APIs wins (so an exact match always beats a broader superset),
    /// and ties are broken by registration order.
    /// </summary>
    public sealed class GraphicsBackendSelector
    {
        private readonly GraphicsBackendRegistry _registry;

        /// <summary>
        /// Creates a selector over <paramref name="registry"/>.
        /// </summary>
        /// <param name="registry">The registry to select from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <c>null</c>.</exception>
        public GraphicsBackendSelector(GraphicsBackendRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Returns the best backend covering <paramref name="required"/>,
        /// or <c>null</c> when no registered backend covers it. Note that
        /// <see cref="NullGraphicsBackend"/> supports
        /// <see cref="GraphicsApi.None"/> only, so it is selected only
        /// when <paramref name="required"/> is itself
        /// <see cref="GraphicsApi.None"/>.
        /// </summary>
        /// <param name="required">The APIs the caller needs translated.</param>
        /// <returns>The selected backend, or <c>null</c> when none qualifies.</returns>
        public IGraphicsBackend? Select(GraphicsApi required)
        {
            return Select(_registry, required);
        }

        /// <summary>
        /// Stateless form of <see cref="Select(GraphicsApi)"/>.
        /// </summary>
        /// <param name="registry">The registry to select from.</param>
        /// <param name="required">The APIs the caller needs translated.</param>
        /// <returns>The selected backend, or <c>null</c> when none qualifies.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <c>null</c>.</exception>
        public static IGraphicsBackend? Select(GraphicsBackendRegistry registry, GraphicsApi required)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            IGraphicsBackend? best = null;
            int bestExtraCount = int.MaxValue;

            // FindSupporting preserves registration order, so keeping the
            // first backend seen at the lowest extra count implements
            // the registration-order tie-break.
            foreach (IGraphicsBackend candidate in registry.FindSupporting(required))
            {
                int extraCount = CountBits(candidate.SupportedApis & ~required);
                if (extraCount < bestExtraCount)
                {
                    best = candidate;
                    bestExtraCount = extraCount;
                }
            }

            return best;
        }

        private static int CountBits(GraphicsApi value)
        {
            int bits = (int)value;
            int count = 0;
            while (bits != 0)
            {
                count += bits & 1;
                bits >>= 1;
            }

            return count;
        }
    }
}
