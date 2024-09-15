using System;
using System.Drawing;
using System.Numerics;
using NativeGestures.Lib.Interfaces;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet.Touch;
using OpenTabletDriver.Plugin.Timing;

namespace NativeGestures.Handlers
{
    public class AbsoluteModeTouchpadHandler(ITouchDevice<TouchPoint> touchDevice) : TouchscreenHandler(touchDevice), ITouchpadHandler
    {
        protected readonly HPETDeltaStopwatch _holdStopwatch = new(true);
        private readonly HPETDeltaStopwatch _stopwatch = new(true);
        private AbsoluteOutputMode _outputMode;
        private Vector2 _lastPrimaryPos = new();
        private Vector2 _primaryPos = new(-1, -1);
        private TimeSpan _deltaTime;
        private Vector2 _min, _max;
        private Vector2? _lastPos;
        protected bool _pressingPrimary;
        private bool _skipReport;

        #region Properties

        public TimeSpan ResetTime { get; set; }
        public Vector2 RelativeModeHoldResetThreshold { get; set; }
        public TimeSpan RelativeModeHoldPressureTime { get; set; }

        #endregion

        #region Methods

        public override bool Initialize(IOutputMode mode, uint maxTouchCount)
        {
            _maxTouchCount = maxTouchCount;

            if (base.Initialize(mode, maxTouchCount) == false)
                return false;

            _outputMode = mode as AbsoluteOutputMode;

            var output = _outputMode!.Output;

            var halfDisplayWidth = output?.Width / 2 ?? 1;
            var halfDisplayHeight = output?.Height / 2 ?? 1;

            var minX = output?.Position.X - halfDisplayWidth ?? 0;
            var maxX = output?.Position.X + output?.Width - halfDisplayWidth ?? 1;
            var minY = output?.Position.Y - halfDisplayHeight ?? 0;
            var maxY = output?.Position.Y + output?.Height - halfDisplayHeight ?? 1;

            _min = new Vector2(minX, minY);
            _max = new Vector2(maxX, maxY);

            if (TouchDevice.GetCursorLocation() is Point location)
                _primaryPos = new Vector2(location.X, location.Y) + _min;

            return true;
        }

        public override void Handle(TouchPoint[] touches)
        {
            // First active touch
            if (_lastActiveTouchCount == 0 && touches[0] != null)
                _skipReport = true;

            _currentActiveTouchCount = 0;

            int count = (int)Math.Min(_maxTouchCount, touches.Length);

            for (int index = count - 1; index > -1; index--)
            {
                if (touches[index] == null) // TODO: pointers should be set inactive here instead
                    continue;

                var res = Transpose(touches[index].TouchID, touches[index].Position);

                // NOTE: changed the index to the touch ID on transpose & pressure
                if (res is Vector2 pos)
                {
                    TouchDevice.SetPosition(touches[index].TouchID, pos);

                    // Might need to set pressure depending on hold time
                    if (index == 0 && _currentActiveTouchCount == 0 && _lastActiveTouchCount < 2)
                        HandlePrimaryPressure();
                    else
                        TouchDevice.SetPressure(touches[index].TouchID, 1); // this would be set at all time in Full Absolute Mode
                }

                _currentActiveTouchCount++;
            }

            TouchDevice.CleanupInactives(touches);

            // Reset Primary Pressure
            if (_pressingPrimary && _currentActiveTouchCount != 1)
                ResetPrimaryPressure();

            // Only update if we had at least one active touch
            if (_lastActiveTouchCount > 0 || _currentActiveTouchCount > 0)
                TouchDevice.Flush();

            _lastActiveTouchCount = _currentActiveTouchCount;
        }

        public override Vector2? Transpose(uint index, Vector2 pos)
        {
            _stubReport.Position = pos;

            var res = InternalTranspose(_stubReport, index);

            if (res is Vector2 resPos && index == 0 && _currentActiveTouchCount == 0)
                return TransposeToRelative(resPos);

            return res;
        }

        #region Methods Specific to Primary Pointer

        private void HandlePrimaryPressure()
        {
            // We might only know that the cursor is inactive outside of where this is called
            if (_pressingPrimary)
                return;

            var deltaAbs = Vector2.Abs(_primaryPos - _lastPrimaryPos);

            // Cursor has left the threshold area
            if (deltaAbs.X > RelativeModeHoldResetThreshold.X || deltaAbs.Y > RelativeModeHoldResetThreshold.Y)
                ResetPrimaryPressure();
            else if (_holdStopwatch.Elapsed > RelativeModeHoldPressureTime) // Cursor is still in the threshold area & the hold time has elapsed
            {
                _pressingPrimary = true;
                TouchDevice.SetPressure(0, 1);
                _holdStopwatch.Stop();
            }
        }

        private void ResetPrimaryPressure()
        {
            _lastPrimaryPos = _primaryPos;
            _holdStopwatch.Restart();

            _pressingPrimary = false;
            TouchDevice.SetPressure(0, 0);
        }

        public Vector2? TransposeToRelative(Vector2 pos)
        {
            _deltaTime = _stopwatch.Restart();

            var delta = pos - _lastPos;
            var final = _primaryPos + (delta ?? Vector2.Zero);

            _lastPos = pos;

            // Clamp the position
            var clippedPoint = Vector2.Clamp(final, _min, _max);

            if (_outputMode.AreaLimiting && clippedPoint != final)
                return null;

            if (_outputMode.AreaClipping)
                final = clippedPoint;

            if (_skipReport)
            {
                _skipReport = false;
                return null;
            }

            // provide the absolute position calculated using the delta
            if (_deltaTime > ResetTime)
                return null;
            else
            {
                _primaryPos = final;
                return final;
            }
        }

        #endregion

        #endregion
    }
}