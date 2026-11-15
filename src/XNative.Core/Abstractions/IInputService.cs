using System;
using XNative.Core.Input;

namespace XNative.Core.Abstractions
{
    /// <summary>
    /// Polling access to gamepad and keyboard input. Implemented in
    /// <c>XNative.Platform</c> (and, for event-driven operating-system
    /// APIs, fed by the application head — see
    /// <c>ManualInputService</c> there).
    /// </summary>
    /// <remarks>
    /// Snapshot semantics: every call returns the latest known state as
    /// an immutable snapshot. A call never blocks waiting for hardware.
    /// Implementations may cache at most one frame of state, but a
    /// snapshot returned earlier must never change when later input
    /// arrives — the state types are immutable, which makes that
    /// guarantee structural. Callers that need per-frame edges should
    /// keep the previous snapshot and diff it against the next one.
    /// Implementations must be safe to call from any thread.
    /// </remarks>
    public interface IInputService
    {
        /// <summary>
        /// Returns the latest known state of the gamepad in the given
        /// player slot. A slot with no pad returns
        /// <see cref="GamepadState.Disconnected"/>; this method never
        /// returns null.
        /// </summary>
        /// <param name="playerIndex">Player slot, 0 to <see cref="InputConstants.MaxPlayers"/> - 1.</param>
        /// <returns>The latest gamepad snapshot for the slot.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="playerIndex"/> is outside 0..<see cref="InputConstants.MaxPlayers"/> - 1.
        /// </exception>
        GamepadState GetGamepadState(int playerIndex);

        /// <summary>
        /// Returns the latest known keyboard state. When no keyboard
        /// input is available this returns
        /// <see cref="KeyboardState.Empty"/>; it never returns null.
        /// </summary>
        /// <returns>The latest keyboard snapshot.</returns>
        KeyboardState GetKeyboardState();

        /// <summary>
        /// How many player slots currently report a connected gamepad.
        /// Always between 0 and <see cref="InputConstants.MaxPlayers"/>.
        /// </summary>
        int ConnectedGamepadCount { get; }
    }
}
