using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace ImageAligner.Wpf;

public partial class PreviewWindow : Window
{
    private readonly PreviewViewModel _vm;

    public PreviewWindow(BitmapImage image, string title)
    {
        InitializeComponent();
        _vm = new PreviewViewModel(image, title, this);
        DataContext = _vm;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
                _vm.SaveCommand.Execute(null);
        };
    }
}

public partial class PreviewViewModel : ObservableObject
{
    private readonly Window _window;

    [ObservableProperty] private BitmapImage? _image;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _sizeText = string.Empty;

    public PreviewViewModel(BitmapImage image, string title, Window window)
    {
        _image = image;
        _title = title;
        _window = window;
        _sizeText = $"{image.PixelWidth} × {image.PixelHeight}";
    }

    [RelayCommand]
    private void Close() => _window.Close();

    [RelayCommand]
    private void Save()
    {
        if (Image is null) return;

        var dlg = new SaveFileDialog
        {
            Filter = "PNG|*.png|JPEG|*.jpg;*.jpeg|BMP|*.bmp|TIFF|*.tif;*.tiff",
            FileName = "result"
        };
        if (dlg.ShowDialog() != true) return;

        var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
        BitmapEncoder enc = ext switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
            ".bmp" => new BmpBitmapEncoder(),
            ".tif" or ".tiff" => new TiffBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
        enc.Frames.Add(BitmapFrame.Create(Image));

        using var fs = File.Create(dlg.FileName);
        enc.Save(fs);
    }
}