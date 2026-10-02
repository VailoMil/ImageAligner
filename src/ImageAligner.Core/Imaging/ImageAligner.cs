using System.Drawing;
using ImageAligner.Core.Events;
using ImageAligner.Core.Models;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ImageAligner.Core.Imaging;

public sealed class ImageAligner : IImageAligner
{
    public event EventHandler<AlignProgressEventArgs>? Progress;

    public Task<DetectionResult?> DetectAllAsync(Bitmap source, AlignOptions opt, CancellationToken ct = default)
        => Task.Run<DetectionResult?>(() =>
    {
        using var src = BitmapConverter.ToMat(source);
        using var small = ImageUtils.Downscale(src, opt.MaxWorkingSize);

        int width = small.Width;
        int height = small.Height;

        using var gray = new Mat();
        Cv2.CvtColor(small, gray, ColorConversionCodes.BGR2GRAY);

        // CLAHE — локальный контраст, помогает отличить светлый билет от светлого фона
        using var clahe = Cv2.CreateCLAHE(clipLimit: 4.0, tileGridSize: new OpenCvSharp.Size(8, 8));
        using var enhanced = new Mat();
        clahe.Apply(gray, enhanced);

        // Медиана фона по рамке кадра
        int marginX = Math.Max(5, width / 10);
        int marginY = Math.Max(5, height / 10);

        var edgePixels = new List<byte>(width * marginY * 2 + height * marginX * 2);
        for (int y = 0; y < marginY; y++)
            for (int x = 0; x < width; x++) edgePixels.Add(enhanced.At<byte>(y, x));
        for (int y = height - marginY; y < height; y++)
            for (int x = 0; x < width; x++) edgePixels.Add(enhanced.At<byte>(y, x));
        for (int y = marginY; y < height - marginY; y++)
        {
            for (int x = 0; x < marginX; x++) edgePixels.Add(enhanced.At<byte>(y, x));
            for (int x = width - marginX; x < width; x++) edgePixels.Add(enhanced.At<byte>(y, x));
        }
        edgePixels.Sort();
        byte bgLevel = edgePixels[edgePixels.Count / 2];

        // Маска «не фон»: пиксели, отличающиеся от медианного фона
        using var bgMat = new Mat(enhanced.Size(), enhanced.Type(), new Scalar(bgLevel));
        using var diff = new Mat();
        Cv2.Absdiff(enhanced, bgMat, diff);

        using var mask = new Mat();
        Cv2.Threshold(diff, mask, 20, 255, ThresholdTypes.Binary);

        // Морфология: закрываем дырки, убираем шум
        using var kClose = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(11, 11));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kClose);
        using var kOpen = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(5, 5));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kOpen);

        Cv2.FindContours(mask, out var contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0) return null;

        var imgArea = (double)width * height;

        // Самый большой контур = внешний край билета
        var biggest = contours
            .Select(c => new { c, area = Cv2.ContourArea(c) })
            .OrderByDescending(x => x.area)
            .First();

        if (biggest.area < imgArea * opt.MinContourAreaRatio) return null;

        var minRect = Cv2.MinAreaRect(biggest.c);
        var boxPoints = Cv2.BoxPoints(minRect);

        var scale = (float)source.Width / width;

        var pts = boxPoints
            .Select(p => new PointF(p.X * scale, p.Y * scale))
            .ToArray();

        double angle = 0;
        double bestLen = 0;
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Length];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len = dx * dx + dy * dy;
            if (len > bestLen)
            {
                bestLen = len;
                angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            }
        }
        while (angle >  45) angle -= 90;
        while (angle <= -45) angle += 90;

        var bounds = Cv2.BoundingRect(boxPoints
            .Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray());

        var outer = new Contour
        {
            Points = pts,
            Bounds = new RectangleF(bounds.X * scale, bounds.Y * scale,
                                    bounds.Width * scale, bounds.Height * scale),
            Area = (float)biggest.area * scale * scale,
            AngleDeg = angle,
            IsCircular = false
        };

        return new DetectionResult
        {
            Outer = outer,
            Inner = Array.Empty<Contour>(),
            All = new[] { outer }
        };
    }, ct);

    public Task<AlignResult> AlignAsync(Bitmap source, Contour contour, AlignOptions opt, CancellationToken ct = default)
        => Task.Run(() =>
    {
        Progress?.Invoke(this, new AlignProgressEventArgs("Rotate", 0.3));
        using var src = BitmapConverter.ToMat(source);

        double angle = contour.AngleDeg;

        float sx = 0, sy = 0;
        int count = contour.Points.Count;
        if (count == 0)
        {
            sx = src.Width / 2f;
            sy = src.Height / 2f;
            count = 1;
        }
        else
        {
            foreach (var p in contour.Points) { sx += p.X; sy += p.Y; }
        }
        var ccx = sx / count;
        var ccy = sy / count;

        var rotCenter = new Point2f(ccx, ccy);
        using var rotM = Cv2.GetRotationMatrix2D(rotCenter, angle, 1.0);

        var rotatedCorners = contour.Points
            .Select(p => new Point2f(
                (float)(rotM.At<double>(0, 0) * p.X + rotM.At<double>(0, 1) * p.Y + rotM.At<double>(0, 2)),
                (float)(rotM.At<double>(1, 0) * p.X + rotM.At<double>(1, 1) * p.Y + rotM.At<double>(1, 2))))
            .ToArray();

        float minX = rotatedCorners.Min(p => p.X);
        float maxX = rotatedCorners.Max(p => p.X);
        float minY = rotatedCorners.Min(p => p.Y);
        float maxY = rotatedCorners.Max(p => p.Y);

        float padX = (maxX - minX) * 0.01f;
        float padY = (maxY - minY) * 0.01f;

        int newW = Math.Max(1, (int)Math.Ceiling(maxX - minX + 2 * padX));
        int newH = Math.Max(1, (int)Math.Ceiling(maxY - minY + 2 * padY));

        double dx = -(minX - padX);
        double dy = -(minY - padY);

        rotM.Set(0, 2, rotM.At<double>(0, 2) + dx);
        rotM.Set(1, 2, rotM.At<double>(1, 2) + dy);

        Progress?.Invoke(this, new AlignProgressEventArgs("Center", 0.6));

        using var aligned = new Mat();
        Cv2.WarpAffine(src, aligned, rotM, new OpenCvSharp.Size(newW, newH),
            InterpolationFlags.Cubic, BorderTypes.Replicate);

        Progress?.Invoke(this, new AlignProgressEventArgs("Done", 1.0));

        return new AlignResult
        {
            Source = source,
            Aligned = BitmapConverter.ToBitmap(aligned),
            Cropped = BitmapConverter.ToBitmap(aligned),
            Contour = contour,
            AppliedAngleDeg = angle,
            CropRect = new Rectangle(0, 0, aligned.Width, aligned.Height)
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

        if (contour.Points.Count == 0)
            return BitmapConverter.ToBitmap(src.Clone());

        var pts = contour.Points
            .Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y))
            .ToArray();

        var rect = Cv2.BoundingRect(pts);

        int pad = options.PaddingPixels;
        int x0 = Math.Max(0, rect.X + pad);
        int y0 = Math.Max(0, rect.Y + pad);
        int w0 = Math.Max(1, rect.Width  - 2 * pad);
        int h0 = Math.Max(1, rect.Height - 2 * pad);

        if (x0 + w0 > src.Width)  w0 = src.Width  - x0;
        if (y0 + h0 > src.Height) h0 = src.Height - y0;
        w0 = Math.Max(1, w0);
        h0 = Math.Max(1, h0);

        var cropRect = new Rect(x0, y0, w0, h0);

        using var cropped = new Mat(src, cropRect);
        return BitmapConverter.ToBitmap(cropped);
    }, ct);

    public Task<Bitmap> RemoveBackgroundAsync(Bitmap source, CancellationToken ct = default)
        => Task.Run(() =>
    {
        using var src = BitmapConverter.ToMat(source);
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        int width = gray.Width;
        int height = gray.Height;
        int marginX = Math.Max(5, width / 10);
        int marginY = Math.Max(5, height / 10);

        var edgePixels = new List<byte>(width * marginY * 2 + height * marginX * 2);
        for (int y = 0; y < marginY; y++)
            for (int x = 0; x < width; x++) edgePixels.Add(gray.At<byte>(y, x));
        for (int y = height - marginY; y < height; y++)
            for (int x = 0; x < width; x++) edgePixels.Add(gray.At<byte>(y, x));
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

        Cv2.FindContours(mask, out var contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0) return source;

        var biggest = contours
            .Select(c => new { c, area = Cv2.ContourArea(c) })
            .OrderByDescending(x => x.area)
            .First();

        var hull = Cv2.ConvexHull(biggest.c);

        using var rgba = new Mat(src.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));

        using var maskFull = new Mat(src.Size(), MatType.CV_8UC1, Scalar.Black);
        var hullArr = new[] { hull };
        Cv2.DrawContours(maskFull, hullArr, -1, Scalar.White, -1);

        src.CopyTo(rgba, maskFull);

        var bmp = BitmapConverter.ToBitmap(rgba);
        var outBmp = new Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(outBmp))
        {
            g.DrawImage(bmp, 0, 0);
        }
        return outBmp;
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
            InterpolationFlags.Cubic, BorderTypes.Replicate);
        return dst;
    }
}