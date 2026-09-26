using System.Drawing;
using ImageAligner.Core.Commands;
using ImageAligner.Core.Imaging;
using ImageAligner.Core.Models;

var opts = CommandLineOptions.Parse(args);
if (opts.Help || opts.Input is null)
{
    Console.WriteLine(CommandLineOptions.HelpText);
    return;
}

var aligner = new ImageAligner.Core.Imaging.ImageAligner();
aligner.Progress += (_, e) => Console.WriteLine($"[{e.Progress:P0}] {e.Stage}");

using var bmp = new Bitmap(opts.Input);
var detect = new AlignOptions { MaxWorkingSize = 2000 };
var contour = await aligner.DetectContourAsync(bmp, detect);

if (contour is null)
{
    Console.WriteLine("Контур не найден");
    return;
}

if (opts.DetectOnly)
{
    var outPath = opts.Output ?? "contour.png";
    using var overlay = OverlayRenderer.DrawContour(bmp, contour, Color.Lime);
    overlay.Save(outPath);
    Console.WriteLine($"Сохранено: {outPath}");
    return;
}

if (opts.Rotate is double angle)
{
    using var rotated = await aligner.RotateFreeAsync(bmp, angle);
    var outPath = opts.Output ?? "rotated.png";
    rotated.Save(outPath);
    Console.WriteLine($"Сохранено: {outPath}");
    return;
}

var result = await aligner.AlignAsync(bmp, contour, detect);
var final = opts.Output ?? "aligned.png";
result.Cropped.Save(final);
Console.WriteLine($"Сохранено: {final}");