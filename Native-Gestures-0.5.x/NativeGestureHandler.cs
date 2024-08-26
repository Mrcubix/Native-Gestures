using System;
using System.Numerics;
using NativeGestures.Lib.Device;
using NativeGestures.Lib.Interfaces;
using OpenTabletDriver.Desktop.Interop;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Tablet.Touch;
using OpenTabletDriver.Plugin.Timing;
using OTD.EnhancedOutputMode.Lib.Interface;

namespace NativeGestures
{
    public abstract class NativeGestureHandler : IGateFilter, IInitialize, IDisposable
    {
        #region Fields

        protected readonly HPETDeltaStopwatch _holdStopwatch = new(true);
        protected Vector2 _relativeModeHoldResetThreshold;
        protected TimeSpan _relativeModeHoldPressureTime;
        protected Vector2 _primaryPos = new(-1, -1);
        protected Vector2 _lastPrimaryPos = new();
        protected bool _pressingPrimary;

        #endregion

        #region Methods

        public abstract void Initialize();

        public abstract bool Pass(IDeviceReport report, ref ITabletReport tabletreport);

        protected abstract Vector2? TransposeCore(uint index, Vector2 pos);

        protected virtual void HandlePrimaryPressure()
        {
            // We might only know that the cursor is inactive outside of where this is called
            if (_pressingPrimary)
                return;

            var deltaAbs = Vector2.Abs(_primaryPos - _lastPrimaryPos);

            // Cursor has left the threshold area
            if (deltaAbs.X > _relativeModeHoldResetThreshold.X || deltaAbs.Y > _relativeModeHoldResetThreshold.Y)
                ResetPrimaryPressure();
            else if (_holdStopwatch.Elapsed > _relativeModeHoldPressureTime) // Cursor is still in the threshold area & the hold time has elapsed
            {
                _pressingPrimary = true;
                CurrentTouchDevice.SetPressure(0, 1);
                _holdStopwatch.Stop();
            }
        }

        protected virtual void ResetPrimaryPressure()
        {
            _lastPrimaryPos = _primaryPos;
            _holdStopwatch.Restart();

            _pressingPrimary = false;
            CurrentTouchDevice.SetPressure(0, 0);
        }

        public void Dispose()
        {
            CurrentTouchDevice.Dispose();
        }

        #endregion

        #region Properties

        public FilterStage FilterStage => FilterStage.PreTranspose;
        
        public ITouchDevice<TouchPoint> CurrentTouchDevice { get; } = TouchDevice;

        [Property("Relative Mode Hold Reset Threshold"),
         DefaultPropertyValue(0.17f),
         Unit("px"),
         ToolTip("Native Gestures:\n\n" +
                 "The threshold in pixels above which the primary point will be considered moving, and pressure will not be applied. \n" +
                 "The default value is the very small value of 0.17px.")]
        public float RelativeModeHoldResetThreshold
        {
            get => _relativeModeHoldResetThreshold.X;
            set => _relativeModeHoldResetThreshold = new Vector2(value, value);
        }

        [Property("Relative Mode Hold Reset Time"),
         DefaultPropertyValue(130),
         Unit("ms"),
         ToolTip("Native Gestures:\n\n" +
                 "The time in milliseconds after which pressure will be applied to the primary point. \n" +
                 "The default value is 130ms.")]
        public long RelativeModeHoldPressureTime
        {
            get => (long)_relativeModeHoldPressureTime.TotalMilliseconds;
            set => _relativeModeHoldPressureTime = TimeSpan.FromMilliseconds(value);
        }

        #endregion

        #region Static Stuff

        public static ITouchDevice<TouchPoint> TouchDevice => SystemInterop.CurrentPlatform switch
        {
            PluginPlatform.Windows when Environment.OSVersion.Version.Build >= 17763 => new WindowsTouchDevice<TouchPoint>(),
            PluginPlatform.Linux => new LinuxTouchDevice<TouchPoint>(SystemInterop.VirtualScreen.Width, SystemInterop.VirtualScreen.Height),
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