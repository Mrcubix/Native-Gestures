using System;
using System.Numerics;

namespace NativeGestures.Lib.Interfaces
{
    public interface IHandler : IDisposable
    {
        Vector2? Transpose(uint index, Vector2 pos);
    }

    public interface IHandler<TOutput, TInput> : IHandler
    {
        bool Initialize(TOutput mode, uint maxTouchCount);

        ITouchDevice<TInput> TouchDevice { get; }

        void Handle(TInput[] touches);
    }
}