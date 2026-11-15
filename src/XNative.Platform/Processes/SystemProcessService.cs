using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Abstractions;
using XNative.Core.Processes;

namespace XNative.Platform.Processes
{
    /// <summary>
    /// <see cref="IProcessService"/> backed by
    /// <see cref="System.Diagnostics.Process"/>. Started processes are
    /// tracked in a concurrent dictionary; snapshots are rebuilt on every
    /// <see cref="GetRunningProcess"/> call so exit state is fresh.
    /// Exited processes are pruned so their handles do not accumulate
    /// for the life of the service: <see cref="StartAsync"/> sweeps out
    /// every exited entry before starting a new process, and
    /// <see cref="GetRunningProcess"/> reports an exited process once
    /// and then prunes it (a later call for the same identifier returns
    /// <c>null</c>).
    /// The service owns the tracked <see cref="Process"/> objects and is
    /// <see cref="IDisposable"/>: register it as a singleton so the DI
    /// container disposes it (and every still-tracked process handle)
    /// at shutdown. Disposing the service does not kill running
    /// processes; call <see cref="StopAsync"/> for that.
    /// </summary>
    public sealed class SystemProcessService : IProcessService, IDisposable
    {
        private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan KillWaitPeriod = TimeSpan.FromSeconds(2);
        private const int PollMilliseconds = 50;

        private readonly ConcurrentDictionary<int, Process> _processes =
            new ConcurrentDictionary<int, Process>();

        private bool _disposed;

        /// <inheritdoc />
        public bool CanStartProcesses
        {
            get { return true; }
        }

        /// <inheritdoc />
        public Task<RunningProcess> StartAsync(
            ProcessStartRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            PruneExitedProcesses();

            var startInfo = new ProcessStartInfo
            {
                FileName = request.FileName,
                Arguments = request.Arguments ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
            {
                startInfo.WorkingDirectory = request.WorkingDirectory;
            }

            if (request.EnvironmentVariables != null)
            {
                foreach (var pair in request.EnvironmentVariables)
                {
                    startInfo.EnvironmentVariables[pair.Key] = pair.Value;
                }
            }

            var process = new Process { StartInfo = startInfo };
            try
            {
                if (!process.Start())
                {
                    process.Dispose();
                    throw new InvalidOperationException(
                        "The process could not be started: " + request.FileName);
                }
            }
            catch
            {
                process.Dispose();
                throw;
            }

            _processes[process.Id] = process;
            return Task.FromResult(Snapshot(process));
        }

        /// <inheritdoc />
        public RunningProcess? GetRunningProcess(int processId)
        {
            ThrowIfDisposed();

            Process process;
            if (!_processes.TryGetValue(processId, out process))
            {
                return null;
            }

            RunningProcess snapshot = Snapshot(process);
            if (snapshot.HasExited)
            {
                // The exit has now been observed, so prune the entry:
                // an exited process nobody looks at again must not keep
                // its handle for the life of the service.
                RemoveAndDispose(processId, process);
            }

            return snapshot;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Cancelling <paramref name="cancellationToken"/> during the
        /// grace period leaves the process running and tracked; the
        /// entry is removed only once the process has exited or
        /// <see cref="Process.Kill"/> has been issued.
        /// </remarks>
        public async Task<bool> StopAsync(
            int processId,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            Process process;
            if (!_processes.TryGetValue(processId, out process))
            {
                // Unknown or already removed: nothing is running.
                return true;
            }

            if (SafeHasExited(process))
            {
                RemoveAndDispose(processId, process);
                return true;
            }

            // There is no cooperative close for a headless child
            // started with UseShellExecute=false, so the grace
            // period is a wait for voluntary exit before Kill().
            // If this wait is cancelled, the process is still running
            // and MUST stay tracked: cancelling a stop must never make
            // a process less stoppable, so the
            // OperationCanceledException propagates with the entry
            // untouched and a later StopAsync (or Dispose) can still
            // kill the process.
            if (await WaitForExitAsync(process, GracePeriod, cancellationToken).ConfigureAwait(false))
            {
                RemoveAndDispose(processId, process);
                return true;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
                // Raced with a natural exit.
                bool exited = SafeHasExited(process);
                if (exited)
                {
                    RemoveAndDispose(processId, process);
                }

                return exited;
            }

            // Kill() has been issued, so the process is terminating
            // whether or not we stay to watch it: the entry is always
            // removed from here on, even when the post-kill wait is
            // cancelled — keeping the handle would only leak it.
            try
            {
                bool exited = await WaitForExitAsync(process, KillWaitPeriod, cancellationToken).ConfigureAwait(false);
                RemoveAndDispose(processId, process);
                return exited;
            }
            catch (OperationCanceledException)
            {
                RemoveAndDispose(processId, process);
                throw;
            }
        }

        /// <summary>
        /// Releases every tracked process handle. Processes themselves
        /// are not killed; use <see cref="StopAsync"/> first when they
        /// must not outlive the service.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var pair in _processes)
            {
                pair.Value.Dispose();
            }

            _processes.Clear();
        }

        private void PruneExitedProcesses()
        {
            foreach (var pair in _processes)
            {
                if (SafeHasExited(pair.Value))
                {
                    RemoveAndDispose(pair.Key, pair.Value);
                }
            }
        }

        private void RemoveAndDispose(int processId, Process process)
        {
            Process removed;
            if (!_processes.TryRemove(processId, out removed))
            {
                return;
            }

            if (ReferenceEquals(removed, process))
            {
                removed.Dispose();
                return;
            }

            // A different process was registered under the same
            // identifier in the meantime (the OS reused the PID): put
            // it back rather than orphaning a live process.
            _processes[processId] = removed;
        }

        private static RunningProcess Snapshot(Process process)
        {
            bool hasExited = SafeHasExited(process);
            int? exitCode = null;
            if (hasExited)
            {
                try
                {
                    exitCode = process.ExitCode;
                }
                catch (InvalidOperationException)
                {
                    exitCode = null;
                }
            }

            DateTime startTimeUtc;
            try
            {
                startTimeUtc = process.StartTime.ToUniversalTime();
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // StartTime is unavailable for some already-exited
                // processes on some platforms; fall back to now rather
                // than failing the snapshot.
                startTimeUtc = DateTime.UtcNow;
            }

            string fileName;
            try
            {
                fileName = process.StartInfo != null && !string.IsNullOrEmpty(process.StartInfo.FileName)
                    ? process.StartInfo.FileName
                    : process.ProcessName;
            }
            catch (InvalidOperationException)
            {
                fileName = string.Empty;
            }

            return new RunningProcess(process.Id, fileName, startTimeUtc, hasExited, exitCode);
        }

        private static bool SafeHasExited(Process process)
        {
            try
            {
                return process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        private static async Task<bool> WaitForExitAsync(
            Process process,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            // netstandard2.0 has no Task.WaitAsync / WaitForExitAsync;
            // poll HasExited instead.
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (SafeHasExited(process))
                {
                    return true;
                }

                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(PollMilliseconds).ConfigureAwait(false);
            }

            return SafeHasExited(process);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SystemProcessService));
            }
        }
    }
}
