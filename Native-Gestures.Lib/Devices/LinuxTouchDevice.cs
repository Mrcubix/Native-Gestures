using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Runtime.Versioning;
using NativeGestures.Lib.Interfaces;
using NativeGestures.Lib.Linux;
using NativeGestures.Lib.Linux.Evdev;

#nullable enable

namespace NativeGestures.Lib.Device
{
    public unsafe sealed class LinuxTouchDevice<T>(float width, float height) : ITouchDevice<T>
    {
        private const int MaxPressure = ushort.MaxValue;

        private readonly EvdevDevice _device = new("Native Gestures Virtual Touch Device");
        private bool[] _lastActiveTouches = Array.Empty<bool>();
        private bool[] _activeTouches = Array.Empty<bool>();
        private Vector2 _screenScale = new(width, height);
        private Vector2 _primaryPosition = new();

        private uint _lastCount = 0;
        private uint _currentCount = 0;

        public uint Count { get; private set; }

        [SupportedOSPlatform("linux")]
        public bool Initialize(uint count)
        {
            Count = count;

            _lastActiveTouches = new bool[count];
            _activeTouches = new bool[count];

            BuildPointers();

            //_device.EnableProperty(InputProperty.INPUT_PROP_DIRECT);
            _device.EnableProperty(InputProperty.INPUT_PROP_POINTER);
            _device.EnableType(EventType.EV_ABS);

            EnableCustomCodes();

            _device.EnableTypeCodes(
                EventType.EV_KEY,
                EventCode.BTN_TOUCH,
                EventCode.BTN_TOOL_FINGER,
                EventCode.BTN_TOOL_DOUBLETAP,
                EventCode.BTN_TOOL_TRIPLETAP,
                EventCode.BTN_TOOL_QUADTAP,
                EventCode.BTN_TOOL_QUINTTAP
            );

            var result = _device.Initialize();

            switch (result)
            {
                case ERRNO.NONE:
                    Console.WriteLine($"Evdev: Successfully initialized virtual multitouch device. (code {result})");
                    return true;
                default:
                    Console.WriteLine($"Evdev: Failed to initialize virtual multitouch device. (error code {result})");
                    return false;
            }
        }

        private void EnableCustomCodes()
        {
            // multi-touch Slot
            var slot = new input_absinfo
            {
                maximum = 10
            };

            input_absinfo* slotPtr = &slot;
            _device.EnableCustomCode(EventType.EV_ABS, EventCode.ABS_MT_SLOT, (IntPtr)slotPtr);

            // multi-touch Tracking ID
            var trackingId = new input_absinfo
            {
                maximum = 10,
            };

            input_absinfo* trackingIdPtr = &trackingId;
            _device.EnableCustomCode(EventType.EV_ABS, EventCode.ABS_MT_TRACKING_ID, (IntPtr)trackingIdPtr);

            // X Multi-touch
            var xAbsMulti = new input_absinfo
            {
                maximum = (int)_screenScale.X,
            };

            input_absinfo* xMultiPtr = &xAbsMulti;
            _device.EnableCustomCode(EventType.EV_ABS, EventCode.ABS_MT_POSITION_X, (IntPtr)xMultiPtr);

            // Y Multi-touch
            var yAbsMulti = new input_absinfo
            {
                maximum = (int)_screenScale.Y,
            };

            input_absinfo* yMultiPtr = &yAbsMulti;
            _device.EnableCustomCode(EventType.EV_ABS, EventCode.ABS_MT_POSITION_Y, (IntPtr)yMultiPtr);

            // Major Contact Size
            var major = new input_absinfo
            {
                maximum = 1
            };

            input_absinfo* majorPtr = &major;
            _device.EnableCustomCode(EventType.EV_ABS, EventCode.ABS_MT_TOUCH_MAJOR, (IntPtr)majorPtr);

            // X Single
            var xAbs = new input_absinfo
            {
                maximum = (int)_screenScale.X,
            };

            input_absinfo* xPtr = &xAbs;
            _device.EnableCustomCode(EventType.EV_ABS, EventCode.ABS_X, (IntPtr)xPtr);

            // Y Single
            var yAbs = new input_absinfo
            {
                maximum = (int)_screenScale.Y,
            };

            input_absinfo* yPtr = &yAbs;
            _device.EnableCustomCode(EventType.EV_ABS, EventCode.ABS_Y, (IntPtr)yPtr);
        }

