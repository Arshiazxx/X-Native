using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Processes;

namespace XNative.Core.Abstractions
{
    /// <summary>
    /// Abstraction over process creation and lifetime control.
    /// Implemented in <c>XNative.Platform</c> on platforms whose sandbox
    /// allows starting processes at all. On Xbox / UWP the sandbox does
    /// not, and the implementation must say so honestly by throwing
    /// <see cref="System.PlatformNotSupportedException"/> from
    /// <see cref="StartAsync"/> rather than pretending to succeed.
    /// </summary>
    public interface IProcessService
    {
        /// <summary>
        /// Whether this platform can start processes at all.
        /// </summary>
        bool CanStartProcesses { get; }

        /// <summary>
        /// Starts a process described by <paramref name="request"/>.
        /// </summary>
        /// <param name="request">What to start.</param>
        /// <param name="cancellationToken">Token to cancel the start.</param>
        /// <returns>A snapshot of the started process.</returns>
        Task<RunningProcess> StartAsync(ProcessStartRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Takes a fresh snapshot of a process previously started
        /// through this service.
        /// </summary>
        /// <param name="processId">The process identifier returned by <see cref="StartAsync"/>.</param>
        /// <returns>The current snapshot, or <c>null</c> when no such process is tracked.</returns>
        RunningProcess? GetRunningProcess(int processId);

        /// <summary>
        /// Requests that a tracked process exits, waiting up to the
        /// implementation's grace period before forcing it.
        /// </summary>
        /// <param name="processId">The process identifier returned by <see cref="StartAsync"/>.</param>
        /// <param name="cancellationToken">Token to cancel the wait.</param>
        /// <returns><c>true</c> when the process exited (or was already gone).</returns>
        Task<bool> StopAsync(int processId, CancellationToken cancellationToken = default);
    }
}
