using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
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
    private DetectionResult? _detection;
    private string? _currentPath;
    private BitmapImage? _originalImage;

    private readonly MediaPlayer _player = new();
    private readonly Dictionary<string, string> _tracks = new()
    {
        ["Calm"]  = "Assets/music_calm.mp3",
        ["Chase"] = "Assets/music_chase.mp3",
        ["Noir"]  = "Assets/music_noir.mp3"
    };

    public ObservableCollection<string> Log { get; } = new();

    [ObservableProperty] private BitmapImage? _sourceImage;
    [ObservableProperty] private BitmapImage? _contourImage;
    [ObservableProperty] private BitmapImage? _resultImage;
    [ObservableProperty] private string _status = "Готов";
    [ObservableProperty] private AppSettings _settings;
    [ObservableProperty] private int _outerPadding = 0;
    [ObservableProperty] private bool _removeBackgroundAfterAlign = false;
    [ObservableProperty] private bool _autoAlignOnOpen = true;
    [ObservableProperty] private string _saveFormat = "PNG";
    [ObservableProperty] private bool _isMusicEnabled = false;
    [ObservableProperty] private string _currentTrack = "Calm";
    [ObservableProperty] private double _musicVolume = 0.5;

    public MainViewModel()
    {
        _settings = _settingsStore.Load();
        if (string.IsNullOrWhiteSpace(_settings.Mode)) _settings.Mode = "SemiAuto";
        _aligner.Progress += (_, e) => Status = $"{e.Stage}: {e.Progress:P0}";

        _player.Volume = MusicVolume;
        _player.MediaEnded += (_, _) =>
        {
            if (IsMusicEnabled)
            {
                _player.Position = TimeSpan.Zero;
                _player.Play();
            }
        };
    }

    partial void OnMusicVolumeChanged(double value) => _player.Volume = value;

    partial void OnIsMusicEnabledChanged(bool value)
    {
        if (value) PlayCurrent();
        else _player.Stop();
    }

    partial void OnCurrentTrackChanged(string value)
    {
        if (IsMusicEnabled) PlayCurrent();
    }

    private void PlayCurrent()
    {
        if (!_tracks.TryGetValue(CurrentTrack, out var rel)) return;

        var full = Path.Combine(AppContext.BaseDirectory, rel);
        if (!File.Exists(full))
        {
            Status = $"Файл не найден: {rel}";
            return;
        }

        _player.Open(new Uri(full));
        _player.Play();
        Status = $"Играет: {CurrentTrack}";
    }

    [RelayCommand]
    private void SetTrack(string? which)
    {
        if (string.IsNullOrWhiteSpace(which)) return;
        CurrentTrack = which;
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff"
        };
        if (dlg.ShowDialog() != true) return;

        _currentPath = dlg.FileName;
        _originalImage = LoadBitmap(_currentPath);
        SourceImage = _originalImage;
        ContourImage = null;
        ResultImage = null;
        _detection = null;

        var ext = Path.GetExtension(_currentPath).ToLowerInvariant();
        SaveFormat = ext switch
        {
            ".jpg" or ".jpeg" => "JPEG",
            ".bmp" => "BMP",
            ".tif" or ".tiff" => "TIFF",
            _ => "PNG"
        };

        if (!AutoAlignOnOpen)
        {
            Status = $"Открыто: {Path.GetFileName(_currentPath)}";
            return;
        }

        Status = "Анализ изображения…";

        using var bmp = BitmapFromImage(_originalImage);

        _detection = await _aligner.DetectAllAsync(bmp, new AlignOptions
        {
            MaxWorkingSize = Settings.MaxWorkingSize,
            CannyLow = Settings.CannyLow,
            CannyHigh = Settings.CannyHigh,
            MinContourAreaRatio = Settings.MinContourAreaRatio
        });

        if (_detection is null)
        {
            Status = "Контур не найден, показываю исходник";
            return;
        }

        using var overlay = OverlayRenderer.DrawContours(bmp, _detection,
            System.Drawing.Color.Red, System.Drawing.Color.Yellow);
        ContourImage = ToBitmapImage(overlay);

        var res = await _aligner.AlignAsync(bmp, _detection.Outer, new AlignOptions());

        if (RemoveBackgroundAfterAlign)
        {
            using var noBg = await _aligner.RemoveBackgroundAsync(res.Cropped);
            ResultImage = ToBitmapImage(noBg);
            Status = $"Авто-выравнивание: {res.AppliedAngleDeg:F1}° + фон удалён";
        }
        else
        {
            ResultImage = ToBitmapImage(res.Cropped);
            Status = $"Авто-выравнивание: {res.AppliedAngleDeg:F1}°";
        }
    }

    [RelayCommand]
    private void Reset()
    {
        if (_originalImage is null) { Status = "Нет исходного изображения"; return; }
        SourceImage = _originalImage;
        ContourImage = null;
        ResultImage = null;
        _detection = null;
        Status = "Сброшено до оригинала";
    }

    [RelayCommand]
    private void OpenPreview(string? which)
    {
        var img = GetImage(which);
        if (img is null) { Status = "Нечего показывать"; return; }

        var win = new PreviewWindow(img, GetTitle(which))
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        win.ShowDialog();
    }

    [RelayCommand]
    private void OpenFullscreen(string? which)
    {
        var img = GetImage(which);
        if (img is null) { Status = "Нечего показывать"; return; }

        var win = new FullscreenWindow(img, GetTitle(which))
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        win.ShowDialog();
    }

    private BitmapImage? GetImage(string? which) => which switch
    {
        "source" => SourceImage,
        "contour" => ContourImage,
        "result" => ResultImage,
        _ => ResultImage ?? SourceImage
    };

    private static string GetTitle(string? which) => which switch
    {
        "source" => "Исходник",
        "contour" => "Контуры",
        "result" => "Результат",
        _ => "Изображение"
    };

    [RelayCommand]
    private async Task DetectAsync()
    {
        if (SourceImage is null) return;
        using var bmp = BitmapFromImage(SourceImage);
        _detection = await _aligner.DetectAllAsync(bmp, new AlignOptions
        {
            MaxWorkingSize = Settings.MaxWorkingSize,
            CannyLow = Settings.CannyLow,
            CannyHigh = Settings.CannyHigh,
            MinContourAreaRatio = Settings.MinContourAreaRatio
        });

        if (_detection is null) { Status = "Контур не найден"; return; }

        using var overlay = OverlayRenderer.DrawContours(bmp, _detection,
            System.Drawing.Color.Red, System.Drawing.Color.Yellow);
        ContourImage = ToBitmapImage(overlay);
        Status = $"Найдено объектов: {_detection.All.Count}, угол {_detection.Outer.AngleDeg:F1}°";
    }

    [RelayCommand]
    private async Task AlignAsync()
    {
        if (SourceImage is null || _detection is null) { Status = "Сначала найдите контуры"; return; }

        using var bmp = BitmapFromImage(SourceImage);
        var res = await _aligner.AlignAsync(bmp, _detection.Outer, new AlignOptions());

        if (RemoveBackgroundAfterAlign)
        {
            using var noBg = await _aligner.RemoveBackgroundAsync(res.Cropped);
            ResultImage = ToBitmapImage(noBg);
            Status = "Выровнено + фон удалён";
        }
        else
        {
            ResultImage = ToBitmapImage(res.Cropped);
            Status = $"Выровнено на {res.AppliedAngleDeg:F1}°";
        }
    }

    [RelayCommand]
    private async Task CropAsync()
    {
        if (SourceImage is null || _detection is null) return;
        using var bmp = BitmapFromImage(SourceImage);
        using var cropped = await _aligner.CropAsync(bmp, _detection.Outer,
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
    private async Task RemoveBackgroundAsync()
    {
        if (SourceImage is null) return;
        using var bmp = BitmapFromImage(SourceImage);
        using var noBg = await _aligner.RemoveBackgroundAsync(bmp);
        ResultImage = ToBitmapImage(noBg);
        Status = "Фон удалён";
    }

    [RelayCommand]
    private void Save()
    {
        if (ResultImage is null) { Status = "Нет результата для сохранения"; return; }

        var baseName = _currentPath is not null
            ? Path.GetFileNameWithoutExtension(_currentPath)
            : "aligned";

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG|*.png|JPEG|*.jpg;*.jpeg|BMP|*.bmp|TIFF|*.tif;*.tiff",
            FilterIndex = SaveFormat switch
            {
                "JPEG" => 2,
                "BMP" => 3,
                "TIFF" => 4,
                _ => 1
            },
            FileName = baseName + "_out"
        };
        if (dlg.ShowDialog() != true) return;

        var chosenExt = Path.GetExtension(dlg.FileName).ToLowerInvariant();
        BitmapEncoder encoder = chosenExt switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
            ".bmp" => new BmpBitmapEncoder(),
            ".tif" or ".tiff" => new TiffBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
        encoder.Frames.Add(BitmapFrame.Create(ResultImage));

        using var fs = File.Create(dlg.FileName);
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