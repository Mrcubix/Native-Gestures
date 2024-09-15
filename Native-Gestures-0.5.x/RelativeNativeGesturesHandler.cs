using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using NativeGestures.Handlers;
using NativeGestures.Lib.Interfaces;
using OpenTabletDriver.Desktop.Interop;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Tablet.Touch;
using OpenTabletDriver.Plugin.Timing;
using OpenTabletDriver.Tablet;

namespace NativeGestures
{
    [PluginIgnore]
    [PluginName("Relative Mode Native Gestures")]
    public class RelativeNativeGesturesHandler : NativeGestureHandler, IFilter
    {
        #region Fields

        // Relative Mode specific fields
        private HPETDeltaStopwatch[] _stopwatches;
        private IList<IFilter> _preFilters, _postFilters;
        private Vector2?[] _lastTransposedTouches;
        private Matrix3x2 _transformationMatrix;
        private TimeSpan _resetTime;
        private TimeSpan _deltaTime;
        private bool[] _skippedReports;

        // Shared fields
        private Func<ITabletReport, uint, Vector2?> Transpose;
        private TabletReport _stubReport = new();
        private int _lastActiveTouchCount;
        private int _currentActiveTouchCount;
        private bool _isInitialized;
        private uint _maxTouchCount;

        #endregion

        #region Properties


        #endregion

        #region Initialization

        public override void Initialize()
        {
            if (Info.Driver.OutputMode is not RelativeOutputMode mode)
            {
                Log.Write("Relative Native Gestures", "Only [Enhanced Relative Output Mode] is supported when this plugin is active", LogLevel.Error);
                return;
            }

            _maxTouchCount = MaxTouchCount;
            CurrentHandler = GetHandler(CurrentTouchDevice, true);

            // Due to a bug, only 10 touches are supported by the Windows API
            CurrentTouchDevice?.Initialize(_maxTouchCount);

            // Absolute mode has its own method for tranposing touch inputs specifically
            //Transpose = TransposeRelative;

            // We need a lot more data when in relative mode
            _resetTime = mode.ResetTime;
            _transformationMatrix = CalculateRelativeTransformation(mode.Sensitivity, mode.Tablet, mode.Rotation);

            // And of course, plugins are one of those things we need to re-implement the transpose method
            if (Info.Driver.InterpolatorActive)
                _preFilters = mode.Filters.Where(t => t.FilterStage == FilterStage.PreTranspose).ToList();
            else
                _preFilters = mode.Filters.Where(t => t.FilterStage == FilterStage.PreTranspose || t.FilterStage == FilterStage.PreInterpolate).ToList();

            _postFilters = mode.Filters.Where(t => t.FilterStage == FilterStage.PostTranspose).ToList();

            _isInitialized = true;
        }

        #endregion

        public Vector2 Filter(Vector2 input) => input;

        public override bool Pass(IDeviceReport report, ref ITabletReport tabletreport)
        {
            if (_isInitialized && report is ITouchReport touchReport)
            {
                CurrentHandler.Handle(touchReport.Touches);
                return false;
            }

            return true;
        }

        /*public override bool Pass(IDeviceReport report, ref ITabletReport tabletreport)
        {
            if (_isInitialized && report is ITouchReport touchReport)
            {
                // if it's the first report, we need them ready for relative mode
                if (_lastTransposedTouches == null)
                    ResetRelativeMode(touchReport.Touches.Length);

                // First active touch
                if (_lastActiveTouchCount == 0 && touchReport.Touches[0] != null)
                    _skippedReports[0] = false;

                _currentActiveTouchCount = 0;

                for (int index = touchReport.Touches.Length - 1; index > -1; index--)
                {
                    if (touchReport.Touches[index] == null)
                        continue;

                    var res = TransposeCore(touchReport.Touches[index].TouchID, touchReport.Touches[index].Position);

                    if (res is Vector2 pos)
                    {
                        CurrentTouchDevice.SetPosition(touchReport.Touches[index].TouchID, pos);

                        // Might need to set pressure depending on hold time
                        if (index == 0 && _currentActiveTouchCount == 0 && _lastActiveTouchCount < 2) 
                            HandlePrimaryPressure();
                        else
                            CurrentTouchDevice.SetPressure(touchReport.Touches[index].TouchID, 1); // this would be set at all time in Full Absolute Mode
                    }
                    //else
                    //    CurrentTouchDevice.SetInactive(touchReport.Touches[index].TouchID);

                    _currentActiveTouchCount++;
                }

                CurrentTouchDevice.CleanupInactives(touchReport.Touches);

                // Reset Primary Pressure
                if (_pressingPrimary && _currentActiveTouchCount != 1)
                    ResetPrimaryPressure();

                CurrentTouchDevice.Flush();

                _lastActiveTouchCount = _currentActiveTouchCount;

                return false;
            }

            return true;
        }

        public Vector2? TransposeRelative(ITabletReport report, uint index)
        {
            _deltaTime = _stopwatches[index].Restart();

            var pos = report.Position;

            // Pre Filter
            foreach (IFilter filter in _preFilters ??= Array.Empty<IFilter>())
                pos = filter.Filter(pos);

            pos = Vector2.Transform(pos, _transformationMatrix);

            // Post Filter
            foreach (IFilter filter in _postFilters ??= Array.Empty<IFilter>())
                pos = filter.Filter(pos);

            var lastPos = _lastTransposedTouches[index] ?? Vector2.Zero;

            var delta = pos - lastPos;
            var final = lastPos + delta;

            _lastTransposedTouches[index] = pos;

            if (_skippedReports[index] == false)
            {
                _skippedReports[index] = true;
                return null;
            }

            //return (deltaTime > _resetTime) ? null : _lastTransposedTouches[index] + delta;
            if (_deltaTime > _resetTime)
                return null;
            else
            {
                return final;
            }
        }*/

        #region Post Initialization

        private void ResetRelativeMode(int count)
        {
            _lastTransposedTouches = new Vector2?[count];
            _skippedReports = new bool[count];

            _stopwatches = new HPETDeltaStopwatch[count];

            ResetInitialLocation();

            for (int i = 0; i < count; i++)
                _stopwatches[i] = new HPETDeltaStopwatch(true);
        }

        private void ResetInitialLocation()
        {
            if (CurrentTouchDevice.GetCursorLocation() is Point location)
                _lastTransposedTouches[0] = new Vector2(location.X, location.Y);
        }

        #endregion

        #region Static Stuff

        protected static Matrix3x2 CalculateRelativeTransformation(Vector2 sensitivity, TabletState tablet, float rotation)
        {
            var res = Matrix3x2.CreateRotation(
                (float)(-rotation * Math.PI / 180));

            return res *= Matrix3x2.CreateScale(
                sensitivity.X * ((tablet?.Digitizer?.Width / tablet?.Digitizer?.MaxX) ?? 0.01f),
                sensitivity.Y * ((tablet?.Digitizer?.Height / tablet?.Digitizer?.MaxY) ?? 0.01f));
        }

        public static ITouchpadHandler<IOutputMode, TouchPoint> GetHandler(ITouchDevice<TouchPoint> touchDevice, bool isTouchpad = false)
        {
            return SystemInterop.CurrentPlatform switch
            {
                PluginPlatform.Windows => new RelativeModeTouchpadHandler(touchDevice),
                PluginPlatform.Linux => throw new NotImplementedException("Linux is not supported yet"),
                _ => null
            };
        }

        #endregion
    }
}