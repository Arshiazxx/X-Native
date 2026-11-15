using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using XNative.Core.Abstractions;
using XNative.Core.Input;
using XNative.Platform.Input;
using Xunit;

namespace XNative.Platform.Tests.Input
{
    public sealed class InputServiceTests
    {
        private static readonly DateTime Stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Later = Stamp.AddSeconds(1);

        private static GamepadState State(
            GamepadButtons buttons = GamepadButtons.None,
            double leftTrigger = 0.0, double rightTrigger = 0.0,
            double leftThumbX = 0.0, double leftThumbY = 0.0,
            bool connected = true, DateTime? stamp = null)
        {
            return new GamepadState(
                buttons, leftTrigger, rightTrigger, leftThumbX, leftThumbY,
                0.0, 0.0, connected, stamp ?? Stamp);
        }

        // ---- NullInputService ----

        [Fact]
        public void NullReportsEverythingDisconnected()
        {
            var service = new NullInputService();

            Assert.Equal(0, service.ConnectedGamepadCount);
            Assert.Same(GamepadState.Disconnected, service.GetGamepadState(0));
            Assert.Same(KeyboardState.Empty, service.GetKeyboardState());
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(4)]
        [InlineData(int.MaxValue)]
        public void NullRejectsOutOfRangePlayerIndex(int index)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NullInputService().GetGamepadState(index));
        }

        // ---- ManualInputService ----

        [Fact]
        public void ManualStartsDisconnected()
        {
            var service = new ManualInputService();

            Assert.Equal(0, service.ConnectedGamepadCount);
            Assert.Same(GamepadState.Disconnected, service.GetGamepadState(3));
            Assert.Empty(service.GetKeyboardState().PressedKeys);
        }

        [Fact]
        public void ManualReturnsWhatWasSetPerSlot()
        {
            var service = new ManualInputService();
            GamepadState pad0 = State(GamepadButtons.A);
            GamepadState pad2 = State(GamepadButtons.B, connected: false);

            service.SetGamepadState(0, pad0);
            service.SetGamepadState(2, pad2);

            Assert.Same(pad0, service.GetGamepadState(0));
            Assert.Same(GamepadState.Disconnected, service.GetGamepadState(1));
            Assert.Same(pad2, service.GetGamepadState(2));
            Assert.Equal(1, service.ConnectedGamepadCount);
        }

        [Fact]
        public void ManualSnapshotIsIsolatedFromLaterSets()
        {
            var service = new ManualInputService();
            service.SetGamepadState(0, State(GamepadButtons.A));

            GamepadState snapshot = service.GetGamepadState(0);
            service.SetGamepadState(0, State(GamepadButtons.B, leftTrigger: 1.0));

            // The earlier snapshot is immutable: still the A state.
            Assert.Equal(GamepadButtons.A, snapshot.Buttons);
            Assert.Equal(0.0, snapshot.LeftTrigger);
            Assert.Equal(GamepadButtons.B, service.GetGamepadState(0).Buttons);
        }

        [Fact]
        public void ManualKeyboardRoundTripsAndResetClears()
        {
            var service = new ManualInputService();
            service.SetKeyboardState(new KeyboardState(new[] { KeyboardKey.Enter }));
            service.SetGamepadState(1, State());

            Assert.True(service.GetKeyboardState().IsKeyDown(KeyboardKey.Enter));
            Assert.Equal(1, service.ConnectedGamepadCount);

            service.Reset();

            Assert.Empty(service.GetKeyboardState().PressedKeys);
            Assert.Equal(0, service.ConnectedGamepadCount);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(4)]
        public void ManualRejectsOutOfRangePlayerIndex(int index)
        {
            var service = new ManualInputService();
            Assert.Throws<ArgumentOutOfRangeException>(() => service.GetGamepadState(index));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.SetGamepadState(index, State()));
        }

        [Fact]
        public void ManualRejectsNullStates()
        {
            var service = new ManualInputService();
            Assert.Throws<ArgumentNullException>(() => service.SetGamepadState(0, null!));
            Assert.Throws<ArgumentNullException>(() => service.SetKeyboardState(null!));
        }

        // ---- Merger ----

        [Fact]
        public void MergerOrsButtonsAndMaxesTriggers()
        {
            GamepadState merged = GamepadStateMerger.Merge(
                State(GamepadButtons.A, leftTrigger: 0.3, rightTrigger: 0.9),
                State(GamepadButtons.B, leftTrigger: 0.8, rightTrigger: 0.1));

            Assert.Equal(GamepadButtons.A | GamepadButtons.B, merged.Buttons);
            Assert.Equal(0.8, merged.LeftTrigger);
            Assert.Equal(0.9, merged.RightTrigger);
        }

        [Fact]
        public void MergerTakesTheLargerStickDeflectionAsAPair()
        {
            GamepadState merged = GamepadStateMerger.Merge(
                State(leftThumbX: 0.2, leftThumbY: 0.9),
                State(leftThumbX: -0.6, leftThumbY: 0.1));

            // First stick magnitude^2 = 0.85, second = 0.37: first wins whole.
            Assert.Equal(0.2, merged.LeftThumbX);
            Assert.Equal(0.9, merged.LeftThumbY);
        }

        [Fact]
        public void MergerConnectivityAndTimestampTakeTheUnionAndLatest()
        {
            GamepadState merged = GamepadStateMerger.Merge(
                State(connected: false, stamp: Later),
                State(connected: true, stamp: Stamp));

            Assert.True(merged.IsConnected);
            Assert.Equal(Later, merged.TimestampUtc);
        }

        [Fact]
        public void MergerRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => GamepadStateMerger.Merge(null!, State()));
            Assert.Throws<ArgumentNullException>(() => GamepadStateMerger.Merge(State(), null!));
        }

        // ---- Module registration ----

        [Fact]
        public void AddXNativeInputResolvesInputServiceAsTheSharedManualInstance()
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddXNativeInput()
                .BuildServiceProvider();

            IInputService input = provider.GetRequiredService<IInputService>();
            ManualInputService manual = provider.GetRequiredService<ManualInputService>();

            Assert.IsType<ManualInputService>(input);
            Assert.Same(manual, input);

            // Feeding the concrete instance is visible through the interface.
            manual.SetGamepadState(0, State(GamepadButtons.Guide));
            Assert.Equal(GamepadButtons.Guide, input.GetGamepadState(0).Buttons);
            Assert.Equal(1, input.ConnectedGamepadCount);
        }

        [Fact]
        public void AddXNativeInputRejectsNullServices()
        {
            Assert.Throws<ArgumentNullException>(() => InputModule.AddXNativeInput(null!));
        }
    }
}
