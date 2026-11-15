using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
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

        [Fact]
        public async Task Stop_CancelledDuringGracePeriod_KeepsProcessTrackedAndStoppable()
        {
            using (var service = new SystemProcessService())
            {
                RunningProcess started = await service.StartAsync(SleeperRequest());

                // The grace period is 2s; cancelling well inside it
                // must not orphan the still-running process.
                using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300)))
                {
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(
                        () => service.StopAsync(started.Id, cts.Token));
                }

                RunningProcess? stillTracked = service.GetRunningProcess(started.Id);
                Assert.NotNull(stillTracked);
                Assert.False(stillTracked!.HasExited);

                // And a later stop can still kill it.
                Assert.True(await service.StopAsync(started.Id));
                Assert.Null(service.GetRunningProcess(started.Id));
            }
        }

        [Fact]
        public async Task GetRunningProcess_ExitedProcess_IsReportedOnceThenPruned()
        {
            using (var service = new SystemProcessService())
            {
                RunningProcess started = await service.StartAsync(ExitImmediatelyRequest());

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

                // The exit was reported; the entry is now pruned.
                Assert.Null(service.GetRunningProcess(started.Id));
            }
        }

        [Fact]
        public async Task Start_PrunesPreviouslyExitedProcesses()
        {
            using (var service = new SystemProcessService())
            {
                RunningProcess first = await service.StartAsync(ExitImmediatelyRequest());

                // Wait for the first process to exit WITHOUT observing
                // it through the service (observing an exit through
                // GetRunningProcess prunes it on its own).
                await WaitForRawProcessExit(first.Id);

                RunningProcess second = await service.StartAsync(SleeperRequest());
                try
                {
                    // Starting the second process swept the exited
                    // first one out of the tracking table.
                    Assert.Null(service.GetRunningProcess(first.Id));
                }
                finally
                {
                    await service.StopAsync(second.Id);
                }
            }
        }

        private static async Task WaitForRawProcessExit(int processId)
        {
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    using (Process process = Process.GetProcessById(processId))
                    {
                        if (process.HasExited)
                        {
                            return;
                        }
                    }
                }
                catch (ArgumentException)
                {
                    // No such process anymore: it has exited.
                    return;
                }

                await Task.Delay(50);
            }

            throw new TimeoutException("Process " + processId + " did not exit in time.");
        }
    }
}
