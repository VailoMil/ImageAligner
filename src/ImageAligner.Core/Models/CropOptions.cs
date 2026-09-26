namespace ImageAligner.Core.Models;

public sealed class CropOptions
{
    public int PaddingPixels { get; init; } = 20;
    public bool KeepAspect { get; init; } = true;
}