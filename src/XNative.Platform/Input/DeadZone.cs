using System;

namespace XNative.Platform.Input
{
    /// <summary>
    /// Thumbstick dead-zone filtering. Values inside the dead zone
    /// collapse to zero (a resting stick rarely reports exactly zero);
    /// values outside it are rescaled so the response starts at zero
    /// at the edge of the zone and still reaches full deflection at
    /// the stick's limit — no jump at the boundary and no lost range.
    /// </summary>
    public static class DeadZone
    {
        /// <summary>
        /// Applies an axial dead zone to a single axis value.
        /// </summary>
        /// <param name="value">Axis value, -1.0 to 1.0.</param>
        /// <param name="deadZone">Dead-zone radius, 0.0 (inclusive) to 1.0 (exclusive).</param>
        /// <returns>
        /// 0 when <paramref name="value"/> is inside the zone; otherwise
        /// the sign-preserving rescaled magnitude in -1..1.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="value"/> is outside -1..1 or
        /// <paramref name="deadZone"/> is outside 0..&lt;1 (NaN and
        /// infinity count as outside).
        /// </exception>
        public static double Apply(double value, double deadZone)
        {
            ValidateAxis(value, nameof(value));
            ValidateDeadZone(deadZone);

            double magnitude = Math.Abs(value);
            if (magnitude <= deadZone)
            {
                return 0.0;
            }

            double scaled = (magnitude - deadZone) / (1.0 - deadZone);
            return value < 0.0 ? -scaled : scaled;
        }

        /// <summary>
        /// Applies a radial dead zone to a thumbstick pair: the zone is
        /// a circle, so diagonal drift is filtered the same as
        /// cardinal drift, and the direction of the deflection is
        /// preserved exactly.
        /// </summary>
        /// <param name="x">Stick X axis, -1.0 to 1.0.</param>
        /// <param name="y">Stick Y axis, -1.0 to 1.0.</param>
        /// <param name="deadZone">Dead-zone radius, 0.0 (inclusive) to 1.0 (exclusive).</param>
        /// <param name="resultX">The filtered X axis.</param>
        /// <param name="resultY">The filtered Y axis.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when an axis is outside -1..1 or
        /// <paramref name="deadZone"/> is outside 0..&lt;1 (NaN and
        /// infinity count as outside).
        /// </exception>
        public static void Apply(double x, double y, double deadZone, out double resultX, out double resultY)
        {
            ValidateAxis(x, nameof(x));
            ValidateAxis(y, nameof(y));
            ValidateDeadZone(deadZone);

            double magnitude = Math.Sqrt((x * x) + (y * y));
            if (magnitude <= deadZone || magnitude == 0.0)
            {
                resultX = 0.0;
                resultY = 0.0;
                return;
            }

            // A stick can report a magnitude slightly above 1 on the
            // diagonals of a square gate; clamp before rescaling so
            // full deflection still maps to exactly 1.
            double clamped = Math.Min(magnitude, 1.0);
            double scaled = (clamped - deadZone) / (1.0 - deadZone);
            double factor = scaled / magnitude;
            resultX = x * factor;
            resultY = y * factor;
        }

        private static void ValidateAxis(double value, string paramName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < -1.0 || value > 1.0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Axis value must be between -1 and 1 inclusive.");
            }
        }

        private static void ValidateDeadZone(double deadZone)
        {
            if (double.IsNaN(deadZone) || double.IsInfinity(deadZone) || deadZone < 0.0 || deadZone >= 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(deadZone), deadZone, "Dead zone must be at least 0 and less than 1.");
            }
        }
    }
}
