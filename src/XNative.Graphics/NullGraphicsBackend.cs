using XNative.Core.Abstractions;
using XNative.Core.Graphics;

namespace XNative.Graphics
{
    /// <summary>
    /// The honest placeholder backend. It translates nothing, renders
    /// nothing, and says so: <see cref="SupportedApis"/> is
    /// <see cref="GraphicsApi.None"/>, so API matching never selects it
    /// for real work, and <see cref="Initialize"/> succeeding only means
    /// "the lifecycle contract is satisfied", never "pixels will appear".
    /// </summary>
    /// <remarks>
    /// Every head can register this backend today so composition and
    /// lifecycle code can be exercised end to end before a real
    /// translation backend (for example a DXVK-style layer) exists for
    /// the target device. Do not present a running application that uses
    /// only this backend as rendered output.
    /// </remarks>
    public sealed class NullGraphicsBackend : IGraphicsBackend
    {
        /// <inheritdoc />
        public string Id => "null";

        /// <inheritdoc />
        public string DisplayName => "Null (renders nothing)";

        /// <inheritdoc />
        public GraphicsApi SupportedApis => GraphicsApi.None;

        /// <inheritdoc />
        public bool IsInitialized { get; private set; }

        /// <inheritdoc />
        public bool Initialize()
        {
            // Idempotent by contract: a second call is a no-op.
            IsInitialized = true;
            return true;
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            // Idempotent: safe to call when never initialized, and safe
            // to call twice.
            IsInitialized = false;
        }
    }
}
