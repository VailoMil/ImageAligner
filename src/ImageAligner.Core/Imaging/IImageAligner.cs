using System.Drawing;
using ImageAligner.Core.Events;
using ImageAligner.Core.Models;

namespace ImageAligner.Core.Imaging;

public interface IImageAligner
{
    event EventHandler<AlignProgressEventArgs>? Progress;

    Task<DetectionResult?> DetectAllAsync(Bitmap source, AlignOptions options, CancellationToken ct = default);
    Task<AlignResult> AlignAsync(Bitmap source, Contour contour, AlignOptions options, CancellationToken ct = default);
    Task<Bitmap> CropAsync(Bitmap source, Contour contour, CropOptions options, CancellationToken ct = default);
    Task<Bitmap> RotateFreeAsync(Bitmap source, double angleDeg, CancellationToken ct = default);
    Task<Bitmap> RemoveBackgroundAsync(Bitmap source, CancellationToken ct = default);
}