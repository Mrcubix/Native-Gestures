using System;
using System.Drawing;
using System.Numerics;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Tablet.Touch;
using OpenTabletDriver.Plugin.Timing;
using OpenTabletDriver.Tablet;
using OTD.EnhancedOutputMode.Output;

namespace NativeGestures
{
    [PluginName("Absolute Mode Native Gestures")]
    public class AbsoluteNativeGesturesHandler : NativeGestureHandler, IFilter
    {
        #region Fields

        // Output mode specific fields
        private readonly HPETDeltaStopwatch _stopwatch = new(true);
        private AbsoluteOutputMode _outputMode;
        private TimeSpan _deltaTime;
        private Vector2? _lastPos;
        private Vector2 _min, _max;

        // Shared fields
        private Func<ITabletReport, uint, Vector2?> Transpose;
        private TabletReport _stubReport = new();
        private TimeSpan _resetTime;
        private int _currentActiveTouchCount = 0;
        private int _lastActiveTouchCount = 0;
        private uint _maxTouchCount;
        private bool _isInitialized;
        private bool _skipReport;
        

        #endregion

        #region Initialization

        public override void Initialize()
        {
            if (Info.Driver.OutputMode is not EnhancedAbsoluteOutputMode mode)
            {
                Log.Write("Absolute Native Gestures", "Only [Enhanced Absolute Output Mode] is supported when this plugin is active", LogLevel.Error);
                return;
            }

            _maxTouchCount = MaxTouchCount;

            if (CurrentTouchDevice == null)
            {
                Log.Write("Absolute Native Gestures", "Couldn't acquire virtual touch device", LogLevel.Error);
                return;
            }

            // Due to a bug, only 10 touches are supported by the Windows API
            if (!CurrentTouchDevice.Initialize(_maxTouchCount))
            {
                Log.Write("Absolute Native Gestures", "Failed to intialize the virtual touch device", LogLevel.Error);
                return;
            }

            // Absolute mode has its own method for tranposing touch inputs specifically
            Transpose = (report, index) => mode.TransposeTouch(report);
            _outputMode = mode;

            // We need to calculate the min & max to properly clamp the output
            var output = mode.Output;

            var halfDisplayWidth = output?.Width / 2 ?? 0;
            var halfDisplayHeight = output?.Height / 2 ?? 0;

            var minX = output?.Position.X - halfDisplayWidth ?? 0;
            var maxX = output?.Position.X + output?.Width - halfDisplayWidth ?? 0;
            var minY = output?.Position.Y - halfDisplayHeight ?? 0;
            var maxY = output?.Position.Y + output?.Height - halfDisplayHeight ?? 0;

            _min = new Vector2(minX, minY);
            _max = new Vector2(maxX, maxY);

            if (RelativePrimaryPointerEnabled && CurrentTouchDevice.GetCursorLocation() is Point location)
                _primaryPos = new Vector2(location.X, location.Y) + _min;

            _isInitialized = true;
        }

        #endregion

        public Vector2 Filter(Vector2 input) => input;

        public override bool Pass(IDeviceReport report, ref ITabletReport tabletreport)
        {
            if (_isInitialized && report is ITouchReport touchReport)
            {
                // First active touch
                if (RelativePrimaryPointerEnabled && _lastActiveTouchCount == 0 && touchReport.Touches[0] != null)
                    _skipReport = true;

                _currentActiveTouchCount = 0;

                for (int index = touchReport.Touches.Length - 1; index > -1; index--)
                {
                    if (touchReport.Touches[index] == null)
                        continue;

                    var res = TransposeCore(touchReport.Touches[index].TouchID, touchReport.Touches[index].Position);

                    // NOTE: changed the index to the touch ID on transpose & pressure
                    if (res is Vector2 pos)
                    {
                        CurrentTouchDevice.SetPosition(touchReport.Touches[index].TouchID, pos);

                        // Might need to set pressure depending on hold time
                        if (RelativePrimaryPointerEnabled && index == 0 && _currentActiveTouchCount == 0 && _lastActiveTouchCount < 2) 
                            HandlePrimaryPressure();
                        else
                            CurrentTouchDevice.SetPressure(touchReport.Touches[index].TouchID, 1); // this would be set at all time in Full Absolute Mode
                    }
                    //else
                    //    CurrentTouchDevice.SetInactive(touchReport.Touches[i].TouchID);

                    _currentActiveTouchCount++;
                }

                CurrentTouchDevice.CleanupInactives(touchReport.Touches);

                // Reset Primary Pressure
                if (_pressingPrimary && _currentActiveTouchCount != 1)
                    ResetPrimaryPressure();

                // Only update if we had at least one active touch
                if (_lastActiveTouchCount > 0 || _currentActiveTouchCount > 0)
                    CurrentTouchDevice.Flush();

                _lastActiveTouchCount = _currentActiveTouchCount;

                return false;
            }

            return true;
        }

        protected override Vector2? TransposeCore(uint index, Vector2 pos)
        {
            _stubReport.Position = pos;

            var res = Transpose(_stubReport, index);

            if (res is Vector2 resPos && RelativePrimaryPointerEnabled && index == 0 && _currentActiveTouchCount == 0)
                return TransposeToRelative(resPos);

            return res;
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
            if (_deltaTime > _resetTime)
                return null;
            else
            {
                _primaryPos = final;
                return final;
            }
        }

        #region Properties

        [BooleanProperty("Primary Pointer in Relative Mode", ""),
         DefaultPropertyValue(true),
         ToolTip("Native Gestures:\n\n" +
                 "When Enabled, the primary point will act like a relative mode pointer, while in absolute mode.")]
        public bool RelativePrimaryPointerEnabled { get; set; }

        [Property("Reset Time"),
         DefaultPropertyValue(100L),
         Unit("ms"),
         ToolTip("Native Gestures:\n\n" +
                 "The time in milliseconds after which the primary point will be reset to its initial position. \n" +
                 "This value will only be used if [Primary Pointer in Relative Mode] is enabled. \n" +
                 "The Recommended value is 3 times your tablet's report interval. \n" +
                 "The default value is 100ms.")]
        public long ResetTime
        {
            get => (long)_resetTime.TotalMilliseconds;
            set => _resetTime = TimeSpan.FromMilliseconds(value);
        }

        #endregion
    }
}