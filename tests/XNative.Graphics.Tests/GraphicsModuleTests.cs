using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;
using XNative.Core.Graphics;
using Xunit;

namespace XNative.Graphics.Tests
{
    public sealed class GraphicsModuleTests
    {
        [Fact]
        public void AddXNativeGraphicsRegistersDescriptorRegistryAndSelector()
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddXNativeGraphics()
                .BuildServiceProvider();

            IModuleDescriptor descriptor = provider.GetServices<IModuleDescriptor>().Single();
            Assert.Equal("xnative.graphics", descriptor.Id);

            GraphicsBackendRegistry registry = provider.GetRequiredService<GraphicsBackendRegistry>();
            Assert.Equal(1, registry.Count);
            Assert.IsType<NullGraphicsBackend>(registry.FindById("null"));

            Assert.IsType<NullGraphicsBackend>(provider.GetRequiredService<IGraphicsBackend>());
            Assert.NotNull(provider.GetRequiredService<GraphicsBackendSelector>());
            Assert.Null(provider.GetRequiredService<GraphicsBackendSelector>().Select(GraphicsApi.Vulkan));
        }
    }
}
