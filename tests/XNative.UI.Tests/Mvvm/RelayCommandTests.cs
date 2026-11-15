using System;
using System.Windows.Input;
using XNative.UI.Mvvm;
using Xunit;

namespace XNative.UI.Tests.Mvvm
{
    public sealed class RelayCommandTests
    {
        [Fact]
        public void Constructor_NullExecute_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RelayCommand(null!));
        }

        [Fact]
        public void CanExecute_WithoutDelegate_ReturnsTrue()
        {
            var command = new RelayCommand(_ => { });

            Assert.True(command.CanExecute(null));
            Assert.True(command.CanExecute("anything"));
        }

        [Fact]
        public void Execute_WithoutCanExecute_RunsDelegate()
        {
            int runCount = 0;
            var command = new RelayCommand(_ => runCount++);

            command.Execute(null);

            Assert.Equal(1, runCount);
        }

        [Fact]
        public void Execute_PassesParameterToDelegate()
        {
            object? received = null;
            var command = new RelayCommand(parameter => received = parameter);

            command.Execute("payload");

            Assert.Equal("payload", received);
        }

        [Fact]
        public void Execute_WhenCanExecuteReturnsTrue_RunsDelegate()
        {
            int runCount = 0;
            var command = new RelayCommand(_ => runCount++, _ => true);

            command.Execute(null);

            Assert.Equal(1, runCount);
        }

        [Fact]
        public void Execute_WhenCanExecuteReturnsFalse_DoesNotRunDelegate()
        {
            // Pins the standard ICommand contract (QA finding M2): a
            // command whose CanExecute is false must be a no-op, exactly
            // like AsyncRelayCommand already behaves.
            int runCount = 0;
            var command = new RelayCommand(_ => runCount++, _ => false);

            Assert.False(command.CanExecute(null));
            command.Execute(null);

            Assert.Equal(0, runCount);
        }

        [Fact]
        public void Execute_WhenCanExecuteRejectsTheParameter_DoesNotRunDelegate()
        {
            int runCount = 0;
            var command = new RelayCommand(
                _ => runCount++,
                parameter => parameter is string text && text == "go");

            command.Execute("stop");
            Assert.Equal(0, runCount);

            command.Execute("go");
            Assert.Equal(1, runCount);
        }

        [Fact]
        public void CanExecuteChanged_FiresWhenRaised()
        {
            var command = new RelayCommand(_ => { });
            int fired = 0;
            command.CanExecuteChanged += (_, __) => fired++;

            command.RaiseCanExecuteChanged();

            Assert.Equal(1, fired);
        }

        [Fact]
        public void Command_IsUsableThroughICommandInterface()
        {
            int runCount = 0;
            ICommand command = new RelayCommand(_ => runCount++, _ => false);

            command.Execute(null);

            Assert.Equal(0, runCount);
        }
    }
}
