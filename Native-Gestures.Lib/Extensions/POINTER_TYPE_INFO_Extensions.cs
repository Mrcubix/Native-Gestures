using System.Drawing;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.Input.Pointer;

namespace NativeGestures.Lib.Extensions
{
    internal static class POINTER_TYPE_INFO_Extensions
    {
        public static void SetPosition(this ref POINTER_TYPE_INFO pointer, int x, int y)
        {
            //var touchInfo = pointer.Anonymous.touchInfo;

            pointer.Anonymous.touchInfo.pointerInfo.ptPixelLocation = new Point(x, y);
            pointer.Anonymous.touchInfo.pointerInfo.ptPixelLocationRaw = pointer.Anonymous.touchInfo.pointerInfo.ptPixelLocation;

            pointer.Anonymous.touchInfo.rcContact = RECT.FromXYWH(x, y, 2, 2);
            pointer.Anonymous.touchInfo.rcContactRaw = pointer.Anonymous.touchInfo.rcContact;
        }

        public static void SetPosition(this ref POINTER_TYPE_INFO pointer, Point point)
        {
            pointer.Anonymous.touchInfo.pointerInfo.ptPixelLocation = point;
            pointer.Anonymous.touchInfo.pointerInfo.ptPixelLocationRaw = point;

            pointer.Anonymous.touchInfo.rcContact = RECT.FromXYWH(point.X, point.Y, 2, 2);
            pointer.Anonymous.touchInfo.rcContactRaw = pointer.Anonymous.touchInfo.rcContact;
        }

        public static void SetPressure(this ref POINTER_TYPE_INFO pointer, uint pressure)
        {
            pointer.Anonymous.touchInfo.pressure = pressure;
        }

        public static void SetTarget(this ref POINTER_TYPE_INFO pointer, HWND hwnd)
        {
            pointer.Anonymous.touchInfo.pointerInfo.hwndTarget = hwnd;
        }

        public static void SetPointerFlags(this ref POINTER_TYPE_INFO pointer, POINTER_FLAGS flags)
        {
            pointer.Anonymous.touchInfo.pointerInfo.pointerFlags |= flags;
        }

        public static void UnsetPointerFlags(this ref POINTER_TYPE_INFO pointer, POINTER_FLAGS flags)
        {
            pointer.Anonymous.touchInfo.pointerInfo.pointerFlags &= ~flags;
        }

        public static void ClearPointerFlags(this ref POINTER_TYPE_INFO pointer)
        {
            pointer.Anonymous.touchInfo.pointerInfo.pointerFlags = 0;
        }

        public static void ClearPointerFlags(this ref POINTER_TYPE_INFO pointer, POINTER_FLAGS flags)
        {
            pointer.Anonymous.touchInfo.pointerInfo.pointerFlags = flags;
        }
    }
}