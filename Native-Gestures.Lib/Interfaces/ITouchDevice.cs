using System;
using System.Drawing;
using System.Numerics;

namespace NativeGestures.Lib.Interfaces
{
    public interface ITouchDevice<T> : IDisposable
    {
        Vector2 ScreenScale { get; set; }
        
        uint Count { get; }

        bool Initialize(uint count);

        Point? GetCursorLocation();

        void SetPosition(uint index, Vector2 point);

        void SetPressure(uint index, uint pressure);

        void SetInactive(uint index);

        void CleanupInactives(T[] points);

        void Flush();
    }
}