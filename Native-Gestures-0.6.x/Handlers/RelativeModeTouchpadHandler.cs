using System;
using System.Drawing;
using System.Numerics;
using NativeGestures.Interfaces;
using NativeGestures.Lib.Interfaces;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Tablet.Touch;
using OpenTabletDriver.Plugin.Timing;
using OTD.EnhancedOutputMode.Output;

namespace NativeGestures.Handlers
{
    // TODO: Complete support for relative mode
    public class RelativeModeTouchpadHandler(ITouchDevice<TouchPoint> touchDevice) : TouchHandler(touchDevice), ITouchpadHandler<IOutputMode, TouchPoint>
    {
        protected readonly HPETDeltaStopwatch _holdStopwatch = new(true);
        private readonly HPETDeltaStopwatch _stopwatch = new(true);
        private Vector2?[] _lastTouchPositions = new Vector2?[10];
        private Vector2[] _touchPositions = new Vector2[10];
        private TimeSpan[] _deltaTimes = new TimeSpan[10];
        private TimeSpan _resetTime = TimeSpan.Zero;
        protected bool _pressingPrimary;
        private bool[] _skipReports = new bool[10];

        protected TabletReport _stubReport = new();
        protected int _lastActiveTouchCount = 0;
        protected int _currentActiveTouchCount = 0;
        protected uint _maxTouchCount;

        #region Properties

        public TimeSpan ResetTime { get; set; }
        public Vector2 RelativeModeHoldResetThreshold { get; set; }
        public TimeSpan RelativeModeHoldPressureTime { get; set; }

        #endregion

        #region Methods

        public override bool Initialize(IOutputMode mode, uint maxTouchCount)
        {
            _maxTouchCount = maxTouchCount;

            InternalTranspose = TransposeToRelative;

            var output = (mode as EnhancedAbsoluteOutputMode)!.Output;

            var halfDisplayWidth = output?.Width / 2 ?? 1;
            var halfDisplayHeight = output?.Height / 2 ?? 1;

            var minX = output?.Position.X - halfDisplayWidth ?? 0;
            var minY = output?.Position.Y - halfDisplayHeight ?? 0;

            var min = new Vector2(minX, minY);

            if (TouchDevice.GetCursorLocation() is Point location)
                _touchPositions[0] = new Vector2(location.X, location.Y) + min;

            return true;
        }

        public override void Handle(TouchPoint[] touches)
        {
            // First active touch
            if (_lastActiveTouchCount == 0 && touches[0] != null)
                Array.Fill(_skipReports, true);

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

                    // Might need to set pressure depending on hold time
                    if (index == 0 && _currentActiveTouchCount == 0 && _lastActiveTouchCount < 2)
                        //HandlePrimaryPressure();
                        return; // TODO: HandlePrimaryPressure()
                    else
                        TouchDevice.SetPressure(touches[index].TouchID, 1); // this would be set at all time in Full Absolute Mode
                }

                _currentActiveTouchCount++;
            }

            TouchDevice.CleanupInactives(touches);

            // Reset Primary Pressure
            //if (_pressingPrimary && _currentActiveTouchCount != 1)
                //ResetPrimaryPressure();

            // Only update if we had at least one active touch
            if (_lastActiveTouchCount > 0 || _currentActiveTouchCount > 0)
                TouchDevice.Flush();

            _lastActiveTouchCount = _currentActiveTouchCount;
        }

        public override Vector2? Transpose(uint index, Vector2 pos)
        {
            _stubReport.Position = pos;

            var res = InternalTranspose(_stubReport, index);

            return res;
        }

        #region Methods Specific to Primary Pointer

        /*private void HandlePrimaryPressure()
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
        }*/

        public Vector2? TransposeToRelative(ITabletReport report, uint index)
        {
            _deltaTimes[index] = _stopwatch.Restart();

            var delta = report.Position - _lastTouchPositions[index];
            var final = _touchPositions[index] + (delta ?? Vector2.Zero);

            _lastTouchPositions[index] = report.Position;

            if (_skipReports[index])
            {
                _skipReports[index] = false;
                return null;
            }

            // provide the absolute position calculated using the delta
            if (_deltaTimes[index] > _resetTime)
                return null;
            else
            {
                _touchPositions[index] = final;
                return final;
            }
        }

        #endregion

        #endregion
    }
}