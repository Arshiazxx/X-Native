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
    /// <remarks>
    /// Thread affinity: this view model is fully synchronous (it starts
    /// no tasks and awaits nothing) and is not thread-safe: it performs
    /// no internal synchronisation and marshals nothing to a UI thread.
    /// <see cref="Load"/> clears and refills <see cref="Modules"/> and
    /// sets <see cref="DeviceInfo"/> and
    /// <see cref="ViewModelBase.IsBusy"/>, raising
    /// <see cref="ObservableObject.PropertyChanged"/> synchronously on
    /// the calling thread. In a UWP/WinUI host, construct the view
    /// model, call <see cref="Load"/>, and bind it from the UI thread
    /// that owns the views. Because nothing is asynchronous, no
    /// continuation can resume on another thread: the calling thread is
    /// the only thread ever involved.
    /// </remarks>
    public sealed class StartupViewModel : ViewModelBase
    {
        // The modules every composition root registers, in display
        // order. Load() reports each of these as loaded or missing, so
        // a shared module that fails to register is visible on the
        // startup screen. Modules a specific head registers on top of
        // this shared set (for example "xnative.xboxhost") are NOT
        // listed here: Load() reports them after the shared modules,
        // in registration order, and only when present — a head's own
        // module must never show as missing in another head.
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
        /// Load state of every registered module, in a stable display
        /// order: the shared modules first, in their expected order
        /// (a shared module that was not registered is reported as
        /// missing), then any additional modules a head registered,
        /// in registration order.
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

                // Modules beyond the shared set belong to the head
                // that registered them (for example the Xbox host
                // module). Report them after the shared modules, in
                // the order they were supplied, always as loaded:
                // they are only ever reported when present, so one
                // head's module can never show as missing in another.
                var reported = new HashSet<string>(ExpectedModuleOrder, StringComparer.OrdinalIgnoreCase);

                foreach (IModuleDescriptor module in _modules)
                {
                    if (reported.Add(module.Id))
                    {
                        Modules.Add(new ModuleStatusViewModel(module.Id, module.DisplayName, isLoaded: true));
                    }
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