        private void BuildPointers()
        {

        }

        [SupportedOSPlatform("linux")]
        public Point? GetCursorLocation()
        {
            return null;
        }

        [SupportedOSPlatform("linux")]
        public void SetPosition(uint index, Vector2 point)
        {
            if (index == 0)
                _primaryPosition = point;

            _device.Write(EventType.EV_ABS, EventCode.ABS_MT_SLOT, (int)index);
            _device.Write(EventType.EV_ABS, EventCode.ABS_MT_TRACKING_ID, (int)index);
            _device.Write(EventType.EV_ABS, EventCode.ABS_MT_POSITION_X, (int)point.X);
            _device.Write(EventType.EV_ABS, EventCode.ABS_MT_POSITION_Y, (int)point.Y);
            _device.Write(EventType.EV_ABS, EventCode.ABS_MT_TOUCH_MAJOR, 1);
            //_device.Write(EventType.EV_SYN, EventCode.SYN_MT_REPORT, 0);

            _activeTouches[index] = true;
            _currentCount++;
        }

        public void SetPressure(uint index, uint pressure)
        {
            //_device.Write(EventType.EV_SYN, EventCode.SYN_REPORT, 0);
            //_device.Write(EventType.EV_ABS, EventCode.ABS_MT_PRESSURE, (int)(MaxPressure * pressure));
            //_device.Write(EventType.EV_KEY, EventCode.BTN_TOUCH, pressure > 0 ? 1 : 0);
            //_device.Write(EventType.EV_KEY, EventCode.BTN_TOOL_PEN, 1);
        }

        public void SetInactive(uint index)
        {
            _device.Write(EventType.EV_ABS, EventCode.ABS_MT_SLOT, (int)index);
            _device.Write(EventType.EV_ABS, EventCode.ABS_MT_TRACKING_ID, -1);
        }

        public void CleanupInactives(T[] points)
        {
            for (uint index = 0; index < Count; index++)
            {
                if (_lastActiveTouches[index] == true && _activeTouches[index] == false)
                {
                    _device.Write(EventType.EV_ABS, EventCode.ABS_MT_SLOT, (int)index);
                    _device.Write(EventType.EV_ABS, EventCode.ABS_MT_TRACKING_ID, -1);
                }
            }
        }

        [SupportedOSPlatform("linux")]
        public void Flush()
        {
            WriteMultitouchEvent();
            WritePrimary();
            
            _device.Sync();

            _lastCount = _currentCount;
            _lastActiveTouches = (bool[])_activeTouches.Clone();
            
            _currentCount = 0;
            Array.Fill(_activeTouches, false);
        }

        private void WriteMultitouchEvent()
        {
            _device.Write(EventType.EV_KEY, EventCode.BTN_TOUCH, _currentCount > 0 ? 1 : 0);

            if (_currentCount > 0 || _lastCount > 0)
            {
                // We need to write different Event codes & values depending on a specific number of fingers being released of not
                _device.Write(EventType.EV_KEY, EventCode.BTN_TOOL_FINGER, _currentCount == 1 ? 1 : 0);
                _device.Write(EventType.EV_KEY, EventCode.BTN_TOOL_DOUBLETAP, _currentCount == 2 ? 1 : 0);
                _device.Write(EventType.EV_KEY, EventCode.BTN_TOOL_TRIPLETAP, _currentCount == 3 ? 1 : 0);
                _device.Write(EventType.EV_KEY, EventCode.BTN_TOOL_QUADTAP, _currentCount == 4 ? 1 : 0);
                _device.Write(EventType.EV_KEY, EventCode.BTN_TOOL_QUINTTAP, _currentCount == 5 ? 1 : 0);
            }
        }

        private void WritePrimary()
        {
            _device.Write(EventType.EV_ABS, EventCode.ABS_X, (int)_primaryPosition.X);
            _device.Write(EventType.EV_ABS, EventCode.ABS_Y, (int)_primaryPosition.Y);
        }

        public void Dispose()
        {
            _device.Dispose();
        }
    }
}