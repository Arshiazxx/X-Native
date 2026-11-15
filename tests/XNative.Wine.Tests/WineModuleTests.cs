using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;
using Xunit;

namespace XNative.Wine.Tests
{
    public sealed class WineModuleTests
    {
        [Fact]
        public void AddXNativeWine_RegistersRunnerProfileStoreAndLayer()
        {
            var services = new ServiceCollection();
            services.AddXNativeWine();

            using (ServiceProvider provider = services.BuildServiceProvider())
            {
                Assert.NotNull(provider.GetService<IGameRunner>());
                Assert.NotNull(provider.GetService<IGameProfileStore>());
                Assert.NotNull(provider.GetService<ICompatibilityLayer>());

                ICompatibilityLayer layer = provider.GetRequiredService<ICompatibilityLayer>();
                Assert.Equal("wine", layer.Id);

                Assert.Empty(provider.GetRequiredService<IGameProfileStore>().GetProfiles());
            }
        }
    }
}
