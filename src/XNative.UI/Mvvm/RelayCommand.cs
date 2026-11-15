using System;
using System.Windows.Input;

namespace XNative.UI.Mvvm
{
    /// <summary>
    /// <see cref="ICommand"/> backed by delegates. <see cref="Execute"/>
    /// honours <see cref="CanExecute"/>: when the command cannot execute,
    /// the call is a no-op and the delegate is not invoked.
    /// </summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCommand"/> class.
        /// </summary>
        /// <param name="execute">The delegate invoked by the command.</param>
        /// <param name="canExecute">An optional delegate deciding whether the command is available.</param>
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <inheritdoc />
        public event EventHandler? CanExecuteChanged;

        /// <inheritdoc />
        public bool CanExecute(object? parameter) => _canExecute is null || _canExecute(parameter);

        /// <inheritdoc />
        public void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            _execute(parameter);
        }

        /// <summary>
        /// Raises <see cref="CanExecuteChanged"/>.
        /// </summary>
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
