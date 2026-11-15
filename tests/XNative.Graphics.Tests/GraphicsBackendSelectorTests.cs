using XNative.Core.Graphics;
using Xunit;

namespace XNative.Graphics.Tests
{
    public sealed class GraphicsBackendSelectorTests
    {
        [Fact]
        public void SelectPrefersExactMatchOverBroaderSuperset()
        {
            var registry = new GraphicsBackendRegistry();
            var broad = new FakeGraphicsBackend("broad", GraphicsApi.Direct3D11 | GraphicsApi.Direct3D9 | GraphicsApi.Vulkan);
            var exact = new FakeGraphicsBackend("exact", GraphicsApi.Direct3D11);
            registry.Register(broad);
            registry.Register(exact);

            var selector = new GraphicsBackendSelector(registry);

            Assert.Same(exact, selector.Select(GraphicsApi.Direct3D11));
        }

        [Fact]
        public void SelectReturnsSupersetWhenNoExactMatchExists()
        {
            var registry = new GraphicsBackendRegistry();
            var combo = new FakeGraphicsBackend("combo", GraphicsApi.Direct3D11 | GraphicsApi.Direct3D9);
            registry.Register(combo);

            Assert.Same(combo, GraphicsBackendSelector.Select(registry, GraphicsApi.Direct3D9));
        }

        [Fact]
        public void SelectBreaksTiesByRegistrationOrder()
        {
            var registry = new GraphicsBackendRegistry();
            var first = new FakeGraphicsBackend("first", GraphicsApi.Direct3D11 | GraphicsApi.Vulkan);
            var second = new FakeGraphicsBackend("second", GraphicsApi.Direct3D11 | GraphicsApi.OpenGL);
            registry.Register(first);
            registry.Register(second);

            var selector = new GraphicsBackendSelector(registry);

            Assert.Same(first, selector.Select(GraphicsApi.Direct3D11));
        }

        [Fact]
        public void SelectReturnsNullWhenNothingCoversTheRequirement()
        {
            var registry = new GraphicsBackendRegistry();
            registry.Register(new FakeGraphicsBackend("vk", GraphicsApi.Vulkan));
            registry.Register(new NullGraphicsBackend());

            var selector = new GraphicsBackendSelector(registry);

            Assert.Null(selector.Select(GraphicsApi.Direct3D12));
        }

        [Fact]
        public void SelectNoneCanReturnTheNullBackend()
        {
            var registry = new GraphicsBackendRegistry();
            var nullBackend = new NullGraphicsBackend();
            registry.Register(nullBackend);

            var selector = new GraphicsBackendSelector(registry);

            Assert.Same(nullBackend, selector.Select(GraphicsApi.None));
        }

        [Fact]
        public void SelectNeverReturnsNullBackendForARealApi()
        {
            var registry = new GraphicsBackendRegistry();
            registry.Register(new NullGraphicsBackend());

            var selector = new GraphicsBackendSelector(registry);

            Assert.Null(selector.Select(GraphicsApi.OpenGL));
        }

        [Fact]
        public void SelectTerminatesForBackendWithSignBitApiValue()
        {
            var registry = new GraphicsBackendRegistry();
            // An undefined cast in a custom backend can set the sign
            // bit of SupportedApis. Counting that backend's extra APIs
            // must terminate (a signed-shift bit count would spin
            // forever on the negative value) and count it as one extra.
            var weird = new FakeGraphicsBackend("weird", GraphicsApi.Direct3D11 | (GraphicsApi)int.MinValue);
            registry.Register(weird);

            var selector = new GraphicsBackendSelector(registry);

            Assert.Same(weird, selector.Select(GraphicsApi.Direct3D11));
        }

        [Fact]
        public void SelectCountsEveryBitOfAnAllBitsBackend()
        {
            var registry = new GraphicsBackendRegistry();
            var everything = new FakeGraphicsBackend("everything", (GraphicsApi)(-1));
            var nullBackend = new NullGraphicsBackend();
            registry.Register(everything);
            registry.Register(nullBackend);

            var selector = new GraphicsBackendSelector(registry);

            // The all-bits backend has 32 extra APIs over None; the
            // null backend has none, so it wins despite registering
            // second — the full popcount, not a prefix of it, decides.
            Assert.Same(nullBackend, selector.Select(GraphicsApi.None));
        }
    }
}
