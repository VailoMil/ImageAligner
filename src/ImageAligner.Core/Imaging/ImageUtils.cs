using System.Drawing;
using OpenCvSharp;

namespace ImageAligner.Core.Imaging;

internal static class ImageUtils
{
    public static Mat Downscale(Mat src, int maxSide)
    {
        var max = Math.Max(src.Width, src.Height);
        if (max <= maxSide) return src.Clone();

        var scale = (double)maxSide / max;
        var size = new OpenCvSharp.Size((int)(src.Width * scale), (int)(src.Height * scale));
        var dst = new Mat();
        Cv2.Resize(src, dst, size, interpolation: InterpolationFlags.Area);
        return dst;
    }

    public static PointF Transform(PointF p, Mat m)
    {
        var x = m.At<double>(0, 0) * p.X + m.At<double>(0, 1) * p.Y + m.At<double>(0, 2);
        var y = m.At<double>(1, 0) * p.X + m.At<double>(1, 1) * p.Y + m.At<double>(1, 2);
        return new PointF((float)x, (float)y);
    }
}