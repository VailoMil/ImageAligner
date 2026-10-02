using System.Drawing;
using ImageAligner.Core.Models;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ImageAligner.Core.Imaging;

public static class OverlayRenderer
{
    public static Bitmap DrawContours(Bitmap src, DetectionResult result, Color outerColor, Color innerColor)
    {
        using var m = BitmapConverter.ToMat(src);

        // Внешний контур — красный
        if (result.Outer.Points.Count > 0)
        {
            var outerPts = result.Outer.Points
                .Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray();
            Cv2.Polylines(m, new[] { outerPts }, true,
                new Scalar(outerColor.B, outerColor.G, outerColor.R), 3);
        }

        // Внутренние контуры — жёлтые
        foreach (var inner in result.Inner)
        {
            if (inner.Points.Count == 0) continue;
            var innerPts = inner.Points
                .Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray();
            Cv2.Polylines(m, new[] { innerPts }, true,
                new Scalar(innerColor.B, innerColor.G, innerColor.R), 2);
        }

        // Красный крест в центре внешнего контура
        if (result.Outer.Points.Count > 0)
        {
            var center = new OpenCvSharp.Point(
                (int)result.Outer.Center.X,
                (int)result.Outer.Center.Y);
            Cv2.DrawMarker(m, center, new Scalar(0, 0, 255), MarkerTypes.Cross, 40, 3);
        }

        return BitmapConverter.ToBitmap(m);
    }
}