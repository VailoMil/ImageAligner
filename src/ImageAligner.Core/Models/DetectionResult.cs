namespace ImageAligner.Core.Models;

public sealed class DetectionResult
{
    public required Contour Outer { get; init; }
    public IReadOnlyList<Contour> Inner { get; init; } = Array.Empty<Contour>();
    public IReadOnlyList<Contour> All { get; init; } = Array.Empty<Contour>();
}