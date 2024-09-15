using System;
using System.Numerics;
using NativeGestures.Interfaces;
using NativeGestures.Lib.Interfaces;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet.Touch;
using OpenTabletDriver.Tablet;
using OTD.EnhancedOutputMode.Output;

namespace NativeGestures.Handlers
{
    /// <summary>
    ///     Implementation of the Touchscreen handler for both platforms,
    ///     as well as the touchpad on linux.
    /// </summary>
    /// <param name="touchDevice">The virtual Touch Device</param>
    public class TouchscreenHandler(ITouchDevice<TouchPoint> touchDevice) : TouchHandler(touchDevice)
    {
        protected TabletReport _stubReport = new();
        protected int _lastActiveTouchCount = 0;
        protected int _currentActiveTouchCount = 0;
        protected uint _maxTouchCount;

        public override bool Initialize(IOutputMode mode, uint maxTouchCount)
        {
            if (mode is not EnhancedAbsoluteOutputMode enhancedMode)
            {
                Log.Write("Native Gestures", "Only [Enhanced Absolute Output Mode] is supported when this plugin is active", LogLevel.Error);
                return false;
            }

            _maxTouchCount = maxTouchCount;
            InternalTranspose = (report, index) => enhancedMode.TransposeTouch(report);

            return true;
        }

        public override void Handle(TouchPoint[] touches)
        {
            _currentActiveTouchCount = 0;

            int count = (int)Math.Min(_maxTouchCount, touches.Length);

            for (int index = count - 1; index > -1; index--)
            {
                if (touches[index] == null)
                    continue;

                var res = Transpose(touches[index].TouchID, touches[index].Position);

                // NOTE: changed the index to the touch ID on transpose & pressure
                if (res is Vector2 pos)
                {
                    TouchDevice.SetPosition(touches[index].TouchID, pos);
                    TouchDevice.SetPressure(touches[index].TouchID, 1); // this would be set at all time in Full Absolute Mode
                }

                _currentActiveTouchCount++;
            }

            TouchDevice.CleanupInactives(touches);

            // Only update if we had at least one active touch
            if (_lastActiveTouchCount > 0 || _currentActiveTouchCount > 0)
                TouchDevice.Flush();

            _lastActiveTouchCount = _currentActiveTouchCount;
        }

        public override Vector2? Transpose(uint index, Vector2 pos)
        {
            _stubReport.Position = pos;

            return InternalTranspose(_stubReport, index);
        }
    }
}