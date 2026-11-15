using System;
using XNative.Platform.Input;
using Xunit;

namespace XNative.Platform.Tests.Input
{
    public sealed class DeadZoneTests
    {
        private const double Tolerance = 1e-9;

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.05)]
        [InlineData(-0.09)]
        [InlineData(0.1)]
        public void AxialInsideZoneIsZero(double value)
        {
            Assert.Equal(0.0, DeadZone.Apply(value, 0.1));
        }

        [Fact]
        public void AxialFullDeflectionIsPreserved()
        {
            Assert.Equal(1.0, DeadZone.Apply(1.0, 0.2), 9);
            Assert.Equal(-1.0, DeadZone.Apply(-1.0, 0.2), 9);
        }

        [Fact]
        public void AxialRescalesAboveTheZone()
        {
            // Halfway between the zone edge (0.2) and full (1.0) -> 0.5.
            Assert.Equal(0.5, DeadZone.Apply(0.6, 0.2), 9);
            Assert.Equal(-0.5, DeadZone.Apply(-0.6, 0.2), 9);
            // Just above the edge -> just above zero (no jump).
            double near = DeadZone.Apply(0.21, 0.2);
            Assert.True(near > 0.0 && near < 0.02);
        }

        [Fact]
        public void AxialZeroDeadZoneIsIdentity()
        {
            Assert.Equal(0.37, DeadZone.Apply(0.37, 0.0), 9);
            Assert.Equal(-0.37, DeadZone.Apply(-0.37, 0.0), 9);
        }

        [Fact]
        public void RadialInsideZoneIsZero()
        {
            DeadZone.Apply(0.05, -0.05, 0.2, out double x, out double y);
            Assert.Equal(0.0, x);
            Assert.Equal(0.0, y);
        }

        [Fact]
        public void RadialFullDeflectionIsPreserved()
        {
            DeadZone.Apply(1.0, 0.0, 0.25, out double x, out double y);
            Assert.Equal(1.0, x, 9);
            Assert.Equal(0.0, y, 9);
        }

        [Fact]
        public void RadialPreservesDirectionAndRescalesMagnitude()
        {
            // Input magnitude 0.5 at 45 degrees, zone 0.1:
            // scaled magnitude = (0.5 - 0.1) / 0.9 = 0.444...
            DeadZone.Apply(0.3535533905932738, 0.3535533905932738, 0.1, out double x, out double y);

            Assert.Equal(x, y, 9);
            double magnitude = Math.Sqrt((x * x) + (y * y));
            Assert.Equal((0.5 - 0.1) / 0.9, magnitude, 9);
        }

        [Fact]
        public void RadialZeroInputIsZeroEvenWithZeroZone()
        {
            DeadZone.Apply(0.0, 0.0, 0.0, out double x, out double y);
            Assert.Equal(0.0, x);
            Assert.Equal(0.0, y);
        }

        [Theory]
        [InlineData(-0.1)]
        [InlineData(1.0)]
        [InlineData(double.NaN)]
        public void InvalidDeadZoneThrows(double deadZone)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DeadZone.Apply(0.5, deadZone));
            Assert.Throws<ArgumentOutOfRangeException>(() => DeadZone.Apply(0.5, 0.5, deadZone, out _, out _));
        }

        [Theory]
        [InlineData(1.01)]
        [InlineData(-1.01)]
        [InlineData(double.NaN)]
        public void InvalidAxisThrows(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DeadZone.Apply(value, 0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => DeadZone.Apply(value, 0.0, 0.1, out _, out _));
        }
    }
}
