using NativeGestures.Interfaces;
using NativeGestures.Lib.Device;
using NativeGestures.Lib.Interfaces;
using OpenTabletDriver.Desktop.Interop;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Tablet.Touch;

namespace NativeGestures
{
    public abstract class NativeGestureHandler : IDisposable
    {
        protected TabletReference _tablet;

        #region Events

        public event Action<IDeviceReport> Emit;

        #endregion

        #region Methods

        public abstract void Initialize();

        public abstract void Consume(IDeviceReport report);

        public void OnEmit(IDeviceReport report) => Emit?.Invoke(report);

        public void Dispose()
        {
            CurrentTouchDevice.Dispose();
        }

        #endregion

        #region Properties

        [TabletReference]
        public TabletReference Tablet
        {
            get => _tablet;
            set
            {
                _tablet = value;
                Initialize();
            }
        }

        [Resolved]
        public IDriver InterfaceDriver { get; set; }

        public PipelinePosition Position => PipelinePosition.PreTransform;
        
        public ITouchDevice<TouchPoint> CurrentTouchDevice { get; } = TouchDevice;

        public IHandler<IOutputMode, TouchPoint> CurrentHandler { get; protected set; }

        #endregion

        #region Static Stuff

        public static ITouchDevice<TouchPoint> TouchDevice => DesktopInterop.CurrentPlatform switch
        {
            PluginPlatform.Windows when Environment.OSVersion.Version.Build >= 17763 => new WindowsTouchDevice<TouchPoint>(),
            PluginPlatform.Linux => new LinuxTouchDevice<TouchPoint>(),
            _ => null
        };

        public static uint MaxTouchCount => DesktopInterop.CurrentPlatform switch
        {
            PluginPlatform.Windows => 10,
            PluginPlatform.Linux => 256,
            _ => 0
        };

        #endregion
    }
}