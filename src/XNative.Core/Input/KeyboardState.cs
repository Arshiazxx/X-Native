using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace XNative.Core.Input
{
    /// <summary>
    /// Immutable snapshot of the keyboard: the set of keys pressed at
    /// one instant. The pressed set is copied on construction, so later
    /// changes to the caller's collection never leak into a snapshot,
    /// and the exposed collection is read-only.
    /// </summary>
    public sealed class KeyboardState
    {
        private readonly HashSet<KeyboardKey> _pressed;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyboardState"/> class.
        /// </summary>
        /// <param name="pressedKeys">
        /// The keys currently pressed. Duplicates are ignored and
        /// <see cref="KeyboardKey.None"/> is dropped.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pressedKeys"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is not a defined <see cref="KeyboardKey"/>.</exception>
        public KeyboardState(IEnumerable<KeyboardKey> pressedKeys)
        {
            if (pressedKeys == null)
            {
                throw new ArgumentNullException(nameof(pressedKeys));
            }

            _pressed = new HashSet<KeyboardKey>();
            foreach (KeyboardKey key in pressedKeys)
            {
                if (key == KeyboardKey.None)
                {
                    continue;
                }

                if (!Enum.IsDefined(typeof(KeyboardKey), key))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(pressedKeys), key, "Value is not a defined KeyboardKey.");
                }

                _pressed.Add(key);
            }

            PressedKeys = new ReadOnlyCollection<KeyboardKey>(new List<KeyboardKey>(_pressed));
        }

        /// <summary>The keys pressed in this snapshot, without duplicates.</summary>
        public IReadOnlyCollection<KeyboardKey> PressedKeys { get; }

        /// <summary>
        /// Reports whether <paramref name="key"/> is pressed in this snapshot.
        /// </summary>
        /// <param name="key">The key to test.</param>
        /// <returns><c>true</c> when the key is pressed.</returns>
        public bool IsKeyDown(KeyboardKey key)
        {
            return key != KeyboardKey.None && _pressed.Contains(key);
        }

        /// <summary>A snapshot with no keys pressed.</summary>
        public static KeyboardState Empty { get; } =
            new KeyboardState(new KeyboardKey[0]);
    }
}
