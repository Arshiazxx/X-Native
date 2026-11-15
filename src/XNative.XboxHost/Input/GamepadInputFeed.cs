using System;
using System.Collections.Generic;
using Windows.Gaming.Input;
using Windows.UI.Xaml;
using XNative.Core.Input;
using XNative.Platform.Input;
using UwpGamepadButtons = Windows.Gaming.Input.GamepadButtons;

namespace XNative.XboxHost.Input
{
    /// <summary>
    /// Bridges the Xbox operating system's gamepad API
    /// (<c>Windows.Gaming.Input</c>) into the polling
    /// <see cref="XNative.Core.Abstractions.IInputService"/> contract by
    /// feeding every reading into the shared
    /// <see cref="ManualInputService"/> singleton.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Windows.Gaming.Input.Gamepad</c> is a poll-per-pad API: the
    /// feed polls every connected pad on a
    /// <see cref="DispatcherTimer"/> at a 16&#160;ms interval (the UI
    /// frame rate, so effectively ~60&#160;Hz) and translates each
    /// <see cref="GamepadReading"/> into an immutable
    /// <see cref="GamepadState"/>. The
    /// <see cref="Gamepad.GamepadAdded"/> and
    /// <see cref="Gamepad.GamepadRemoved"/> events only trigger an
    /// immediate extra poll, so the poll loop stays the single writer
    /// and connection changes are reflected without waiting for the
    /// next tick.
    /// </para>
    /// <para>
    /// The player index of a pad is its position in
    /// <see cref="Gamepad.Gamepads"/>, capped at
    /// <see cref="InputConstants.MaxPlayers"/>; pads beyond the cap are
    /// ignored, and when a pad disconnects the remaining pads shift
    /// down into the earlier slots. Slots with no pad are written
    /// <see cref="GamepadState.Disconnected"/> on every poll, so a
    /// removed pad can never leave a stale "connected" snapshot behind.
    /// </para>
    /// <para>
    /// Button mapping notes: the UWP <c>Paddle1</c>–<c>Paddle4</c>
    /// buttons have no counterpart in
    /// <see cref="XNative.Core.Input.GamepadButtons"/> and are dropped.
    /// The Core <see cref="XNative.Core.Input.GamepadButtons.Guide"/>
    /// button is never set by this feed: the system reserves the Xbox
    /// button and <see cref="GamepadReading"/> never reports it.
    /// </para>
    /// <para>
    /// This file is written against the UAP 10.0.19041
    /// <c>Windows.Gaming.Input</c> / <c>Windows.UI.Xaml</c> surface but
    /// has NOT been verified by a compiler: the legacy UWP head cannot
    /// be built outside Windows. It needs a Windows MSBuild pass before
    /// anyone relies on it.
    /// </para>
    /// </remarks>
    public sealed class GamepadInputFeed
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(16);

        private readonly ManualInputService _inputService;
        private readonly DispatcherTimer _timer;
        private readonly object _pollLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="GamepadInputFeed"/> class.
        /// </summary>
        /// <param name="inputService">
        /// The shared manual input service the readings are fed into.
        /// Resolve the concrete <see cref="ManualInputService"/> from
        /// the container: it is the same singleton that backs
        /// <see cref="XNative.Core.Abstractions.IInputService"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="inputService"/> is null.</exception>
        public GamepadInputFeed(ManualInputService inputService)
        {
            _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
            _timer = new DispatcherTimer { Interval = PollInterval };
            _timer.Tick += OnTimerTick;
        }

