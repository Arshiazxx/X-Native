using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace XNative.UI.Mvvm
{
    /// <summary>
    /// <see cref="ICommand"/> backed by an asynchronous delegate.
    /// While the delegate runs, <see cref="CanExecute"/> returns
    /// <c>false</c> and <see cref="CanExecuteChanged"/> is raised when the
    /// execution starts and when it finishes. An exception thrown by the
    /// delegate never escapes onto the UI thread: it is captured in
    /// <see cref="LastError"/> and surfaced through
    /// <see cref="ErrorRaised"/> instead.
    /// </summary>
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isRunning;
        private Exception? _lastError;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncRelayCommand"/> class.
        /// </summary>
        /// <param name="execute">The asynchronous delegate invoked by the command.</param>
        /// <param name="canExecute">An optional delegate deciding whether the command is available.</param>
        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <inheritdoc />
        public event EventHandler? CanExecuteChanged;

        /// <summary>
        /// Raised when the wrapped delegate throws. The exception is also
        /// available through <see cref="LastError"/>.
        /// </summary>
        public event EventHandler<Exception>? ErrorRaised;

        /// <summary>
        /// Indicates whether the wrapped delegate is currently running.
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// The exception thrown by the most recent failed execution, or
        /// <c>null</c> when the last execution succeeded (or none ran yet).
        /// Cleared at the start of every execution.
        /// </summary>
        public Exception? LastError => _lastError;

        /// <inheritdoc />
        public bool CanExecute(object? parameter)
        {
            return !_isRunning && (_canExecute is null || _canExecute());
        }

        /// <inheritdoc />
        public async void Execute(object? parameter)
        {
            await ExecuteAsync();
        }

        /// <summary>
        /// Runs the wrapped delegate, honouring <see cref="CanExecute"/>.
        /// Unlike a fire-and-forget invocation this overload can be awaited,
        /// which makes the command directly testable. Exceptions thrown by
        /// the delegate are captured, never rethrown.
        /// </summary>
        /// <returns>A task that completes when the execution finished.</returns>
        public async Task ExecuteAsync()
        {
            if (!CanExecute(null))
            {
                return;
            }

            _isRunning = true;
            _lastError = null;
            RaiseCanExecuteChanged();

            try
            {
                await _execute();
            }
            catch (Exception ex)
            {
                _lastError = ex;
                ErrorRaised?.Invoke(this, ex);
            }
            finally
            {
                _isRunning = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Raises <see cref="CanExecuteChanged"/>.
        /// </summary>
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
