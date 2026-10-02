using System.Drawing;

namespace ImageAligner.Core.Models;

public sealed class Contour
{
    public IReadOnlyList<PointF> Points { get; init; } = Array.Empty<PointF>();
    public RectangleF Bounds { get; init; }
    public float Area { get; init; }
    public double AngleDeg { get; init; }
    public bool IsCircular { get; init; }

    public PointF Center
    {
        get
        {
            if (Points.Count == 0) return PointF.Empty;
            float sx = 0, sy = 0;
            foreach (var p in Points) { sx += p.X; sy += p.Y; }
            return new PointF(sx / Points.Count, sy / Points.Count);
        }
    }
}