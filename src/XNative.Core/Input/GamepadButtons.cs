using System;

namespace XNative.Core.Input
{
    /// <summary>
    /// Digital buttons of a gamepad. Analog controls (triggers and
    /// thumbsticks) are not buttons and live on
    /// <see cref="GamepadState"/> as axes instead.
    /// </summary>
    [Flags]
    public enum GamepadButtons
    {
        /// <summary>No button is pressed.</summary>
        None = 0,

        /// <summary>The A (cross / bottom) face button.</summary>
        A = 1 << 0,

        /// <summary>The B (circle / right) face button.</summary>
        B = 1 << 1,

        /// <summary>The X (square / left) face button.</summary>
        X = 1 << 2,

        /// <summary>The Y (triangle / top) face button.</summary>
        Y = 1 << 3,

        /// <summary>The left shoulder (bumper) button.</summary>
        LeftShoulder = 1 << 4,

        /// <summary>The right shoulder (bumper) button.</summary>
        RightShoulder = 1 << 5,

        /// <summary>The left thumbstick pressed in as a button.</summary>
        LeftStick = 1 << 6,

        /// <summary>The right thumbstick pressed in as a button.</summary>
        RightStick = 1 << 7,

        /// <summary>The directional pad up direction.</summary>
        DPadUp = 1 << 8,

        /// <summary>The directional pad down direction.</summary>
        DPadDown = 1 << 9,

        /// <summary>The directional pad left direction.</summary>
        DPadLeft = 1 << 10,

        /// <summary>The directional pad right direction.</summary>
        DPadRight = 1 << 11,

        /// <summary>The Menu (start) button.</summary>
        Menu = 1 << 12,

        /// <summary>The View (back / select) button.</summary>
        View = 1 << 13,

        /// <summary>The Guide (Xbox / home) button.</summary>
        Guide = 1 << 14,
    }
}
