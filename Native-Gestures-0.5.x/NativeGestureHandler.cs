using System;
using NativeGestures.Lib.Device;
using NativeGestures.Lib.Interfaces;
using OpenTabletDriver.Desktop.Interop;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Tablet.Touch;
using OpenTabletDriver.Tablet;
using OTD.EnhancedOutputMode.Lib.Interface;

namespace NativeGestures
{
    public abstract class NativeGestureHandler : IGateFilter, IInitialize, IDisposable
    {
        #region Fields

        private ITabletReport _bulletproofStub = new TabletReport();

        #endregion

        #region Events

        public event Action<IDeviceReport> Emit;

        #endregion

        #region Methods

        public abstract void Initialize();

        public void Consume(IDeviceReport report)
        {
            if (Pass(report, ref _bulletproofStub))
                Emit?.Invoke(report);
        }

        public abstract bool Pass(IDeviceReport report, ref ITabletReport tabletreport);

        public void Dispose()
        {
            CurrentTouchDevice.Dispose();
        }

        #endregion

        #region Properties

        public FilterStage FilterStage => FilterStage.PreTranspose;
        
        public ITouchDevice<TouchPoint> CurrentTouchDevice { get; } = TouchDevice;

        public IHandler<IOutputMode, TouchPoint> CurrentHandler { get; protected set; }

        #endregion

        #region Static Stuff

        public static ITouchDevice<TouchPoint> TouchDevice => SystemInterop.CurrentPlatform switch
        {
            PluginPlatform.Windows when Environment.OSVersion.Version.Build >= 17763 => new WindowsTouchDevice<TouchPoint>(),
            PluginPlatform.Linux => new LinuxTouchDevice<TouchPoint>(),
            _ => null
        };

        public static uint MaxTouchCount => SystemInterop.CurrentPlatform switch
        {
            PluginPlatform.Windows => 10,
            PluginPlatform.Linux => 256,
            _ => 0
        };

        #endregion
    }
}