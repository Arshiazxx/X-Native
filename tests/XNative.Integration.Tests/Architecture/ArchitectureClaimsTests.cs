using System;
using System.Linq;
using XNative.Core.Abstractions;
using XNative.Graphics;
using XNative.Platform;
using XNative.Plugins;
using XNative.Wine;
using Xunit;

namespace XNative.Integration.Tests.Architecture
{
    /// <summary>
    /// Tests the implementation claims made by the table in
    /// docs/architecture.md ("Interface | Implemented by"). Each fact asserts
    /// that the named module assembly actually contains a public, concrete
    /// implementation of the interface: an interface shipped without an
    /// implementation makes the documented claim false and fails the
    /// matching fact. IDeviceInfoService is excluded because its claimed
    /// implementer is the application head (XNative.XboxHost), which this
    /// project does not reference; its implementation does exist
    /// (UwpDeviceInfoService).
    /// </summary>
    public sealed class ArchitectureClaimsTests
    {
        private static bool HasImplementation<TInterface>(System.Reflection.Assembly assembly)
        {
            return assembly.GetTypes().Any(type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.IsPublic &&
                typeof(TInterface).IsAssignableFrom(type));
        }

        [Fact]
        public void Platform_ImplementsFileSystemService_AsDocumented()
        {
            Assert.True(
                HasImplementation<IFileSystemService>(typeof(PlatformModule).Assembly),
                "docs/architecture.md claims XNative.Platform implements IFileSystemService, but the assembly contains no implementation.");
        }

        [Fact]
        public void Platform_ImplementsLogService_AsDocumented()
        {
            Assert.True(
                HasImplementation<ILogService>(typeof(PlatformModule).Assembly),
                "docs/architecture.md claims XNative.Platform implements ILogService, but the assembly contains no implementation.");
        }

        [Fact]
        public void Platform_ImplementsStorageService_AsDocumented()
        {
            Assert.True(
                HasImplementation<IStorageService>(typeof(PlatformModule).Assembly),
                "docs/architecture.md claims XNative.Platform implements IStorageService, but the assembly contains no implementation.");
        }

        [Fact]
        public void Platform_ImplementsProcessService_AsDocumented()
        {
            Assert.True(
                HasImplementation<IProcessService>(typeof(PlatformModule).Assembly),
                "docs/architecture.md claims XNative.Platform implements IProcessService, but the assembly contains no implementation.");
        }

        [Fact]
        public void Graphics_ImplementsGraphicsBackend_AsDocumented()
        {
            Assert.True(
                HasImplementation<IGraphicsBackend>(typeof(GraphicsModule).Assembly),
                "docs/architecture.md claims XNative.Graphics implements IGraphicsBackend, but the assembly contains no implementation.");
        }

        [Fact]
        public void Wine_ImplementsCompatibilityLayer_AsDocumented()
        {
            Assert.True(
                HasImplementation<ICompatibilityLayer>(typeof(WineModule).Assembly),
                "docs/architecture.md claims XNative.Wine implements ICompatibilityLayer, but the assembly contains no implementation.");
        }

        [Fact]
        public void Wine_ImplementsGameProfileStore_AsItsContractStates()
        {
            Assert.True(
                HasImplementation<IGameProfileStore>(typeof(WineModule).Assembly),
                "IGameProfileStore's contract states it is implemented in XNative.Wine, but the assembly contains no implementation.");
        }

        [Fact]
        public void Plugins_ImplementsPluginHost_AsDocumented()
        {
            Assert.True(
                HasImplementation<IPluginHost>(typeof(PluginsModule).Assembly),
                "docs/architecture.md claims XNative.Plugins implements IPluginHost, but the assembly contains no implementation.");
        }

        [Fact]
        public void NoCoreAbstractionIsLeftAsAnEmptyInterface()
        {
            // Regression gate: ILogService, IStorageService,
            // IProcessService, ICompatibilityLayer, IGraphicsBackend and
            // IPluginHost must each declare the members their contracts
            // promise — none of them may regress to an empty interface.
            // INavigationService lives in XNative.UI, not the core, and is
            // deliberately not covered here.
            Type[] abstractions = typeof(IModuleDescriptor).Assembly
                .GetTypes()
                .Where(type => type.IsInterface && type.Namespace == "XNative.Core.Abstractions")
                .ToArray();

            Assert.NotEmpty(abstractions);
            Assert.All(abstractions, type =>
                Assert.True(
                    type.GetMethods().Length + type.GetProperties().Length > 0,
                    type.Name + " declares no members."));
        }
    }
}
