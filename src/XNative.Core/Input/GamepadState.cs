using System;

namespace XNative.Core.Input
{
    /// <summary>
    /// Immutable snapshot of one gamepad at one instant. A snapshot never
    /// changes after construction, so a state returned by
    /// <c>XNative.Core.Abstractions.IInputService</c> stays valid (and
    /// unchanged) no matter what the physical pad does afterwards.
    /// </summary>
    /// <remarks>
    /// This is a sealed class rather than a struct, matching the rest of
    /// the core model (<c>DeviceInfo</c>, <c>GameInfo</c>): snapshots are
    /// shared by reference, compared by identity by default, and the two
    /// well-known values <see cref="Disconnected"/> and
    /// <see cref="Neutral"/> can be singletons.
    /// Analog values are validated, not clamped: a value outside its
    /// documented range (including <see cref="double.NaN"/> and infinity)
    /// throws <see cref="ArgumentOutOfRangeException"/>. Silently clamping
    /// would hide bugs in platform backends that feed this type.
    /// The button flags are validated the same way, matching
    /// <see cref="KeyboardState"/>'s rejection of undefined
    /// <see cref="KeyboardKey"/> values: any bit outside the defined
    /// <see cref="GamepadButtons"/> flags throws
    /// <see cref="ArgumentOutOfRangeException"/>. The timestamp must be
    /// UTC (<see cref="DateTimeKind.Utc"/>); any other kind throws
    /// <see cref="ArgumentException"/>, because consumers compare
    /// timestamps directly and a local or unspecified timestamp would
    /// silently compare against a different clock.
    /// </remarks>
    public sealed class GamepadState
    {
        private static readonly DateTime EpochUtc = new DateTime(0L, DateTimeKind.Utc);

        /// <summary>
        /// Every defined <see cref="GamepadButtons"/> flag, OR-ed
        /// together. A button combination is valid only when it sets
        /// no bit outside this mask.
        /// </summary>
        private const GamepadButtons AllDefinedButtons =
            GamepadButtons.A | GamepadButtons.B | GamepadButtons.X | GamepadButtons.Y
            | GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder
            | GamepadButtons.LeftStick | GamepadButtons.RightStick
            | GamepadButtons.DPadUp | GamepadButtons.DPadDown
            | GamepadButtons.DPadLeft | GamepadButtons.DPadRight
            | GamepadButtons.Menu | GamepadButtons.View | GamepadButtons.Guide;

        /// <summary>
        /// Initializes a new instance of the <see cref="GamepadState"/> class.
        /// </summary>
        /// <param name="buttons">The digital buttons currently pressed.</param>
        /// <param name="leftTrigger">Left trigger position, 0.0 (released) to 1.0 (fully pressed).</param>
        /// <param name="rightTrigger">Right trigger position, 0.0 (released) to 1.0 (fully pressed).</param>
        /// <param name="leftThumbX">Left thumbstick X axis, -1.0 to 1.0.</param>
        /// <param name="leftThumbY">Left thumbstick Y axis, -1.0 to 1.0.</param>
        /// <param name="rightThumbX">Right thumbstick X axis, -1.0 to 1.0.</param>
        /// <param name="rightThumbY">Right thumbstick Y axis, -1.0 to 1.0.</param>
        /// <param name="isConnected">Whether a pad backs this snapshot.</param>
        /// <param name="timestampUtc">When this snapshot was captured, in UTC.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when a trigger is outside 0..1 or a thumbstick axis is
        /// outside -1..1 (NaN and infinity count as outside), or when
        /// <paramref name="buttons"/> sets a bit that is not a defined
        /// <see cref="GamepadButtons"/> flag.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="timestampUtc"/> does not have
        /// <see cref="DateTimeKind.Utc"/> kind.
        /// </exception>
        public GamepadState(
            GamepadButtons buttons,
            double leftTrigger,
            double rightTrigger,
            double leftThumbX,
            double leftThumbY,
            double rightThumbX,
            double rightThumbY,
            bool isConnected,
            DateTime timestampUtc)
        {
            Buttons = ValidateButtons(buttons);
            LeftTrigger = ValidateRange(leftTrigger, 0.0, 1.0, nameof(leftTrigger));
            RightTrigger = ValidateRange(rightTrigger, 0.0, 1.0, nameof(rightTrigger));
            LeftThumbX = ValidateRange(leftThumbX, -1.0, 1.0, nameof(leftThumbX));
            LeftThumbY = ValidateRange(leftThumbY, -1.0, 1.0, nameof(leftThumbY));
            RightThumbX = ValidateRange(rightThumbX, -1.0, 1.0, nameof(rightThumbX));
            RightThumbY = ValidateRange(rightThumbY, -1.0, 1.0, nameof(rightThumbY));
            IsConnected = isConnected;
            TimestampUtc = ValidateTimestampUtc(timestampUtc);
        }

