using System;
using XNative.Core.Abstractions;
using XNative.Core.Input;

namespace XNative.Platform.Input
{
    /// <summary>
    /// Settable <see cref="IInputService"/>: the bridge for platform
    /// heads whose operating system delivers input as events rather
    /// than something that can be polled. The Xbox head, for example,
    /// receives <c>Windows.Gaming.Input</c> readings and connection
    /// events; it translates each one into a
    /// <see cref="GamepadState"/> and calls
    /// <see cref="SetGamepadState"/>, while the rest of the
    /// application keeps using the polling
    /// <see cref="IInputService"/> contract.
    /// </summary>
    /// <remarks>
    /// Thread safety: all members are safe to call from any thread.
    /// Reads and writes are guarded by one lock, and because the state
    /// types are immutable a snapshot handed out earlier can never be
    /// mutated by a later <see cref="SetGamepadState"/> call — snapshot
    /// isolation is structural, not a copy.
    /// </remarks>
    public sealed class ManualInputService : IInputService
    {
        private readonly object _lock = new object();
        private readonly GamepadState[] _gamepads = new GamepadState[InputConstants.MaxPlayers];
        private KeyboardState _keyboard = KeyboardState.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManualInputService"/> class
        /// with every slot disconnected and no keys pressed.
        /// </summary>
        public ManualInputService()
        {
            for (int i = 0; i < _gamepads.Length; i++)
            {
                _gamepads[i] = GamepadState.Disconnected;
            }
        }

        /// <inheritdoc />
        public int ConnectedGamepadCount
        {
            get
            {
                lock (_lock)
                {
                    int count = 0;
                    for (int i = 0; i < _gamepads.Length; i++)
                    {
                        if (_gamepads[i].IsConnected)
                        {
                            count++;
                        }
                    }

                    return count;
                }
            }
        }

        /// <inheritdoc />
        public GamepadState GetGamepadState(int playerIndex)
        {
            NullInputService.ValidatePlayerIndex(playerIndex);
            lock (_lock)
            {
                return _gamepads[playerIndex];
            }
        }

        /// <inheritdoc />
        public KeyboardState GetKeyboardState()
        {
            lock (_lock)
            {
                return _keyboard;
            }
        }

        /// <summary>
        /// Replaces the latest known state of one player slot. The
        /// state is stored as-is; it is immutable, so the caller may
        /// keep and reuse the instance safely.
        /// </summary>
        /// <param name="playerIndex">Player slot, 0 to <see cref="InputConstants.MaxPlayers"/> - 1.</param>
        /// <param name="state">The new snapshot for the slot.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="playerIndex"/> is out of range.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
        public void SetGamepadState(int playerIndex, GamepadState state)
        {
            NullInputService.ValidatePlayerIndex(playerIndex);
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            lock (_lock)
            {
                _gamepads[playerIndex] = state;
            }
        }

        /// <summary>
        /// Replaces the latest known keyboard state.
        /// </summary>
        /// <param name="state">The new keyboard snapshot.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
        public void SetKeyboardState(KeyboardState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            lock (_lock)
            {
                _keyboard = state;
            }
        }

        /// <summary>
        /// Returns every slot to disconnected and clears the keyboard,
        /// as if all devices were unplugged at once.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                for (int i = 0; i < _gamepads.Length; i++)
                {
                    _gamepads[i] = GamepadState.Disconnected;
                }

                _keyboard = KeyboardState.Empty;
            }
        }
    }
}
