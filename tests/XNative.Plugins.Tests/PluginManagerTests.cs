using System;
using Xunit;

namespace XNative.Plugins.Tests
{
    public sealed class PluginManagerTests
    {
        [Fact]
        public void InitializeAllInitializesEveryPluginOnce()
        {
            var first = new FakePlugin("one");
            var second = new FakePlugin("two");
            var manager = new PluginManager(new InMemoryPluginCatalog(new IPlugin[] { first, second }));
            var host = new PluginHost(new RecordingLogService(), "/plugins");

            PluginInitializationReport report = manager.InitializeAll(host);

            Assert.Equal(1, first.InitializeCount);
            Assert.Equal(1, second.InitializeCount);
            Assert.Same(host, first.LastHost);
            Assert.Equal(2, report.SucceededCount);
            Assert.Equal(0, report.FailedCount);
            Assert.True(report.AllSucceeded);
        }

        [Fact]
        public void InitializeAllIsolatesAThrowingPlugin()
        {
            var failure = new InvalidOperationException("boom");
            var before = new FakePlugin("before");
            var throwing = new FakePlugin("bad", failure);
            var after = new FakePlugin("after");
            var log = new RecordingLogService();
            var manager = new PluginManager(
                new InMemoryPluginCatalog(new IPlugin[] { before, throwing, after }));

            PluginInitializationReport report = manager.InitializeAll(new PluginHost(log, "/plugins"));

            Assert.Equal(1, before.InitializeCount);
            Assert.Equal(1, throwing.InitializeCount);
            Assert.Equal(1, after.InitializeCount);
            Assert.Equal(2, report.SucceededCount);
            Assert.Equal(1, report.FailedCount);
            Assert.False(report.AllSucceeded);
            Assert.Equal(PluginInitializationStatus.Failed, report.Results[1].Status);
            Assert.Same(failure, report.Results[1].Error);
            Assert.Single(log.Errors);
        }

        [Fact]
        public void SecondInitializeAllDoesNotCallInitializeAgain()
        {
            var plugin = new FakePlugin("one");
            var manager = new PluginManager(new InMemoryPluginCatalog(new IPlugin[] { plugin }));
            var host = new PluginHost(new RecordingLogService(), "/plugins");

            manager.InitializeAll(host);
            PluginInitializationReport second = manager.InitializeAll(host);

            Assert.Equal(1, plugin.InitializeCount);
            Assert.Equal(PluginInitializationStatus.AlreadyInitialized, second.Results[0].Status);
        }

        [Fact]
        public void FailedPluginIsNotRetriedByTheSameManager()
        {
            var plugin = new FakePlugin("bad", new InvalidOperationException("boom"));
            var manager = new PluginManager(new InMemoryPluginCatalog(new IPlugin[] { plugin }));
            var host = new PluginHost(new RecordingLogService(), "/plugins");

            manager.InitializeAll(host);
            PluginInitializationReport second = manager.InitializeAll(host);

            Assert.Equal(1, plugin.InitializeCount);
            Assert.Equal(PluginInitializationStatus.AlreadyInitialized, second.Results[0].Status);
        }

        [Fact]
        public void InitializeAllOnEmptyCatalogReturnsAnEmptyReport()
        {
            var manager = new PluginManager(new InMemoryPluginCatalog());

            PluginInitializationReport report = manager.InitializeAll(
                new PluginHost(new RecordingLogService(), "/plugins"));

            Assert.Empty(report.Results);
            Assert.True(report.AllSucceeded);
        }
    }
}
