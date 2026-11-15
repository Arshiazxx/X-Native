using System;
using XNative.Core.Input;
using Xunit;

namespace XNative.Core.Tests.Input
{
    public sealed class GamepadStateTests
    {
        private static readonly DateTime Stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static GamepadState Create(
            double leftTrigger = 0.0, double rightTrigger = 0.0,
            double leftThumbX = 0.0, double leftThumbY = 0.0,
            double rightThumbX = 0.0, double rightThumbY = 0.0)
        {
            return new GamepadState(
                GamepadButtons.None, leftTrigger, rightTrigger,
                leftThumbX, leftThumbY, rightThumbX, rightThumbY, true, Stamp);
        }

        [Fact]
        public void ConstructorStoresAllValues()
        {
            var state = new GamepadState(
                GamepadButtons.A | GamepadButtons.Menu, 0.25, 0.75,
                -0.5, 0.5, 0.1, -0.1, true, Stamp);

            Assert.Equal(GamepadButtons.A | GamepadButtons.Menu, state.Buttons);
            Assert.Equal(0.25, state.LeftTrigger);
            Assert.Equal(0.75, state.RightTrigger);
            Assert.Equal(-0.5, state.LeftThumbX);
            Assert.Equal(0.5, state.LeftThumbY);
            Assert.Equal(0.1, state.RightThumbX);
            Assert.Equal(-0.1, state.RightThumbY);
            Assert.True(state.IsConnected);
            Assert.Equal(Stamp, state.TimestampUtc);
        }

        [Theory]
        [InlineData(-0.01)]
        [InlineData(1.01)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void TriggerOutsideRangeThrows(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(leftTrigger: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(rightTrigger: value));
        }

        [Theory]
        [InlineData(-1.01)]
        [InlineData(1.01)]
        [InlineData(double.NaN)]
        [InlineData(double.NegativeInfinity)]
        public void ThumbAxisOutsideRangeThrows(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(leftThumbX: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(leftThumbY: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(rightThumbX: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(rightThumbY: value));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        public void TriggerBoundariesAreAccepted(double value)
        {
            GamepadState state = Create(leftTrigger: value, rightTrigger: value);
            Assert.Equal(value, state.LeftTrigger);
        }

        [Theory]
        [InlineData(-1.0)]
        [InlineData(1.0)]
        public void ThumbBoundariesAreAccepted(double value)
        {
            GamepadState state = Create(leftThumbX: value, rightThumbY: value);
            Assert.Equal(value, state.LeftThumbX);
            Assert.Equal(value, state.RightThumbY);
        }

        [Fact]
        public void DisconnectedIsNotConnectedAndAtRest()
        {
            Assert.False(GamepadState.Disconnected.IsConnected);
            Assert.Equal(GamepadButtons.None, GamepadState.Disconnected.Buttons);
            Assert.Equal(0.0, GamepadState.Disconnected.LeftTrigger);
            Assert.Equal(0.0, GamepadState.Disconnected.LeftThumbX);
        }

        [Fact]
        public void NeutralIsConnectedAndAtRest()
        {
            Assert.True(GamepadState.Neutral.IsConnected);
            Assert.Equal(GamepadButtons.None, GamepadState.Neutral.Buttons);
            Assert.Equal(0.0, GamepadState.Neutral.RightTrigger);
            Assert.Equal(0.0, GamepadState.Neutral.RightThumbY);
        }

        [Fact]
        public void ButtonsCombineAndTestAsFlags()
        {
            GamepadButtons combined = GamepadButtons.A | GamepadButtons.DPadUp;
            Assert.True(combined.HasFlag(GamepadButtons.A));
            Assert.True(combined.HasFlag(GamepadButtons.DPadUp));
            Assert.False(combined.HasFlag(GamepadButtons.B));

            var state = new GamepadState(combined, 0, 0, 0, 0, 0, 0, true, Stamp);
            Assert.True(state.IsButtonDown(GamepadButtons.A));
            Assert.True(state.IsButtonDown(GamepadButtons.A | GamepadButtons.DPadUp));
            Assert.False(state.IsButtonDown(GamepadButtons.A | GamepadButtons.B));
            Assert.False(state.IsButtonDown(GamepadButtons.None));
        }

        [Fact]
        public void EveryButtonIsASingleBit()
        {
            foreach (GamepadButtons button in Enum.GetValues(typeof(GamepadButtons)))
            {
                if (button == GamepadButtons.None)
                {
                    continue;
                }

                int value = (int)button;
                Assert.True(value > 0 && (value & (value - 1)) == 0, button + " must be a single bit.");
            }
        }
    }
}
