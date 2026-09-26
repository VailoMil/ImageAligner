using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageAligner.Core.Commands;
using ImageAligner.Core.Imaging;
using ImageAligner.Core.Models;
using ImageAligner.Core.Settings;

namespace ImageAligner.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IImageAligner _aligner = new ImageAligner.Core.Imaging.ImageAligner();
    private readonly ISettingsStore _settingsStore = new JsonSettingsStore();
    private Contour? _contour;
    private string? _currentPath;

    public ObservableCollection<string> Log { get; } = new();

    [ObservableProperty] private BitmapImage? _sourceImage;
    [ObservableProperty] private BitmapImage? _contourImage;
    [ObservableProperty] private BitmapImage? _resultImage;
    [ObservableProperty] private string _status = "Готов";
    [ObservableProperty] private AppSettings _settings;

    public MainViewModel()
    {
        _settings = _settingsStore.Load();
        _aligner.Progress += (_, e) => Status = $"{e.Stage}: {e.Progress:P0}";
    }

    [RelayCommand]
    private void Open()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff"
        };
        if (dlg.ShowDialog() != true) return;
        _currentPath = dlg.FileName;
        SourceImage = LoadBitmap(_currentPath);
        Status = $"Открыто: {Path.GetFileName(_currentPath)}";
    }

    [RelayCommand]
    private async Task DetectAsync()
    {
        if (SourceImage is null) return;
        using var bmp = BitmapFromImage(SourceImage);
        _contour = await _aligner.DetectContourAsync(bmp, new AlignOptions
        {
            MaxWorkingSize = Settings.MaxWorkingSize,
            CannyLow = Settings.CannyLow,
            CannyHigh = Settings.CannyHigh,
            MinContourAreaRatio = Settings.MinContourAreaRatio
        });
        if (_contour is null) { Status = "Контур не найден"; return; }
        using var overlay = OverlayRenderer.DrawContour(bmp, _contour, System.Drawing.Color.Lime);
        ContourImage = ToBitmapImage(overlay);
        Status = $"Контур найден (угол {_contour.AngleDeg:F1}°)";
    }

    [RelayCommand]
    private async Task AlignAsync()
    {
        if (SourceImage is null || _contour is null) { Status = "Сначала найдите контур"; return; }
        using var bmp = BitmapFromImage(SourceImage);
        var res = await _aligner.AlignAsync(bmp, _contour, new AlignOptions());
        ResultImage = ToBitmapImage(res.Cropped);
        Status = $"Выровнено на {res.AppliedAngleDeg:F1}°";
    }

    [RelayCommand]
    private async Task CropAsync()
    {
        if (SourceImage is null || _contour is null) return;
        using var bmp = BitmapFromImage(SourceImage);
        using var cropped = await _aligner.CropAsync(bmp, _contour,
            new CropOptions { PaddingPixels = Settings.CropPadding });
        ResultImage = ToBitmapImage(cropped);
        Status = "Обрезано";
    }

    [RelayCommand]
    private async Task RotateAsync()
    {
        if (SourceImage is null) return;

        var input = Microsoft.VisualBasic.Interaction.InputBox(
            "Угол поворота в градусах (можно отрицательные):",
            "Свободное вращение", "0");
        if (string.IsNullOrWhiteSpace(input)) return;
        if (!double.TryParse(input, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var angle))
        {
            Status = "Не удалось разобрать угол";
            return;
        }

        using var bmp = BitmapFromImage(SourceImage);
        using var rotated = await _aligner.RotateFreeAsync(bmp, angle);
        ResultImage = ToBitmapImage(rotated);
        Status = $"Повёрнуто на {angle}°";
    }

    [RelayCommand]
    private void Save()
    {
        if (ResultImage is null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "PNG|*.png" };
        if (dlg.ShowDialog() != true) return;
        using var fs = File.Create(dlg.FileName);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(ResultImage));
        encoder.Save(fs);
        Status = $"Сохранено: {Path.GetFileName(dlg.FileName)}";
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settingsStore.Save(Settings);
        Status = "Настройки сохранены";
    }

    [RelayCommand]
    private void Help()
    {
        System.Windows.MessageBox.Show(CommandLineOptions.HelpText, "Справка",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.UriSource = new Uri(path);
        img.EndInit();
        return img;
    }

    private static System.Drawing.Bitmap BitmapFromImage(BitmapImage src)
    {
        using var ms = new MemoryStream();
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(src));
        enc.Save(ms);
        ms.Position = 0;
        return new System.Drawing.Bitmap(ms);
    }

    private static BitmapImage ToBitmapImage(System.Drawing.Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Position = 0;
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        return img;
    }
}