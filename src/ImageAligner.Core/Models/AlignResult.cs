using System.Drawing;

namespace ImageAligner.Core.Models;

public sealed class AlignResult
{
    public required Bitmap Source { get; init; }
    public required Bitmap Aligned { get; init; }
    public required Bitmap Cropped { get; init; }
    public required Contour Contour { get; init; }
    public double AppliedAngleDeg { get; init; }
    public Rectangle CropRect { get; init; }
}