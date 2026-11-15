using System;

namespace XNative.Core.Devices
{
    /// <summary>
    /// Immutable description of the device the application runs on. The values
    /// are produced by the host that owns the platform APIs; the core only
    /// defines their shape.
    /// </summary>
    public sealed class DeviceInfo
    {
        /// <summary>
        /// Value used whenever the host cannot determine a field.
        /// </summary>
        public const string UnknownValue = "Unknown";

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceInfo"/> class.
        /// </summary>
        /// <param name="deviceFamily">The device family, for example <c>Windows.Xbox</c>.</param>
        /// <param name="operatingSystemVersion">The version of the operating system.</param>
        /// <param name="architecture">The processor architecture of the current process.</param>
        /// <param name="manufacturer">The manufacturer reported by the device.</param>
        /// <param name="model">The model reported by the device.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        /// <exception cref="ArgumentException">Thrown when required parameters are empty.</exception>
        public DeviceInfo(
            string deviceFamily,
            string operatingSystemVersion,
            string architecture,
            string manufacturer,
            string model)
        {
            DeviceFamily = deviceFamily ?? throw new ArgumentNullException(nameof(deviceFamily));
            OperatingSystemVersion = operatingSystemVersion ?? throw new ArgumentNullException(nameof(operatingSystemVersion));
            Architecture = architecture ?? throw new ArgumentNullException(nameof(architecture));
            Manufacturer = manufacturer ?? throw new ArgumentNullException(nameof(manufacturer));
            Model = model ?? throw new ArgumentNullException(nameof(model));

            if (string.IsNullOrWhiteSpace(DeviceFamily))
                throw new ArgumentException("DeviceFamily cannot be empty.", nameof(deviceFamily));
            if (string.IsNullOrWhiteSpace(OperatingSystemVersion))
                throw new ArgumentException("OperatingSystemVersion cannot be empty.", nameof(operatingSystemVersion));
            if (string.IsNullOrWhiteSpace(Architecture))
                throw new ArgumentException("Architecture cannot be empty.", nameof(architecture));
            if (string.IsNullOrWhiteSpace(Manufacturer))
                throw new ArgumentException("Manufacturer cannot be empty.", nameof(manufacturer));
            if (string.IsNullOrWhiteSpace(Model))
                throw new ArgumentException("Model cannot be empty.", nameof(model));
        }

        /// <summary>
        /// Device family reported by the host, for example <c>Windows.Xbox</c>
        /// or <c>Windows.Desktop</c>.
        /// </summary>
        public string DeviceFamily { get; }

        /// <summary>
        /// Version of the operating system.
        /// </summary>
        public string OperatingSystemVersion { get; }

        /// <summary>
        /// Processor architecture of the current process.
        /// </summary>
        public string Architecture { get; }

        /// <summary>
        /// Manufacturer reported by the device.
        /// </summary>
        public string Manufacturer { get; }

        /// <summary>
        /// Model reported by the device.
        /// </summary>
        public string Model { get; }

        /// <summary>
        /// Indicates whether the device belongs to the Xbox device family.
        /// This is <c>true</c> only when <see cref="DeviceFamily"/> is the
        /// Xbox family itself: exactly <c>Windows.Xbox</c>, exactly
        /// <c>Xbox</c>, or a dotted refinement of it such as
        /// <c>Windows.Xbox.SeriesX</c> (compared case-insensitively).
        /// A family that merely contains "Xbox" as a substring of a longer
        /// segment, for example <c>Windows.XboxOne</c> or
        /// <c>Windows.NotXbox</c>, does not count.
        /// </summary>
        public bool IsXbox =>
            string.Equals(DeviceFamily, "Windows.Xbox", StringComparison.OrdinalIgnoreCase)
            || string.Equals(DeviceFamily, "Xbox", StringComparison.OrdinalIgnoreCase)
            || DeviceFamily.StartsWith("Windows.Xbox.", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Placeholder used when nothing could be detected.
        /// </summary>
        /// <returns>A <see cref="DeviceInfo"/> with every field unknown.</returns>
        public static DeviceInfo CreateUnknown()
        {
            return new DeviceInfo(UnknownValue, UnknownValue, UnknownValue, UnknownValue, UnknownValue);
        }
    }
}
