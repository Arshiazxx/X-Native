using System;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Abstractions;
using XNative.Core.Processes;

namespace XNative.XboxHost.Services
{
    /// <summary>
    /// Honest <see cref="IProcessService"/> for the Xbox host. The UWP
    /// sandbox forbids starting arbitrary processes, so this
    /// implementation says so instead of pretending: <see
    /// cref="CanStartProcesses"/> is <c>false</c> and <see
    /// cref="StartAsync"/> throws <see
    /// cref="PlatformNotSupportedException"/>. It exists so the shared
    /// modules can resolve <see cref="IProcessService"/> on this head
    /// without the container composing the desktop implementation,
    /// which wraps <see cref="System.Diagnostics.Process"/> and could
    /// only fail at first use inside the sandbox.
    /// </summary>
    public sealed class UnavailableProcessService : IProcessService
    {
        private const string NotSupportedMessage =
            "Starting processes is not supported in the UWP sandbox on Xbox.";

        /// <inheritdoc />
        public bool CanStartProcesses
        {
            get { return false; }
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

            throw new PlatformNotSupportedException(NotSupportedMessage);
        }

        /// <inheritdoc />
        public RunningProcess? GetRunningProcess(int processId)
        {
            // Nothing can ever have been started through this service,
            // so no identifier is tracked.
            return null;
        }

        /// <inheritdoc />
        public Task<bool> StopAsync(
            int processId,
            CancellationToken cancellationToken = default)
        {
            // Nothing can ever have been started through this service,
            // so the named process is already gone — the same answer the
            // desktop implementation gives for an untracked identifier.
            return Task.FromResult(true);
        }
    }
}
