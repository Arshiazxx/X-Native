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
        public void InitializeAllFailsASecondInstanceClaimingAnAttemptedId()
        {
            var first = new FakePlugin("dup");
            var second = new FakePlugin("dup");
            var manager = new PluginManager(
                new StubPluginCatalog(new IPlugin[] { first, second }));
            var host = new PluginHost(new RecordingLogService(), "/plugins");

            PluginInitializationReport report = manager.InitializeAll(host);

            Assert.Equal(1, first.InitializeCount);
            Assert.Equal(0, second.InitializeCount);
            Assert.Equal(PluginInitializationStatus.Succeeded, report.Results[0].Status);
            Assert.Equal(PluginInitializationStatus.Failed, report.Results[1].Status);
            Assert.IsType<InvalidOperationException>(report.Results[1].Error);
            Assert.Equal(1, report.FailedCount);
            Assert.False(report.AllSucceeded);
        }

        [Fact]
        public void InitializeAllReportsTheSameInstanceTwiceAsAlreadyInitialized()
        {
            var plugin = new FakePlugin("one");
            var manager = new PluginManager(
                new StubPluginCatalog(new IPlugin[] { plugin, plugin }));
            var host = new PluginHost(new RecordingLogService(), "/plugins");

            PluginInitializationReport report = manager.InitializeAll(host);

            Assert.Equal(1, plugin.InitializeCount);
            Assert.Equal(PluginInitializationStatus.Succeeded, report.Results[0].Status);
            Assert.Equal(PluginInitializationStatus.AlreadyInitialized, report.Results[1].Status);
            Assert.True(report.AllSucceeded);
        }

        [Fact]
        public void DuplicateInstanceIsNeverReportedAsAlreadyInitialized()
        {
            var first = new FakePlugin("dup");
            var second = new FakePlugin("DUP");
            var manager = new PluginManager(
                new StubPluginCatalog(new IPlugin[] { first, second }));
            var host = new PluginHost(new RecordingLogService(), "/plugins");

            manager.InitializeAll(host);
            PluginInitializationReport secondReport = manager.InitializeAll(host);

            Assert.Equal(0, second.InitializeCount);
            Assert.Equal(PluginInitializationStatus.AlreadyInitialized, secondReport.Results[0].Status);
            Assert.Equal(PluginInitializationStatus.Failed, secondReport.Results[1].Status);
            Assert.False(secondReport.AllSucceeded);
        }

        [Fact]
        public void InitializeAllIsolatesAPluginWhoseIdGetterThrows()
        {
            var idFailure = new InvalidOperationException("id boom");
            var before = new FakePlugin("before");
            var throwingId = new ThrowingIdPlugin(idFailure);
            var after = new FakePlugin("after");
            var manager = new PluginManager(
                new StubPluginCatalog(new IPlugin[] { before, throwingId, after }));

            PluginInitializationReport report = manager.InitializeAll(
                new PluginHost(new RecordingLogService(), "/plugins"));

            Assert.Equal(1, before.InitializeCount);
            Assert.Equal(0, throwingId.InitializeCount);
            Assert.Equal(1, after.InitializeCount);
            Assert.Equal(PluginInitializationStatus.Succeeded, report.Results[0].Status);
            Assert.Equal(PluginInitializationStatus.Failed, report.Results[1].Status);
            Assert.Same(idFailure, report.Results[1].Error);
            Assert.Equal(typeof(ThrowingIdPlugin).FullName, report.Results[1].PluginId);
            Assert.Equal(PluginInitializationStatus.Succeeded, report.Results[2].Status);
            Assert.False(report.AllSucceeded);
        }

        [Fact]
        public void InitializeAllFailsAPluginWithNoUsableId()
        {
            var noId = new FakePlugin(string.Empty);
            var after = new FakePlugin("after");
            var manager = new PluginManager(
                new StubPluginCatalog(new IPlugin[] { noId, after }));

            PluginInitializationReport report = manager.InitializeAll(
                new PluginHost(new RecordingLogService(), "/plugins"));

            Assert.Equal(0, noId.InitializeCount);
            Assert.Equal(1, after.InitializeCount);
            Assert.Equal(PluginInitializationStatus.Failed, report.Results[0].Status);
            Assert.IsType<ArgumentException>(report.Results[0].Error);
            Assert.Equal(typeof(FakePlugin).FullName, report.Results[0].PluginId);
            Assert.False(report.AllSucceeded);
        }

        [Fact]
        public void InitializeAllSurvivesAThrowingLogService()
        {
            var failure = new InvalidOperationException("boom");
            var throwing = new FakePlugin("bad", failure);
            var after = new FakePlugin("after");
            var manager = new PluginManager(
                new InMemoryPluginCatalog(new IPlugin[] { throwing, after }));
            var host = new PluginHost(new ThrowingLogService(), "/plugins");

            PluginInitializationReport report = manager.InitializeAll(host);

            Assert.Equal(1, throwing.InitializeCount);
            Assert.Equal(1, after.InitializeCount);
            Assert.Equal(PluginInitializationStatus.Failed, report.Results[0].Status);
            Assert.Same(failure, report.Results[0].Error);
            Assert.Equal(PluginInitializationStatus.Succeeded, report.Results[1].Status);
            Assert.False(report.AllSucceeded);
        }

        [Fact]
        public void InitializeAllSurvivesAThrowingLogServiceForAnUnidentifiablePlugin()
        {
            var idFailure = new InvalidOperationException("id boom");
            var throwingId = new ThrowingIdPlugin(idFailure);
            var after = new FakePlugin("after");
            var manager = new PluginManager(
                new StubPluginCatalog(new IPlugin[] { throwingId, after }));
            var host = new PluginHost(new ThrowingLogService(), "/plugins");

            PluginInitializationReport report = manager.InitializeAll(host);

            Assert.Equal(1, after.InitializeCount);
            Assert.Equal(PluginInitializationStatus.Failed, report.Results[0].Status);
            Assert.Same(idFailure, report.Results[0].Error);
            Assert.Equal(PluginInitializationStatus.Succeeded, report.Results[1].Status);
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
