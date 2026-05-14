using System;
using Neosmartpen.Net;

namespace tei_penService_ui.Models
{
    /// <summary>
    /// Aggregierte Min/Max-Koordinaten einer Seite in NCode-Zellen (für Format-Schätzung und Stride).
    /// </summary>
    public sealed class PageDotBounds
    {
        public float MinX { get; private set; } = float.MaxValue;
        public float MaxX { get; private set; } = float.MinValue;
        public float MinY { get; private set; } = float.MaxValue;
        public float MaxY { get; private set; } = float.MinValue;

        public bool HasX => MaxX > float.MinValue / 2f;
        public bool HasY => MaxY > float.MinValue / 2f;

        public float WidthSpan => HasX ? Math.Max(0f, MaxX - MinX) : 0f;
        public float HeightSpan => HasY ? Math.Max(0f, MaxY - MinY) : 0f;

        public void Include(Dot dot)
        {
            if (dot.X < MinX) MinX = dot.X;
            if (dot.X > MaxX) MaxX = dot.X;
            if (dot.Y < MinY) MinY = dot.Y;
            if (dot.Y > MaxY) MaxY = dot.Y;
        }
    }
}
