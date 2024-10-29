using System;
using System.Numerics;
using NativeGestures.Interfaces;
using NativeGestures.Lib.Interfaces;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Tablet.Touch;
using OTD.EnhancedOutputMode.Output;
using OTD.EnhancedOutputMode.Lib.Tablet;

namespace NativeGestures.Handlers
{
    /// <summary>
    ///     Implementation of the Touchscreen handler for both platforms,
    ///     as well as the touchpad on linux.
    /// </summary>
    /// <param name="touchDevice">The virtual Touch Device</param>
    public class TouchscreenHandler(ITouchDevice<TouchPoint> touchDevice) : TouchHandler(touchDevice)
    {
        private Vector2 min, max;
        protected TouchConvertedReport _stubReport = new();
        protected EnhancedAbsoluteOutputMode _outputMode;
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

            _outputMode = enhancedMode;
            var output = _outputMode.Output;

            var halfDisplayWidth = output?.Width / 2 ?? 0;
            var halfDisplayHeight = output?.Height / 2 ?? 0;

            var minX = output?.Position.X - halfDisplayWidth ?? 0;
            var maxX = output?.Position.X + output?.Width - halfDisplayWidth ?? 0;
            var minY = output?.Position.Y - halfDisplayHeight ?? 0;
            var maxY = output?.Position.Y + output?.Height - halfDisplayHeight ?? 0;

            this.min = new Vector2(minX, minY);
            this.max = new Vector2(maxX, maxY);

            _maxTouchCount = maxTouchCount;
            InternalTranspose = (report, index) => Transform(report);

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

        protected Vector2? Transform(ITabletReport report)
        {
            // Apply transformation
            var pos = Vector2.Transform(report.Position, _outputMode.TouchTransformationMatrix);

            // Clipping to display bounds
            var clippedPoint = Vector2.Clamp(pos, this.min, this.max);
            if (_outputMode.AreaLimiting && clippedPoint != pos)
                return null;

            if (_outputMode.AreaClipping)
                pos = clippedPoint;

            return pos;
        }
    }
}