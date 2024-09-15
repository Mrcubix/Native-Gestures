using System;
using System.Numerics;

namespace NativeGestures.Lib.Interfaces
{
    public interface ITouchpadHandler : IHandler
    {
        TimeSpan ResetTime { get; set; }
        Vector2 RelativeModeHoldResetThreshold { get; set; }
        TimeSpan RelativeModeHoldPressureTime { get; set; }
    }

    public interface ITouchpadHandler<TOutput, TInput> : IHandler<TOutput, TInput> {}
}