using System.Runtime.InteropServices;

namespace NativeGestures.Lib.Linux.Evdev
{
    [StructLayout(LayoutKind.Sequential)]
    public struct input_absinfo
    {
        public int value;
        public int minimum;
        public int maximum;
        public int fuzz;
        public int flat;
	    public int resolution;
    }
}