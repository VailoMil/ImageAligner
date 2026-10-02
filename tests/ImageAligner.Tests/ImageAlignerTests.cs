using System.Drawing;
using ImageAligner.Core.Commands;
using ImageAligner.Core.Imaging;
using ImageAligner.Core.Models;

namespace ImageAligner.Tests;

public class ImageAlignerTests
{
    [Fact]
    public async Task Detect_FindsRectangle_OnPlainBackground()
    {
        using var bmp = new Bitmap(600, 450);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(200, 200, 200));
            g.FillRectangle(Brushes.White, 80, 60, 440, 330);
            using var pen = new Pen(Color.Black, 4);
            g.DrawRectangle(pen, 80, 60, 440, 330);
        }

        var aligner = new ImageAligner.Core.Imaging.ImageAligner();
        var result = await aligner.DetectAllAsync(bmp, new AlignOptions
        {
            MinContourAreaRatio = 0.02,
            MaxWorkingSize = 2000
        });

        Assert.NotNull(result);
        Assert.True(result!.Outer.Area > 440 * 330 * 0.3,
            $"Area = {result.Outer.Area}, ожидалось > {440 * 330 * 0.3}");
    }

    [Fact]
    public void Parse_ReadsAllFlags()
    {
        var o = CommandLineOptions.Parse(new[]
        {
            "--input", "a.png", "--output", "b.png",
            "--crop", "15", "--rotate", "3.5", "--mode", "Auto", "--detect-only"
        });
        Assert.Equal("a.png", o.Input);
        Assert.Equal("b.png", o.Output);
        Assert.Equal(15, o.CropPadding);
        Assert.Equal(3.5, o.Rotate);
        Assert.Equal("Auto", o.Mode);
        Assert.True(o.DetectOnly);
    }
}