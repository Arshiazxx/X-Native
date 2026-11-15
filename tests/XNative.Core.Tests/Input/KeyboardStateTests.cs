using System;
using System.Collections.Generic;
using XNative.Core.Input;
using Xunit;

namespace XNative.Core.Tests.Input
{
    public sealed class KeyboardStateTests
    {
        [Fact]
        public void EmptyHasNoKeysDown()
        {
            Assert.Empty(KeyboardState.Empty.PressedKeys);
            Assert.False(KeyboardState.Empty.IsKeyDown(KeyboardKey.A));
            Assert.False(KeyboardState.Empty.IsKeyDown(KeyboardKey.None));
        }

        [Fact]
        public void PressedKeysAreReportedByIsKeyDown()
        {
            var state = new KeyboardState(new[] { KeyboardKey.W, KeyboardKey.Space, KeyboardKey.Shift });

            Assert.True(state.IsKeyDown(KeyboardKey.W));
            Assert.True(state.IsKeyDown(KeyboardKey.Space));
            Assert.False(state.IsKeyDown(KeyboardKey.S));
            Assert.Equal(3, state.PressedKeys.Count);
        }

        [Fact]
        public void DuplicatesAndNoneAreDropped()
        {
            var state = new KeyboardState(new[] { KeyboardKey.A, KeyboardKey.A, KeyboardKey.None });

            Assert.Single(state.PressedKeys);
            Assert.True(state.IsKeyDown(KeyboardKey.A));
        }

        [Fact]
        public void ConstructorCopiesTheSourceCollection()
        {
            var source = new List<KeyboardKey> { KeyboardKey.Enter };
            var state = new KeyboardState(source);

            source.Add(KeyboardKey.Escape);
            source.Clear();

            Assert.True(state.IsKeyDown(KeyboardKey.Enter));
            Assert.False(state.IsKeyDown(KeyboardKey.Escape));
        }

        [Fact]
        public void NullPressedKeysThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new KeyboardState(null!));
        }

        [Fact]
        public void UndefinedKeyThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new KeyboardState(new[] { (KeyboardKey)999 }));
        }
    }
}
