namespace ImageAligner.Core.Commands;

public sealed class CommandLineOptions
{
    public string? Input { get; set; }
    public string? Output { get; set; }
    public bool DetectOnly { get; set; }
    public double? Rotate { get; set; }
    public int? CropPadding { get; set; }
    public string? Mode { get; set; }
    public bool Help { get; set; }

    public static CommandLineOptions Parse(string[] args)
    {
        var o = new CommandLineOptions();
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--input":  o.Input = args[++i]; break;
                case "--output": o.Output = args[++i]; break;
                case "--detect-only": o.DetectOnly = true; break;
                case "--rotate": o.Rotate = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--crop":   o.CropPadding = int.Parse(args[++i]); break;
                case "--mode":   o.Mode = args[++i]; break;
                case "-h":
                case "--help":   o.Help = true; break;
            }
        }
        return o;
    }

    public static string HelpText => """
        ImageAligner — полу-автоматическое выравнивание изображения.

        Использование:
          ImageAligner.Cli --input <file> [--output <file>] [--detect-only]
                           [--rotate <deg>] [--crop <px>] [--mode <SemiAuto|Auto|Manual>]

        Клавиши в GUI:
          F1      — справка
          Ctrl+O  — открыть изображение
          D       — найти контур
          A       — выровнять
          C       — обрезать по контуру
          R       — свободное вращение
          Ctrl+S  — сохранить результат
        """;
}