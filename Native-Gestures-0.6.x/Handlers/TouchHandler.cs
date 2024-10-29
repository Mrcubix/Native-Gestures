using System;
using System.Numerics;
using NativeGestures.Lib.Device;
using NativeGestures.Lib.Interfaces;
using OpenTabletDriver.Desktop.Interop;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Tablet.Touch;

namespace NativeGestures.Interfaces
{
    public abstract class TouchHandler(ITouchDevice<TouchPoint> touchDevice) : IHandler<IOutputMode, TouchPoint>
    {
        protected Func<ITabletReport, uint, Vector2?> InternalTranspose { get; set; }

        public ITouchDevice<TouchPoint> TouchDevice { get; } = touchDevice;

        public abstract bool Initialize(IOutputMode mode, uint maxTouchCount);

        public abstract void Handle(TouchPoint[] touches);

        public abstract Vector2? Transpose(uint index, Vector2 pos);

        public virtual void Dispose()
        {
            TouchDevice?.Dispose();
            GC.SuppressFinalize(this);
        }

        public static ITouchDevice<TouchPoint> GetTouchDevice(bool isTouchscreen = true)
        {
            return DesktopInterop.CurrentPlatform switch
            {
                PluginPlatform.Windows when Environment.OSVersion.Version.Build >= 17763 => new WindowsTouchDevice<TouchPoint>(),
                PluginPlatform.Linux => new LinuxTouchDevice<TouchPoint>()
                {
                    IsTouchscreen = isTouchscreen
                },
                _ => null
            };
        }

        public static uint MaxTouchCount => DesktopInterop.CurrentPlatform switch
        {
            PluginPlatform.Windows => 10,
            PluginPlatform.Linux => 256,
            _ => 0
        };
    }
}