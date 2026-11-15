using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using XNative.Core.Abstractions;
using XNative.Core.Devices;
using XNative.UI.Mvvm;

namespace XNative.UI.ViewModels
{
    /// <summary>
    /// View model of the startup screen. It reports which modules the
    /// container resolved and which device the application runs on.
    /// </summary>
    public sealed class StartupViewModel : ViewModelBase
    {
        private static readonly string[] ExpectedModuleOrder =
        {
            "xnative.core",
            "xnative.platform",
            "xnative.graphics",
            "xnative.plugins",
            "xnative.wine",
            "xnative.ui",
        };

        private readonly IEnumerable<IModuleDescriptor> _modules;
        private readonly IDeviceInfoService _deviceInfoService;

        private DeviceInfo _deviceInfo = DeviceInfo.CreateUnknown();

        /// <summary>
        /// Initializes a new instance of the <see cref="StartupViewModel"/> class.
        /// </summary>
        /// <param name="modules">The module descriptors resolved from the container.</param>
        /// <param name="deviceInfoService">The service describing the current device.</param>
        public StartupViewModel(IEnumerable<IModuleDescriptor> modules, IDeviceInfoService deviceInfoService)
        {
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
            _deviceInfoService = deviceInfoService ?? throw new ArgumentNullException(nameof(deviceInfoService));
            Modules = new ObservableCollection<ModuleStatusViewModel>();
        }

        /// <summary>
        /// Title of the application.
        /// </summary>
        public string Title => "X-Native";

        /// <summary>
        /// Load state of every expected module, in a stable display order.
        /// </summary>
        public ObservableCollection<ModuleStatusViewModel> Modules { get; }

        /// <summary>
        /// Description of the current device.
        /// </summary>
        public DeviceInfo DeviceInfo
        {
            get => _deviceInfo;
            private set
            {
                if (SetProperty(ref _deviceInfo, value))
                {
                    OnPropertyChanged(nameof(DeviceInfoText));
                }
            }
        }

        /// <summary>
        /// Device description rendered as the block of text shown on the
        /// startup screen.
        /// </summary>
        public string DeviceInfoText =>
            "Device family: " + DeviceInfo.DeviceFamily + Environment.NewLine +
            "OS version: " + DeviceInfo.OperatingSystemVersion + Environment.NewLine +
            "Architecture: " + DeviceInfo.Architecture + Environment.NewLine +
            "Manufacturer: " + DeviceInfo.Manufacturer + Environment.NewLine +
            "Model: " + DeviceInfo.Model;

        /// <summary>
        /// Populates the view model from the container and the device service.
        /// </summary>
        public void Load()
        {
            IsBusy = true;

            try
            {
                Dictionary<string, IModuleDescriptor> resolved = _modules
                    .GroupBy(module => module.Id, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

                Modules.Clear();

                foreach (string id in ExpectedModuleOrder)
                {
                    bool isLoaded = resolved.TryGetValue(id, out IModuleDescriptor descriptor);
                    string displayName = isLoaded ? descriptor.DisplayName : ToFallbackName(id);
                    Modules.Add(new ModuleStatusViewModel(id, displayName, isLoaded));
                }

                DeviceInfo = _deviceInfoService.GetDeviceInfo();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string ToFallbackName(string id)
        {
            int separator = id.LastIndexOf('.');
            string name = separator >= 0 ? id.Substring(separator + 1) : id;
            return name.Length == 0 ? id : char.ToUpperInvariant(name[0]) + name.Substring(1);
        }
    }
}
