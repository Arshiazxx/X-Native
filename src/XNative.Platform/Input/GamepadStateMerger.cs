using System;
using XNative.Core.Input;

namespace XNative.Platform.Input
{
    /// <summary>
    /// Merges two gamepad snapshots into one. X-Native can see the
    /// same player through more than one source — for example a
    /// physical pad plus a phone-as-controller feed into the same
    /// player slot — and the game should see the union of both, not
    /// whichever source happened to be polled last.
    /// </summary>
    /// <remarks>
    /// Merge rules: buttons are OR-ed (a press on either source
    /// counts); each trigger takes the larger value (the more-pressed
    /// trigger wins); each thumbstick takes the deflection with the
    /// larger magnitude as a pair, so a stick's X and Y always come
    /// from the same source and direction is never mixed. The result
    /// is connected when either source is connected, and carries the
    /// later of the two timestamps.
    /// </remarks>
    public static class GamepadStateMerger
    {
        /// <summary>
        /// Merges <paramref name="first"/> and <paramref name="second"/>
        /// into a new snapshot.
        /// </summary>
        /// <param name="first">The first source snapshot.</param>
        /// <param name="second">The second source snapshot.</param>
        /// <returns>The merged snapshot.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either snapshot is null.</exception>
        public static GamepadState Merge(GamepadState first, GamepadState second)
        {
            if (first == null)
            {
                throw new ArgumentNullException(nameof(first));
            }

            if (second == null)
            {
                throw new ArgumentNullException(nameof(second));
            }

            double leftX = first.LeftThumbX;
            double leftY = first.LeftThumbY;
            if (MagnitudeSquared(second.LeftThumbX, second.LeftThumbY)
                > MagnitudeSquared(first.LeftThumbX, first.LeftThumbY))
            {
                leftX = second.LeftThumbX;
                leftY = second.LeftThumbY;
            }

            double rightX = first.RightThumbX;
            double rightY = first.RightThumbY;
            if (MagnitudeSquared(second.RightThumbX, second.RightThumbY)
                > MagnitudeSquared(first.RightThumbX, first.RightThumbY))
            {
                rightX = second.RightThumbX;
                rightY = second.RightThumbY;
            }

            DateTime timestamp = first.TimestampUtc >= second.TimestampUtc
                ? first.TimestampUtc
                : second.TimestampUtc;

            return new GamepadState(
                first.Buttons | second.Buttons,
                Math.Max(first.LeftTrigger, second.LeftTrigger),
                Math.Max(first.RightTrigger, second.RightTrigger),
                leftX,
                leftY,
                rightX,
                rightY,
                first.IsConnected || second.IsConnected,
                timestamp);
        }

        private static double MagnitudeSquared(double x, double y)
        {
            return (x * x) + (y * y);
        }
    }
}
