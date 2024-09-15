using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NativeGestures.Lib.Extensions;
using NativeGestures.Lib.Interfaces;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.Input.Pointer;
using Windows.Win32.UI.WindowsAndMessaging;

#nullable enable

namespace NativeGestures.Lib.Device
{
    public unsafe sealed class WindowsTouchDevice<T> : ITouchDevice<T>
    {
        private HSYNTHETICPOINTERDEVICE _touchHandle;
        private POINTER_TYPE_INFO[]? pointers;
        private readonly HANDLE _sourceDevice = HANDLE.Null;

        private bool[] _lastContact = Array.Empty<bool>();

        public Vector2 ScreenScale { get; set; }
        public uint Count { get; private set; }

        [SupportedOSPlatform("windows10.0.17763")]
        public bool Initialize(uint count)
        {
            Count = count;
            _lastContact = new bool[count];

            // Overrides windows settings unless default is used
            _touchHandle = PInvoke.CreateSyntheticPointerDevice(POINTER_INPUT_TYPE.PT_TOUCH, count, POINTER_FEEDBACK_MODE.POINTER_FEEDBACK_INDIRECT);

            var err = Marshal.GetLastWin32Error();

            if (err < 0 || _touchHandle.IsNull)
                return false;

            BuildPointers();
            SetAllTargets();

            // Notify WindowsInk
            //ClearAllPointerFlags(POINTER_FLAGS.POINTER_FLAG_NEW);

            fixed (POINTER_TYPE_INFO* p = pointers)
                if (!PInvoke.InjectSyntheticPointerInput(_touchHandle, p, (uint)pointers!.Length))
                    return false;

            // Back to normal state
            pointers![0].ClearPointerFlags(POINTER_FLAGS.POINTER_FLAG_PRIMARY);

            return true;
        }

        private void BuildPointers()
        {
            pointers = new POINTER_TYPE_INFO[Count];

            for (uint i = 0; i < Count; i++)
            {
                var info = new POINTER_INFO
                {
                    pointerType = POINTER_INPUT_TYPE.PT_TOUCH,
                    frameId = 0,
                    pointerId = i + 1,
                    pointerFlags = POINTER_FLAGS.POINTER_FLAG_NONE,
                    sourceDevice = _sourceDevice,
                    ptPixelLocation = new Point(0, 0),
                    ptPixelLocationRaw = new Point(0, 0),
                    dwTime = 0,
                    historyCount = 0,
                    dwKeyStates = 0,
                    PerformanceCount = 0,
                    ButtonChangeType = POINTER_BUTTON_CHANGE_TYPE.POINTER_CHANGE_NONE
                };

                var pointer = new POINTER_TOUCH_INFO
                {
                    pointerInfo = info,
                    touchFlags = PInvoke.TOUCH_FLAG_NONE,
                    touchMask = PInvoke.TOUCH_MASK_CONTACTAREA //PInvoke.TOUCH_MASK_PRESSURE // The device i plan to use it on does not provide contact area information
                };

                pointers[i] = new POINTER_TYPE_INFO
                {
                    type = POINTER_INPUT_TYPE.PT_TOUCH,
                    Anonymous = new()
                    {
                        touchInfo = pointer
                    }
                };
            }
        }

        [SupportedOSPlatform("windows5.0")]
        public Point? GetCursorLocation()
        {
            PInvoke.GetCursorPos(out var location);

            // Find which monitor the cursor is on
            var monitor = PInvoke.MonitorFromPoint(location, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);

            if (monitor.Value == IntPtr.Zero)
                return null;

            MONITORINFO info = new()
            {
                cbSize = (uint)Marshal.SizeOf<MONITORINFO>()
            };

            if (PInvoke.GetMonitorInfo(monitor, ref info) == false)
                return null;

            return new Point(
                info.rcWork.X + location.X,
                info.rcWork.Y + location.Y
            );
        }

        [SupportedOSPlatform("windows10.0.17763")]
        public void SetPosition(uint index, Vector2 point)
        {
            if (index >= Count)
                return;

            var intern = new Point((int)point.X, (int)point.Y);

            pointers![index].SetPosition(intern);

            if (_lastContact[index] == false)
            {
                UnsetPointerFlags(index, POINTER_FLAGS.POINTER_FLAG_CANCELED | POINTER_FLAGS.POINTER_FLAG_UPDATE);
                SetPointerFlags(index, POINTER_FLAGS.POINTER_FLAG_INRANGE);
                _lastContact[index] = true;
            }
            else
                SetPointerFlags(index, POINTER_FLAGS.POINTER_FLAG_UPDATE);

            pointers![index].SetTarget(PInvoke.GetForegroundWindow());
        }

