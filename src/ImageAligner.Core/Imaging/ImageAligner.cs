using System.Drawing;
using ImageAligner.Core.Events;
using ImageAligner.Core.Models;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ImageAligner.Core.Imaging;

public sealed class ImageAligner : IImageAligner
{
    public event EventHandler<AlignProgressEventArgs>? Progress;

    public Task<Contour?> DetectContourAsync(Bitmap source, AlignOptions opt, CancellationToken ct = default)
        => Task.Run<Contour?>(() =>
    {
        using var src = BitmapConverter.ToMat(source);
        using var small = ImageUtils.Downscale(src, opt.MaxWorkingSize);

        using var gray = new Mat();
        Cv2.CvtColor(small, gray, ColorConversionCodes.BGR2GRAY);

        int width = gray.Width;
        int height = gray.Height;

        int marginX = Math.Max(5, width  / 10);
        int marginY = Math.Max(5, height / 10);

        var edgePixels = new List<byte>(width * marginY * 2 + height * marginX * 2);

        for (int y = 0; y < marginY; y++)
            for (int x = 0; x < width; x++)
                edgePixels.Add(gray.At<byte>(y, x));

        for (int y = height - marginY; y < height; y++)
            for (int x = 0; x < width; x++)
                edgePixels.Add(gray.At<byte>(y, x));

        for (int y = marginY; y < height - marginY; y++)
        {
            for (int x = 0; x < marginX; x++) edgePixels.Add(gray.At<byte>(y, x));
            for (int x = width - marginX; x < width; x++) edgePixels.Add(gray.At<byte>(y, x));
        }

        edgePixels.Sort();
        byte bgLevel = edgePixels[edgePixels.Count / 2];

        using var bgMat = new Mat(gray.Size(), gray.Type(), new Scalar(bgLevel));
        using var diff = new Mat();
        Cv2.Absdiff(gray, bgMat, diff);

        using var mask = new Mat();
        Cv2.Threshold(diff, mask, 18, 255, ThresholdTypes.Binary);

        using var kernelClose = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(25, 25));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernelClose);

