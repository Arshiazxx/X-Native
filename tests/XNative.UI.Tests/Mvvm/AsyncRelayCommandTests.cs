using System;
using System.Threading.Tasks;
using System.Windows.Input;
using XNative.UI.Mvvm;
using Xunit;

namespace XNative.UI.Tests.Mvvm
{
    public sealed class AsyncRelayCommandTests
    {
        [Fact]
        public async Task ExecuteAsync_RunsTheDelegate()
        {
            bool ran = false;
            var command = new AsyncRelayCommand(() =>
            {
                ran = true;
                return Task.CompletedTask;
            });

            await command.ExecuteAsync();

            Assert.True(ran);
            Assert.False(command.IsRunning);
            Assert.Null(command.LastError);
        }

        [Fact]
        public async Task CanExecute_IsFalseWhileRunning_AndCanExecuteChangedFires()
        {
            var gate = new TaskCompletionSource<bool>();
            var command = new AsyncRelayCommand(() => gate.Task);
            int canExecuteChangedCount = 0;
            command.CanExecuteChanged += (_, __) => canExecuteChangedCount++;

            Task execution = command.ExecuteAsync();

            Assert.True(command.IsRunning);
            Assert.False(command.CanExecute(null));

            gate.SetResult(true);
            await execution;

            Assert.False(command.IsRunning);
            Assert.True(command.CanExecute(null));
            Assert.True(canExecuteChangedCount >= 2);
        }

        [Fact]
        public async Task ExecuteAsync_CapturesException_IntoLastErrorAndErrorRaised()
        {
            var failure = new InvalidOperationException("boom");
            var command = new AsyncRelayCommand(() => throw failure);
            Exception? raised = null;
            command.ErrorRaised += (_, ex) => raised = ex;

            // Must not throw: the command captures the exception instead.
            await command.ExecuteAsync();

            Assert.Same(failure, command.LastError);
            Assert.Same(failure, raised);
            Assert.False(command.IsRunning);
            Assert.True(command.CanExecute(null));
        }

        [Fact]
        public async Task ExecuteAsync_SuccessfulRun_ClearsPreviousError()
        {
            bool shouldFail = true;
            var command = new AsyncRelayCommand(() =>
            {
                if (shouldFail)
                {
                    throw new InvalidOperationException("first run fails");
                }

                return Task.CompletedTask;
            });

            await command.ExecuteAsync();
            Assert.NotNull(command.LastError);

            shouldFail = false;
            await command.ExecuteAsync();

            Assert.Null(command.LastError);
        }

        [Fact]
        public async Task ExecuteAsync_DoesNotRun_WhenCanExecuteDelegateReturnsFalse()
        {
            bool ran = false;
            var command = new AsyncRelayCommand(
                () =>
                {
                    ran = true;
                    return Task.CompletedTask;
                },
                () => false);

            Assert.False(command.CanExecute(null));

            await command.ExecuteAsync();

            Assert.False(ran);
        }

        [Fact]
        public async Task Execute_ThroughICommand_RunsTheDelegate()
        {
            var gate = new TaskCompletionSource<bool>();
            var command = new AsyncRelayCommand(async () =>
            {
                await Task.Delay(10);
                gate.SetResult(true);
            });

            ((ICommand)command).Execute(null);

            bool completed = await Task.WhenAny(gate.Task, Task.Delay(5000)) == gate.Task;
            Assert.True(completed);
            Assert.False(command.IsRunning);
        }
    }
}
