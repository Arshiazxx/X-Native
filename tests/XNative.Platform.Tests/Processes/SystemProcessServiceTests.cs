using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using XNative.Core.Processes;
using XNative.Platform.Processes;
using Xunit;

namespace XNative.Platform.Tests.Processes
{
    public sealed class SystemProcessServiceTests
    {
        private static bool IsWindows()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        private static ProcessStartRequest ExitImmediatelyRequest()
        {
            return IsWindows()
                ? new ProcessStartRequest("cmd", "/c exit 0")
                : new ProcessStartRequest("sh", "-c \"exit 0\"");
        }

        private static ProcessStartRequest SleeperRequest()
        {
            return IsWindows()
                ? new ProcessStartRequest("ping", "-n 31 127.0.0.1")
                : new ProcessStartRequest("sleep", "30");
        }

        [Fact]
        public async Task Start_ExitImmediately_ReportsExitedWithCodeZero()
        {
            using (var service = new SystemProcessService())
            {
                Assert.True(service.CanStartProcesses);

                RunningProcess started = await service.StartAsync(ExitImmediatelyRequest());
                Assert.True(started.Id > 0);

                RunningProcess? snapshot = null;
                for (int i = 0; i < 100; i++)
                {
                    snapshot = service.GetRunningProcess(started.Id);
                    if (snapshot != null && snapshot.HasExited)
                    {
                        break;
                    }

                    await Task.Delay(50);
                }

                Assert.NotNull(snapshot);
                Assert.True(snapshot!.HasExited);
                Assert.Equal(0, snapshot.ExitCode);
            }
        }

        [Fact]
        public async Task Stop_Sleeper_TerminatesIt()
        {
            using (var service = new SystemProcessService())
            {
                RunningProcess started = await service.StartAsync(SleeperRequest());
                RunningProcess? before = service.GetRunningProcess(started.Id);
                Assert.NotNull(before);
                Assert.False(before!.HasExited);

                bool stopped = await service.StopAsync(started.Id);

                Assert.True(stopped);
                // After StopAsync the process is no longer tracked.
                Assert.Null(service.GetRunningProcess(started.Id));
            }
        }

        [Fact]
        public async Task Stop_UnknownProcess_ReturnsTrue()
        {
            using (var service = new SystemProcessService())
            {
                Assert.True(await service.StopAsync(999999));
                Assert.Null(service.GetRunningProcess(999999));
            }
        }

        [Fact]
        public async Task Start_NullRequest_Throws()
        {
            using (var service = new SystemProcessService())
            {
                await Assert.ThrowsAsync<ArgumentNullException>(
                    () => service.StartAsync(null!));
            }
        }
    }
}