        using var kernelOpen = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(9, 9));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernelOpen);

        Cv2.FindContours(mask, out var contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0) return null;

        var biggest = contours
            .Select(c => new { c, area = Cv2.ContourArea(c) })
            .OrderByDescending(x => x.area)
            .First();

        var imgArea = (double)width * height;
        if (biggest.area < imgArea * opt.MinContourAreaRatio) return null;

        var minRect = Cv2.MinAreaRect(biggest.c);
        var boxPoints = Cv2.BoxPoints(minRect);

        var scale = (float)source.Width / width;

        var pts = boxPoints
            .Select(p => new PointF(p.X * scale, p.Y * scale))
            .ToArray();

        var angle = minRect.Angle;
        if (angle < -45) angle += 90;
        if (angle > 45)  angle -= 90;

        var bounds = Cv2.BoundingRect(boxPoints
            .Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray());

        return new Contour
        {
            Points = pts,
            Bounds = new RectangleF(bounds.X * scale, bounds.Y * scale,
                                    bounds.Width * scale, bounds.Height * scale),
            Area = (float)biggest.area * scale * scale,
            AngleDeg = angle
        };
    }, ct);

    public Task<AlignResult> AlignAsync(Bitmap source, Contour contour, AlignOptions opt, CancellationToken ct = default)
        => Task.Run(() =>
    {
        Progress?.Invoke(this, new AlignProgressEventArgs("Rotate", 0.3));
        using var src = BitmapConverter.ToMat(source);

        var angle = contour.AngleDeg;

        var srcCenter = new Point2f(src.Width / 2f, src.Height / 2f);
        using var rotM = Cv2.GetRotationMatrix2D(srcCenter, -angle, 1.0);

        var contourCenter = contour.Center;
        using var contourCenterMat = new Mat(3, 1, MatType.CV_64F);
        contourCenterMat.Set(0, 0, (double)contourCenter.X);
        contourCenterMat.Set(1, 0, (double)contourCenter.Y);
        contourCenterMat.Set(2, 0, 1.0);

        using var rotMFull = new Mat(3, 3, MatType.CV_64F);
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 3; c++)
                rotMFull.Set(r, c, rotM.At<double>(r, c));
        rotMFull.Set(2, 0, 0.0); rotMFull.Set(2, 1, 0.0); rotMFull.Set(2, 2, 1.0);

        using var rotatedCenter = (rotMFull * contourCenterMat).ToMat();
        var cx = rotatedCenter.At<double>(0, 0);
        var cy = rotatedCenter.At<double>(1, 0);

        var cos = Math.Abs(rotM.At<double>(0, 0));
        var sin = Math.Abs(rotM.At<double>(0, 1));
        int newW = (int)(src.Height * sin + src.Width * cos);
        int newH = (int)(src.Height * cos + src.Width * sin);

        double shiftX = newW / 2.0 - cx;
        double shiftY = newH / 2.0 - cy;

        using var totalM = new Mat(2, 3, MatType.CV_64F);
        totalM.Set(0, 0, rotM.At<double>(0, 0));
        totalM.Set(0, 1, rotM.At<double>(0, 1));
        totalM.Set(0, 2, rotM.At<double>(0, 2) + shiftX);

        totalM.Set(1, 0, rotM.At<double>(1, 0));
        totalM.Set(1, 1, rotM.At<double>(1, 1));
        totalM.Set(1, 2, rotM.At<double>(1, 2) + shiftY);

        Progress?.Invoke(this, new AlignProgressEventArgs("Center", 0.6));

        using var aligned = new Mat();
        Cv2.WarpAffine(src, aligned, totalM, new OpenCvSharp.Size(newW, newH),
            InterpolationFlags.Linear, BorderTypes.Replicate);

        Progress?.Invoke(this, new AlignProgressEventArgs("Crop", 0.85));

        var ptsInAligned = contour.Points
            .Select(p => new Point2f(
                (float)(totalM.At<double>(0, 0) * p.X + totalM.At<double>(0, 1) * p.Y + totalM.At<double>(0, 2)),
                (float)(totalM.At<double>(1, 0) * p.X + totalM.At<double>(1, 1) * p.Y + totalM.At<double>(1, 2))))
            .Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y))
            .ToArray();

        var cropRect = Cv2.BoundingRect(ptsInAligned);
        int pad = 20;
        cropRect.Inflate(-pad, -pad);
        cropRect.Intersect(new Rect(0, 0, aligned.Width, aligned.Height));

        using var cropped = new Mat(aligned, cropRect);

        Progress?.Invoke(this, new AlignProgressEventArgs("Done", 1.0));

        return new AlignResult
        {
            Source = source,
            Aligned = BitmapConverter.ToBitmap(aligned),
            Cropped = BitmapConverter.ToBitmap(cropped),
            Contour = contour,
            AppliedAngleDeg = -angle,
            CropRect = new Rectangle(cropRect.X, cropRect.Y, cropRect.Width, cropRect.Height)
        };
    }, ct);

    public Task<Bitmap> RotateFreeAsync(Bitmap source, double angleDeg, CancellationToken ct = default)
        => Task.Run(() =>
    {
        using var src = BitmapConverter.ToMat(source);
        using var rot = Rotate(src, angleDeg, out _);
        return BitmapConverter.ToBitmap(rot);
    }, ct);

    public Task<Bitmap> CropAsync(Bitmap source, Contour contour, CropOptions options, CancellationToken ct = default)
        => Task.Run(() =>
    {
        using var src = BitmapConverter.ToMat(source);

        var pts = contour.Points
            .Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y))
            .ToArray();

        var rect = Cv2.BoundingRect(pts);
        rect.Inflate(-options.PaddingPixels, -options.PaddingPixels);
        rect.Intersect(new Rect(0, 0, src.Width, src.Height));

        using var cropped = new Mat(src, rect);
        return BitmapConverter.ToBitmap(cropped);
    }, ct);

    private static Mat Rotate(Mat src, double angle, out Mat m)
    {
        var center = new Point2f(src.Width / 2f, src.Height / 2f);
        m = Cv2.GetRotationMatrix2D(center, angle, 1.0);
        var cos = Math.Abs(m.At<double>(0, 0));
        var sin = Math.Abs(m.At<double>(0, 1));
        var w = (int)(src.Height * sin + src.Width * cos);
        var h = (int)(src.Height * cos + src.Width * sin);
        m.Set(0, 2, m.At<double>(0, 2) + (w - src.Width) / 2.0);
        m.Set(1, 2, m.At<double>(1, 2) + (h - src.Height) / 2.0);

        var dst = new Mat();
        Cv2.WarpAffine(src, dst, m, new OpenCvSharp.Size(w, h),
            InterpolationFlags.Linear, BorderTypes.Replicate);
        return dst;
    }
}