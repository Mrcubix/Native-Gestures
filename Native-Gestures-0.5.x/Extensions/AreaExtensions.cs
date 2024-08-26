using System.Numerics;
using OpenTabletDriver.Plugin;

namespace NativeGestures.Extensions
{
    public static class AreaExtensions
    {
        public static Vector2 GetTopLeft(this Area area)
        {
            return new Vector2(area.Position.X - area.Width / 2, area.Position.Y - area.Height / 2);
        }
    }
}