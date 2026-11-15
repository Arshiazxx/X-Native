using XNative.Core.Graphics;
using Xunit;

namespace XNative.Graphics.Tests
{
    public sealed class NullGraphicsBackendTests
    {
        [Fact]
        public void IdentityIsTheHonestPlaceholder()
        {
            var backend = new NullGraphicsBackend();

            Assert.Equal("null", backend.Id);
            Assert.Equal(GraphicsApi.None, backend.SupportedApis);
            Assert.False(backend.IsInitialized);
        }

        [Fact]
        public void InitializeReturnsTrueAndIsIdempotent()
        {
            var backend = new NullGraphicsBackend();

            Assert.True(backend.Initialize());
            Assert.True(backend.IsInitialized);
            Assert.True(backend.Initialize());
            Assert.True(backend.IsInitialized);
        }

        [Fact]
        public void ShutdownIsIdempotentAndResetsLifecycle()
        {
            var backend = new NullGraphicsBackend();
            backend.Shutdown();
            Assert.False(backend.IsInitialized);

            backend.Initialize();
            backend.Shutdown();
            Assert.False(backend.IsInitialized);
            backend.Shutdown();
            Assert.False(backend.IsInitialized);

            Assert.True(backend.Initialize());
            Assert.True(backend.IsInitialized);
        }
    }
}
