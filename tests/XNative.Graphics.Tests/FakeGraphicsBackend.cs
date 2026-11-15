using XNative.Core.Abstractions;
using XNative.Core.Graphics;

namespace XNative.Graphics.Tests
{
    internal sealed class FakeGraphicsBackend : IGraphicsBackend
    {
        internal FakeGraphicsBackend(string id, GraphicsApi supportedApis)
        {
            Id = id;
            SupportedApis = supportedApis;
        }

        public string Id { get; }

        public string DisplayName => Id;

        public GraphicsApi SupportedApis { get; }

        public bool IsInitialized { get; private set; }

        public bool Initialize()
        {
            IsInitialized = true;
            return true;
        }

        public void Shutdown()
        {
            IsInitialized = false;
        }
    }
}
