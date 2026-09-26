namespace ImageAligner.Core.Settings;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "#4CAF50";
    public string Background { get; set; } = "#1E1E1E";
    public string Mode { get; set; } = "SemiAuto";
    public int CropPadding { get; set; } = 20;
    public int MaxWorkingSize { get; set; } = 2000;
    public double CannyLow { get; set; } = 50;
    public double CannyHigh { get; set; } = 150;
    public double MinContourAreaRatio { get; set; } = 0.05;
}