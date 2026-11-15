using System;
using System.Runtime.InteropServices;
using XNative.Core.Abstractions;
using XNative.Core.Devices;

namespace XNative.App
{
    /// <summary>
    /// Reads the device description from the desktop runtime. This
    /// implementation lives in the console head because it relies on
    /// .NET runtime APIs that the .NET Standard module libraries
    /// cannot assume. Values that have no honest desktop source are
    /// reported as <see cref="DeviceInfo.UnknownValue"/> instead of
    /// being invented.
    /// </summary>
    public sealed class DesktopDeviceInfoService : IDeviceInfoService
    {
        private DeviceInfo? _cached;

        /// <inheritdoc />
        public DeviceInfo GetDeviceInfo()
        {
            return _cached ??= Detect();
        }

        private static DeviceInfo Detect()
        {
            string deviceFamily = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Windows.Desktop"
                : Fallback(RuntimeInformation.OSDescription);

            string operatingSystemVersion = Fallback(Environment.OSVersion.VersionString);
            string architecture = Fallback(RuntimeInformation.ProcessArchitecture.ToString());
            string manufacturer = DeviceInfo.UnknownValue;
            string model = Fallback(Environment.MachineName);

            return new DeviceInfo(deviceFamily, operatingSystemVersion, architecture, manufacturer, model);
        }

        private static string Fallback(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? DeviceInfo.UnknownValue : value!;
        }
    }
}
