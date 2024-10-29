using System.Numerics;
using NativeGestures.Handlers;
using NativeGestures.Lib.Device;
using NativeGestures.Lib.Interfaces;
using OpenTabletDriver;
using OpenTabletDriver.Desktop.Interop;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Tablet.Touch;

namespace NativeGestures
{
    [PluginName("Absolute Mode Native Gestures")]
    public class AbsoluteNativeGesturesHandler : NativeGestureHandler, IPositionedPipelineElement<IDeviceReport>
    {
        #region Fields

        private TimeSpan _resetTime;
        private Vector2 _relativeModeHoldResetThreshold;
        private TimeSpan _relativeModeHoldPressureTime;
        private bool _isInitialized;
        private uint _maxTouchCount;

        #endregion

        #region Initialization

        public override void Initialize()
        {
            if (InterfaceDriver is not Driver driver)
                return;

            // fetch the device first
            var device = driver.InputDevices.Where(x => x.Properties.Name == Tablet.Properties.Name).FirstOrDefault();

            // then fetch the output mode
            var _outputMode = device?.OutputMode;

            if (_outputMode == null)
                return;

            _maxTouchCount = MaxTouchCount;

            if (CurrentTouchDevice is LinuxTouchDevice<TouchPoint> touchDevice)
                touchDevice.IsTouchscreen = TouchpadModeEnabled == false;

            CurrentTouchDevice.ScreenScale = new(DesktopInterop.VirtualScreen.Width, DesktopInterop.VirtualScreen.Height);

            CurrentHandler = GetHandler(CurrentTouchDevice, TouchpadModeEnabled);

            if (CurrentHandler == null)
            {
                Log.Write("Absolute Native Gestures", "Couldn't acquire handler (Is your platform supported?)", LogLevel.Error);
                return;
            }
            else if (CurrentHandler.Initialize(_outputMode, _maxTouchCount) == false)
            {
                Log.Write("Absolute Native Gestures", "Failed to initialize the handler", LogLevel.Error);
                return;
            }
            else if (CurrentTouchDevice == null)
            {
                Log.Write("Absolute Native Gestures", "Couldn't acquire virtual touch device (Is your platform supported?)", LogLevel.Error);
                return;
            }
            else if (CurrentTouchDevice.Initialize(_maxTouchCount) == false) // Due to a bug, only 10 touches are supported by the Windows API
            {
                Log.Write("Absolute Native Gestures", "Failed to intialize the virtual touch device", LogLevel.Error);
                return;
            }

            if (CurrentHandler is ITouchpadHandler touchpadHandler)
            {
                touchpadHandler.RelativeModeHoldPressureTime = _relativeModeHoldPressureTime;
                touchpadHandler.RelativeModeHoldResetThreshold = _relativeModeHoldResetThreshold;
                touchpadHandler.ResetTime = _resetTime;
            }

            _isInitialized = true;
        }

        #endregion

        public override void Consume(IDeviceReport report)
        {
            if (_isInitialized && report is ITouchReport touchReport)
                CurrentHandler.Handle(touchReport.Touches);
            else
                OnEmit(report);
        }

        #region Properties

        [BooleanProperty("Touchpad Mode", ""),
         DefaultPropertyValue(true),
         ToolTip("Native Gestures:\n\n" +
                 "When Enabled, the tablet will act as a touchpad.\n" +
                 "WARNING: Experimental on Windows")]
        public bool TouchpadModeEnabled { get; set; }

        [Property("Reset Time"),
         DefaultPropertyValue(100L),
         Unit("ms"),
         ToolTip("Native Gestures:\n\n" +
                 "The time in milliseconds after which the primary pointer will be reset to its initial position. \n" +
                 "This value will only be used if [Touchpad Mode] is enabled. \n" +
                 "The Recommended value is 3 times your tablet's report interval. \n" +
                 "The default value is 100ms.")]
        public long ResetTime
        {
            get => (long)_resetTime.TotalMilliseconds;
            set => _resetTime = TimeSpan.FromMilliseconds(value);
        }

        [Property("Relative Mode Hold Deadzone"),
         DefaultPropertyValue(0.17f),
         Unit("px"),
         ToolTip("Native Gestures:\n\n" +
                 "The deadzone in pixels above which the primary point will be considered moving, and pressure will not be applied. \n" +
                 "This value will only be used if [Touchpad Mode] is enabled. \n" +
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
                 "This value will only be used if [Touchpad Mode] is enabled. \n" +
                 "The default value is 130ms.")]
        public long RelativeModeHoldPressureTime
        {
            get => (long)_relativeModeHoldPressureTime.TotalMilliseconds;
            set => _relativeModeHoldPressureTime = TimeSpan.FromMilliseconds(value);
        }

        #endregion

        #region Static Stuff

        public static IHandler<IOutputMode, TouchPoint> GetHandler(ITouchDevice<TouchPoint> touchDevice, bool isTouchpad = false)
        {
            return DesktopInterop.CurrentPlatform switch
            {
                PluginPlatform.Windows when isTouchpad => new AbsoluteModeTouchpadHandler(touchDevice),
                PluginPlatform.Windows => new TouchscreenHandler(touchDevice),
                PluginPlatform.Linux => new TouchscreenHandler(touchDevice), // This can handle both touchscreen and touchpad on linux
                _ => null
            };
        }

        #endregion
    }
}