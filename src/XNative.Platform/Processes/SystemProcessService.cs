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

            return Snapshot(process);
        }

        /// <inheritdoc />
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

            try
            {
                if (SafeHasExited(process))
                {
                    return true;
                }

                // There is no cooperative close for a headless child
                // started with UseShellExecute=false, so the grace
                // period is a wait for voluntary exit before Kill().
                if (await WaitForExitAsync(process, GracePeriod, cancellationToken).ConfigureAwait(false))
                {
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
                    return SafeHasExited(process);
                }

                return await WaitForExitAsync(process, KillWaitPeriod, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Process removed;
                if (_processes.TryRemove(processId, out removed))
                {
                    removed.Dispose();
                }
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