        /// <summary>Digital buttons currently pressed.</summary>
        public GamepadButtons Buttons { get; }

        /// <summary>Left trigger position, 0.0 to 1.0.</summary>
        public double LeftTrigger { get; }

        /// <summary>Right trigger position, 0.0 to 1.0.</summary>
        public double RightTrigger { get; }

        /// <summary>Left thumbstick X axis, -1.0 to 1.0.</summary>
        public double LeftThumbX { get; }

        /// <summary>Left thumbstick Y axis, -1.0 to 1.0.</summary>
        public double LeftThumbY { get; }

        /// <summary>Right thumbstick X axis, -1.0 to 1.0.</summary>
        public double RightThumbX { get; }

        /// <summary>Right thumbstick Y axis, -1.0 to 1.0.</summary>
        public double RightThumbY { get; }

        /// <summary>Whether a pad backs this snapshot.</summary>
        public bool IsConnected { get; }

        /// <summary>When this snapshot was captured, in UTC.</summary>
        public DateTime TimestampUtc { get; }

        /// <summary>
        /// Reports whether <paramref name="button"/> is pressed. For a
        /// combination of flags, every flag in the combination must be
        /// pressed.
        /// </summary>
        /// <param name="button">The button (or combination) to test.</param>
        /// <returns><c>true</c> when all tested buttons are pressed.</returns>
        public bool IsButtonDown(GamepadButtons button)
        {
            return (Buttons & button) == button && button != GamepadButtons.None;
        }

        /// <summary>
        /// The state of a player slot with no pad connected: not
        /// connected, no buttons, all axes at rest. Its timestamp is the
        /// UTC epoch because no capture ever happened.
        /// </summary>
        public static GamepadState Disconnected { get; } = new GamepadState(
            GamepadButtons.None, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, false, EpochUtc);

        /// <summary>
        /// A connected pad at rest: connected, no buttons, all axes at
        /// zero. Useful as the starting state for event-driven backends.
        /// Its timestamp is the UTC epoch; backends that feed real input
        /// should construct states with real capture timestamps.
        /// </summary>
        public static GamepadState Neutral { get; } = new GamepadState(
            GamepadButtons.None, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, true, EpochUtc);

        private static GamepadButtons ValidateButtons(GamepadButtons buttons)
        {
            if ((buttons & ~AllDefinedButtons) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(buttons), buttons, "Value sets a bit that is not a defined GamepadButtons flag.");
            }

            return buttons;
        }

        private static DateTime ValidateTimestampUtc(DateTime timestampUtc)
        {
            if (timestampUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "The timestamp must be in UTC (DateTimeKind.Utc).", nameof(timestampUtc));
            }

            return timestampUtc;
        }

        private static double ValidateRange(double value, double min, double max, string paramName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
            {
                throw new ArgumentOutOfRangeException(
                    paramName, value, "Value must be between " + min + " and " + max + " inclusive.");
            }

            return value;
        }
    }
}
