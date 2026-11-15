using System;
using XNative.Core.Abstractions;
using XNative.Core.Graphics;
using Xunit;

namespace XNative.Graphics.Tests
{
    public sealed class GraphicsBackendRegistryTests
    {
        [Fact]
        public void RegisterThenFindByIdReturnsTheBackend()
        {
            var registry = new GraphicsBackendRegistry();
            var backend = new FakeGraphicsBackend("dxvk", GraphicsApi.Direct3D11);
            registry.Register(backend);

            Assert.True(registry.TryGetById("dxvk", out IGraphicsBackend? found));
            Assert.Same(backend, found);
            Assert.Same(backend, registry.FindById("dxvk"));
            Assert.Equal(1, registry.Count);
        }

        [Fact]
        public void FindByIdIsCaseInsensitive()
        {
            var registry = new GraphicsBackendRegistry();
            var backend = new FakeGraphicsBackend("DxVk", GraphicsApi.Vulkan);
            registry.Register(backend);

            Assert.Same(backend, registry.FindById("DXVK"));
            Assert.Same(backend, registry.FindById("dxvk"));
        }

        [Fact]
        public void FindByIdReturnsNullForUnknownId()
        {
            var registry = new GraphicsBackendRegistry();

            Assert.Null(registry.FindById("missing"));
            Assert.False(registry.TryGetById("missing", out IGraphicsBackend? found));
            Assert.Null(found);
        }

        [Fact]
        public void RegisterDuplicateIdThrows()
        {
            var registry = new GraphicsBackendRegistry();
            registry.Register(new FakeGraphicsBackend("dxvk", GraphicsApi.Direct3D11));

            Assert.Throws<ArgumentException>(() =>
                registry.Register(new FakeGraphicsBackend("DXVK", GraphicsApi.Vulkan)));
        }

        [Fact]
        public void RegisterNullThrows()
        {
            var registry = new GraphicsBackendRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
        }

        [Fact]
        public void GetAllReturnsSnapshotInRegistrationOrder()
        {
            var registry = new GraphicsBackendRegistry();
            var first = new FakeGraphicsBackend("a", GraphicsApi.OpenGL);
            var second = new FakeGraphicsBackend("b", GraphicsApi.Vulkan);
            registry.Register(first);
            registry.Register(second);

            System.Collections.Generic.IReadOnlyList<IGraphicsBackend> all = registry.GetAll();

            Assert.Equal(new IGraphicsBackend[] { first, second }, all);
            ((System.Collections.Generic.List<IGraphicsBackend>)all).Clear();
            Assert.Equal(2, registry.Count);
        }

        [Fact]
        public void FindSupportingMatchesOnlyFullSupersets()
        {
            var registry = new GraphicsBackendRegistry();
            var d3d11Only = new FakeGraphicsBackend("d3d11", GraphicsApi.Direct3D11);
            var combo = new FakeGraphicsBackend("combo", GraphicsApi.Direct3D11 | GraphicsApi.Direct3D9);
            var vulkan = new FakeGraphicsBackend("vk", GraphicsApi.Vulkan);
            registry.Register(d3d11Only);
            registry.Register(combo);
            registry.Register(vulkan);

            Assert.Equal(
                new IGraphicsBackend[] { d3d11Only, combo },
                registry.FindSupporting(GraphicsApi.Direct3D11));
            Assert.Equal(
                new IGraphicsBackend[] { combo },
                registry.FindSupporting(GraphicsApi.Direct3D11 | GraphicsApi.Direct3D9));
            Assert.Empty(registry.FindSupporting(GraphicsApi.Direct3D12));
        }

        [Fact]
        public void FindSupportingNoneMatchesEveryBackend()
        {
            var registry = new GraphicsBackendRegistry();
            registry.Register(new FakeGraphicsBackend("a", GraphicsApi.OpenGL));
            registry.Register(new NullGraphicsBackend());

            Assert.Equal(2, registry.FindSupporting(GraphicsApi.None).Count);
        }
    }
}
