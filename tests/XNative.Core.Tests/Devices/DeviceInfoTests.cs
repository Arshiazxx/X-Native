using System;
using XNative.Core.Devices;
using Xunit;

namespace XNative.Core.Tests.Devices
{
    public sealed class DeviceInfoTests
    {
        private const string Family = "Windows.Desktop";
        private const string OsVersion = "10.0.19041.0";
        private const string Architecture = "X64";
        private const string Manufacturer = "Microsoft";
        private const string Model = "Surface Pro";

        // Slot indexes map to constructor parameter order:
        // 0 deviceFamily, 1 operatingSystemVersion, 2 architecture,
        // 3 manufacturer, 4 model.
        private static readonly string[] ParamNames =
        {
            "deviceFamily",
            "operatingSystemVersion",
            "architecture",
            "manufacturer",
            "model",
        };

        private static DeviceInfo CreateWith(int slot, string? value)
        {
            string?[] values = { Family, OsVersion, Architecture, Manufacturer, Model };
            values[slot] = value;
            return new DeviceInfo(values[0]!, values[1]!, values[2]!, values[3]!, values[4]!);
        }

        [Fact]
        public void Constructor_ValidValues_StoresEveryProperty()
        {
            var info = new DeviceInfo(Family, OsVersion, Architecture, Manufacturer, Model);

            Assert.Equal(Family, info.DeviceFamily);
            Assert.Equal(OsVersion, info.OperatingSystemVersion);
            Assert.Equal(Architecture, info.Architecture);
            Assert.Equal(Manufacturer, info.Manufacturer);
            Assert.Equal(Model, info.Model);
        }

        [Fact]
        public void CreateUnknown_AllFieldsUnknown_AndNotXbox()
        {
            var info = DeviceInfo.CreateUnknown();

            Assert.Equal(DeviceInfo.UnknownValue, info.DeviceFamily);
            Assert.Equal(DeviceInfo.UnknownValue, info.OperatingSystemVersion);
            Assert.Equal(DeviceInfo.UnknownValue, info.Architecture);
            Assert.Equal(DeviceInfo.UnknownValue, info.Manufacturer);
            Assert.Equal(DeviceInfo.UnknownValue, info.Model);
            Assert.False(info.IsXbox);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void Constructor_NullValue_ThrowsArgumentNullException(int slot)
        {
            var exception = Assert.Throws<ArgumentNullException>(() => CreateWith(slot, null));

            Assert.Equal(ParamNames[slot], exception.ParamName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void Constructor_EmptyValue_ThrowsArgumentException(int slot)
        {
            var exception = Assert.Throws<ArgumentException>(() => CreateWith(slot, ""));

            Assert.Equal(ParamNames[slot], exception.ParamName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void Constructor_WhitespaceValue_ThrowsArgumentException(int slot)
        {
            var exception = Assert.Throws<ArgumentException>(() => CreateWith(slot, "   "));

            Assert.Equal(ParamNames[slot], exception.ParamName);
        }

        [Theory]
        [InlineData("Windows.Xbox")]
        [InlineData("windows.xbox")]
        [InlineData("WINDOWS.XBOX")]
        [InlineData("Xbox")]
        [InlineData("xbox")]
        [InlineData("Windows.Xbox.SeriesX")]
        public void IsXbox_KnownXboxFamilies_ReturnTrue(string deviceFamily)
        {
            var info = new DeviceInfo(deviceFamily, OsVersion, Architecture, Manufacturer, Model);

            Assert.True(info.IsXbox);
        }

        [Theory]
        [InlineData("Windows.Desktop")]
        [InlineData("Windows.Mobile")]
        [InlineData("Windows.Universal")]
        [InlineData("Unknown")]
        [InlineData("Windows.XboxOne")]
        [InlineData("Windows.NotXbox")]
        [InlineData("Xbox360")]
        [InlineData("MyXboxDevice")]
        public void IsXbox_NonXboxOrSubstringOnlyFamilies_ReturnFalse(string deviceFamily)
        {
            var info = new DeviceInfo(deviceFamily, OsVersion, Architecture, Manufacturer, Model);

            Assert.False(info.IsXbox);
        }
    }
}
