using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;
using Xunit;

namespace XNative.Plugins.Tests
{
    public sealed class PluginsModuleTests
    {
        [Fact]
        public void AddXNativePluginsRegistersDescriptorCatalogHostAndManager()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ILogService>(new RecordingLogService());
            services.AddXNativePlugins();
            using ServiceProvider provider = services.BuildServiceProvider();

            IModuleDescriptor descriptor = provider.GetServices<IModuleDescriptor>().Single();
            Assert.Equal("xnative.plugins", descriptor.Id);

            IPluginCatalog catalog = provider.GetRequiredService<IPluginCatalog>();
            Assert.IsType<InMemoryPluginCatalog>(catalog);
            Assert.Empty(catalog.GetPlugins());

            IPluginHost host = provider.GetRequiredService<IPluginHost>();
            Assert.IsType<PluginHost>(host);
            Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, host.PluginDirectory);
            Assert.NotNull(host.Log);

            Assert.NotNull(provider.GetRequiredService<PluginManager>());
        }
    }
}
