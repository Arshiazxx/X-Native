using System;
using XNative.Core.Abstractions;
using Xunit;

namespace XNative.Plugins.Tests
{
    public sealed class PluginHostTests
    {
        [Fact]
        public void HostExposesTheLogAndDirectoryItWasBuiltWith()
        {
            var log = new RecordingLogService();
            var host = new PluginHost(log, "/plugins");

            Assert.Same(log, host.Log);
            Assert.Equal("/plugins", host.PluginDirectory);
            Assert.IsAssignableFrom<IPluginHost>(host);
        }

        [Fact]
        public void HostRejectsNullLog()
        {
            Assert.Throws<ArgumentNullException>(() => new PluginHost(null!, "/plugins"));
        }

        [Fact]
        public void HostRejectsNullOrEmptyDirectory()
        {
            var log = new RecordingLogService();

            Assert.Throws<ArgumentNullException>(() => new PluginHost(log, null!));
            Assert.Throws<ArgumentException>(() => new PluginHost(log, "  "));
        }
    }
}
