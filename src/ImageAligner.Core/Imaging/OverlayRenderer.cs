using System.Drawing;
using ImageAligner.Core.Models;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ImageAligner.Core.Imaging;

public static class OverlayRenderer
{
    public static Bitmap DrawContour(Bitmap src, Contour c, Color color)
    {
        using var m = BitmapConverter.ToMat(src);
        var pts = c.Points.Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray();
        Cv2.Polylines(m, new[] { pts }, true, new Scalar(color.B, color.G, color.R), 3);

        var center = new OpenCvSharp.Point((int)c.Center.X, (int)c.Center.Y);
        Cv2.DrawMarker(m, center, new Scalar(0, 0, 255), MarkerTypes.Cross, 40, 3);
        return BitmapConverter.ToBitmap(m);
    }
}