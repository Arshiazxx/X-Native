using System;
using System.Collections.Generic;
using Xunit;

namespace XNative.Plugins.Tests
{
    public sealed class InMemoryPluginCatalogTests
    {
        [Fact]
        public void EmptyCatalogReturnsNoPlugins()
        {
            Assert.Empty(new InMemoryPluginCatalog().GetPlugins());
            Assert.Empty(new InMemoryPluginCatalog(null).GetPlugins());
        }

        [Fact]
        public void GetPluginsReturnsTheRegisteredPlugins()
        {
            var first = new FakePlugin("one");
            var second = new FakePlugin("two");
            var catalog = new InMemoryPluginCatalog(new IPlugin[] { first, second });

            Assert.Equal(new IPlugin[] { first, second }, catalog.GetPlugins());
        }

        [Fact]
        public void GetPluginsReturnsASnapshotCopy()
        {
            var catalog = new InMemoryPluginCatalog(new IPlugin[] { new FakePlugin("one") });

            IReadOnlyCollection<IPlugin> snapshot = catalog.GetPlugins();
            ((List<IPlugin>)snapshot).Clear();

            Assert.Single(catalog.GetPlugins());
            Assert.NotSame(snapshot, catalog.GetPlugins());
        }

        [Fact]
        public void ConstructorCopiesTheSourceSequence()
        {
            var source = new List<IPlugin> { new FakePlugin("one") };
            var catalog = new InMemoryPluginCatalog(source);
            source.Add(new FakePlugin("two"));

            Assert.Single(catalog.GetPlugins());
        }

        [Fact]
        public void ConstructorRejectsNullPluginEntries()
        {
            var plugins = new IPlugin[] { new FakePlugin("one"), null! };

            Assert.Throws<ArgumentException>(() => new InMemoryPluginCatalog(plugins));
        }

        [Fact]
        public void ConstructorRejectsDuplicatePluginIds()
        {
            var plugins = new IPlugin[] { new FakePlugin("telemetry"), new FakePlugin("telemetry") };

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new InMemoryPluginCatalog(plugins));

            Assert.Contains("telemetry", exception.Message);
        }

        [Fact]
        public void ConstructorRejectsPluginIdsThatDifferOnlyByCase()
        {
            var plugins = new IPlugin[] { new FakePlugin("Telemetry"), new FakePlugin("telemetry") };

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new InMemoryPluginCatalog(plugins));

            Assert.Contains("telemetry", exception.Message);
        }

        [Fact]
        public void ConstructorRejectsAPluginWithANullId()
        {
            var plugins = new IPlugin[] { new FakePlugin(null!) };

            Assert.Throws<ArgumentException>(() => new InMemoryPluginCatalog(plugins));
        }

        [Fact]
        public void ConstructorRejectsAPluginWithAnEmptyId()
        {
            var plugins = new IPlugin[] { new FakePlugin(string.Empty) };

            Assert.Throws<ArgumentException>(() => new InMemoryPluginCatalog(plugins));
        }

        [Fact]
        public void ConstructorRejectsAPluginWithAWhitespaceId()
        {
            var plugins = new IPlugin[] { new FakePlugin("   ") };

            Assert.Throws<ArgumentException>(() => new InMemoryPluginCatalog(plugins));
        }
    }
}
