using System;
using XNative.Core.Abstractions;
using XNative.Core.Input;

namespace XNative.Platform.Input
{
    /// <summary>
    /// <see cref="IInputService"/> that reports no input at all: every
    /// slot is disconnected and the keyboard is empty. This is the
    /// honest default for a head that has no input backend yet — input
    /// consumers can be constructed and exercised, and "no pad" is
    /// indistinguishable from a real unplugged pad, which is exactly
    /// the case consumers must already handle.
    /// </summary>
    public sealed class NullInputService : IInputService
    {
        /// <inheritdoc />
        public int ConnectedGamepadCount
        {
            get { return 0; }
        }

        /// <inheritdoc />
        public GamepadState GetGamepadState(int playerIndex)
        {
            ValidatePlayerIndex(playerIndex);
            return GamepadState.Disconnected;
        }

        /// <inheritdoc />
        public KeyboardState GetKeyboardState()
        {
            return KeyboardState.Empty;
        }

        internal static void ValidatePlayerIndex(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= InputConstants.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerIndex), playerIndex,
                    "Player index must be between 0 and " + (InputConstants.MaxPlayers - 1) + " inclusive.");
            }
        }
    }
}