        /// <summary>
        /// Indicates whether the feed is currently polling.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Starts polling: subscribes to the connection events, takes
        /// one reading immediately, then polls on the timer. Calling
        /// this while already running does nothing.
        /// </summary>
        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            Gamepad.GamepadAdded += OnGamepadAdded;
            Gamepad.GamepadRemoved += OnGamepadRemoved;
            IsRunning = true;
            Poll();
            _timer.Start();
        }

        /// <summary>
        /// Stops polling and resets the input service, so every slot
        /// reports disconnected once the feed is gone. Calling this
        /// while already stopped does nothing beyond the reset.
        /// </summary>
        public void Stop()
        {
            _timer.Stop();
            Gamepad.GamepadAdded -= OnGamepadAdded;
            Gamepad.GamepadRemoved -= OnGamepadRemoved;
            IsRunning = false;
            _inputService.Reset();
        }

        private void OnTimerTick(object sender, object e)
        {
            Poll();
        }

        private void OnGamepadAdded(object sender, Gamepad gamepad)
        {
            Poll();
        }

        private void OnGamepadRemoved(object sender, Gamepad gamepad)
        {
            Poll();
        }

        private void Poll()
        {
            lock (_pollLock)
            {
                IReadOnlyList<Gamepad> gamepads = Gamepad.Gamepads;
                int count = Math.Min(gamepads.Count, InputConstants.MaxPlayers);

                for (int playerIndex = 0; playerIndex < count; playerIndex++)
                {
                    _inputService.SetGamepadState(playerIndex, ReadState(gamepads[playerIndex]));
                }

                for (int playerIndex = count; playerIndex < InputConstants.MaxPlayers; playerIndex++)
                {
                    _inputService.SetGamepadState(playerIndex, GamepadState.Disconnected);
                }
            }
        }

        private static GamepadState ReadState(Gamepad gamepad)
        {
            GamepadReading reading;

            try
            {
                reading = gamepad.GetCurrentReading();
            }
            catch (Exception)
            {
                // A pad can vanish between the Gamepads snapshot and
                // the read; report the slot as disconnected instead of
                // letting one dying pad fault the whole poll.
                return GamepadState.Disconnected;
            }

            return new GamepadState(
                MapButtons(reading.Buttons),
                Clamp(reading.LeftTrigger, 0.0, 1.0),
                Clamp(reading.RightTrigger, 0.0, 1.0),
                Clamp(reading.LeftThumbstickX, -1.0, 1.0),
                Clamp(reading.LeftThumbstickY, -1.0, 1.0),
                Clamp(reading.RightThumbstickX, -1.0, 1.0),
                Clamp(reading.RightThumbstickY, -1.0, 1.0),
                isConnected: true,
                timestampUtc: DateTime.UtcNow);
        }

        private static GamepadButtons MapButtons(UwpGamepadButtons buttons)
        {
            GamepadButtons mapped = GamepadButtons.None;

            if ((buttons & UwpGamepadButtons.A) != 0)
            {
                mapped |= GamepadButtons.A;
            }

            if ((buttons & UwpGamepadButtons.B) != 0)
            {
                mapped |= GamepadButtons.B;
            }

            if ((buttons & UwpGamepadButtons.X) != 0)
            {
                mapped |= GamepadButtons.X;
            }

            if ((buttons & UwpGamepadButtons.Y) != 0)
            {
                mapped |= GamepadButtons.Y;
            }

            if ((buttons & UwpGamepadButtons.LeftShoulder) != 0)
            {
                mapped |= GamepadButtons.LeftShoulder;
            }

            if ((buttons & UwpGamepadButtons.RightShoulder) != 0)
            {
                mapped |= GamepadButtons.RightShoulder;
            }

            if ((buttons & UwpGamepadButtons.LeftThumbstick) != 0)
            {
                mapped |= GamepadButtons.LeftStick;
            }

            if ((buttons & UwpGamepadButtons.RightThumbstick) != 0)
            {
                mapped |= GamepadButtons.RightStick;
            }

            if ((buttons & UwpGamepadButtons.DPadUp) != 0)
            {
                mapped |= GamepadButtons.DPadUp;
            }

            if ((buttons & UwpGamepadButtons.DPadDown) != 0)
            {
                mapped |= GamepadButtons.DPadDown;
            }

            if ((buttons & UwpGamepadButtons.DPadLeft) != 0)
            {
                mapped |= GamepadButtons.DPadLeft;
            }

            if ((buttons & UwpGamepadButtons.DPadRight) != 0)
            {
                mapped |= GamepadButtons.DPadRight;
            }

            if ((buttons & UwpGamepadButtons.Menu) != 0)
            {
                mapped |= GamepadButtons.Menu;
            }

            if ((buttons & UwpGamepadButtons.View) != 0)
            {
                mapped |= GamepadButtons.View;
            }

            // UwpGamepadButtons.Paddle1..Paddle4 have no Core
            // counterpart and are dropped. Core GamepadButtons.Guide
            // is never set: GamepadReading never reports it.
            return mapped;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            // GamepadState validates its analog values and throws on
            // out-of-range input; the readings are documented to be in
            // range, but a single bad value (NaN included) must never
            // fault the poll, so the feed clamps defensively.
            if (double.IsNaN(value))
            {
                return 0.0;
            }

            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }
    }
}
