using System.Drawing;

namespace ImageAligner.Core.Models;

public sealed class Contour
{
    public IReadOnlyList<PointF> Points { get; init; } = Array.Empty<PointF>();
    public RectangleF Bounds { get; init; }
    public float Area { get; init; }
    public double AngleDeg { get; init; }

    public PointF Center => new(Bounds.Left + Bounds.Width / 2f,
                                Bounds.Top  + Bounds.Height / 2f);
}