        public void SetPressure(uint index, uint pressure)
        {
            //pointers![index].SetPressure(pressure);

            if (pressure > 0)
            {
                // Goes from hovering (POINTER_FLAG_UP) to in contact (POINTER_FLAG_INCONTACT | POINTER_FLAG_DOWN)
                UnsetPointerFlags(index, POINTER_FLAGS.POINTER_FLAG_CANCELED | POINTER_FLAGS.POINTER_FLAG_UP);
                SetPointerFlags(index, POINTER_FLAGS.POINTER_FLAG_INCONTACT | POINTER_FLAGS.POINTER_FLAG_DOWN);
            }
            else
            {
                // Goes from in contact (POINTER_FLAG_INCONTACT | POINTER_FLAG_DOWN) to hovering (POINTER_FLAG_UP)
                UnsetPointerFlags(index, POINTER_FLAGS.POINTER_FLAG_INCONTACT | POINTER_FLAGS.POINTER_FLAG_DOWN);
                SetPointerFlags(index, POINTER_FLAGS.POINTER_FLAG_UP);
            }
        }

        public void SetInactive(uint index)
        {
            // Unset any flags that would represent it being in range
            UnsetPointerFlags(index, POINTER_FLAGS.POINTER_FLAG_DOWN | POINTER_FLAGS.POINTER_FLAG_UP | 
                                     POINTER_FLAGS.POINTER_FLAG_INCONTACT | POINTER_FLAGS.POINTER_FLAG_INRANGE);

            // Apprently, for ending a touch, cancelled need to be provided with either POINTER_FLAG_UP or POINTER_FLAG_UPDATE
            if (_lastContact[index] == false)
                UnsetPointerFlags(index, POINTER_FLAGS.POINTER_FLAG_CANCELED | POINTER_FLAGS.POINTER_FLAG_UPDATE);
            else
                SetPointerFlags(index, POINTER_FLAGS.POINTER_FLAG_CANCELED | POINTER_FLAGS.POINTER_FLAG_UPDATE);

            _lastContact[index] = false;
        }

        public void CleanupInactives(T[] points)
        {
            var count = Math.Min(Count, points.Length);

            for (uint i = 0; i < count; i++)
                if (points[i] == null && _lastContact[i] == true)
                    SetInactive(i);
        }

        [SupportedOSPlatform("windows10.0.17763")]
        public void Flush()
        {
            fixed (POINTER_TYPE_INFO* p = pointers)
                if (!PInvoke.InjectSyntheticPointerInput(_touchHandle, p, (uint)pointers!.Length))
                    //throw new Exception($"Input injection failed. Reason: {Marshal.GetLastWin32Error()}");
                    return;
        }

        [SupportedOSPlatform("windows10.0.17763")]
        private void SetAllTargets()
        {
            for (int i = 0; i < pointers!.Length; i++)
                pointers[i].SetTarget(PInvoke.GetForegroundWindow());
        }

        private void SetPointerFlags(uint index, POINTER_FLAGS flags)
        {
            pointers![index].SetPointerFlags(flags);
        }

        private void SetAllPointerFlags(POINTER_FLAGS flags)
        {
            for (int i = 0; i < pointers!.Length; i++)
                pointers[i].SetPointerFlags(flags);
        }

        private void UnsetPointerFlags(uint index, POINTER_FLAGS flags)
        {
            pointers![index].UnsetPointerFlags(flags);
        }

        private void UnsetAllPointerFlags(POINTER_FLAGS flags)
        {
            for (int i = 0; i < pointers!.Length; i++)
                pointers[i].UnsetPointerFlags(flags);
        }

        private void ClearPointerFlags(uint index)
        {
            pointers![index].ClearPointerFlags();
        }

        private void ClearPointerFlags(uint index, POINTER_FLAGS flags)
        {
            pointers![index].ClearPointerFlags(flags);
        }

        private void ClearAllPointerFlags()
        {
            for (int i = 0; i < pointers!.Length; i++)
                pointers[i].ClearPointerFlags(0);
        }

        private void ClearAllPointerFlags(POINTER_FLAGS flags)
        {
            for (int i = 0; i < pointers!.Length; i++)
                pointers![i].ClearPointerFlags(flags);
        }

        [SupportedOSPlatform("windows10.0.17763")]
        public void Dispose()
        {
            PInvoke.DestroySyntheticPointerDevice(_touchHandle);
        }
    }
}