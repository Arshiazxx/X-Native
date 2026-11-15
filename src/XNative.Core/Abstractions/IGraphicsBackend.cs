using XNative.Core.Graphics;

namespace XNative.Core.Abstractions
{
    /// <summary>
    /// Contract for a rendering backend. Implemented in
    /// <c>XNative.Graphics</c>. The backend owns translation of a source
    /// graphics API to whatever the host device renders natively; this
    /// contract covers only its identity and lifecycle, the rendering
    /// surface itself is backend-specific and intentionally not part of
    /// the platform independent core.
    /// </summary>
    public interface IGraphicsBackend
    {
        /// <summary>
        /// Stable identifier of the backend, for example <c>dxvk</c>.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Human readable name of the backend.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// The source APIs this backend can translate.
        /// </summary>
        GraphicsApi SupportedApis { get; }

        /// <summary>
        /// Whether <see cref="Initialize"/> has completed successfully.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Prepares the backend for rendering. Calling it twice is not
        /// an error; the second call is a no-op.
        /// </summary>
        /// <returns><c>true</c> when the backend is ready to render.</returns>
        bool Initialize();

        /// <summary>
        /// Releases everything <see cref="Initialize"/> acquired.
        /// </summary>
        void Shutdown();
    }
}
