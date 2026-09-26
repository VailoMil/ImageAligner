namespace ImageAligner.Core.Models;

public sealed class AlignOptions
{
    public int MaxWorkingSize { get; init; } = 2000;
    public double CannyLow { get; init; } = 50;
    public double CannyHigh { get; init; } = 150;
    public double MinContourAreaRatio { get; init; } = 0.05;
}