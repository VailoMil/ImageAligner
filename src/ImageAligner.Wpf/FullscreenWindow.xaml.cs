using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ImageAligner.Wpf;

public partial class FullscreenWindow : Window
{
    public FullscreenWindow(BitmapImage image, string title)
    {
        InitializeComponent();
        DataContext = new FullscreenViewModel(image, title, this);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
}

public partial class FullscreenViewModel : ObservableObject
{
    private readonly Window _window;

    [ObservableProperty] private BitmapImage? _image;
    [ObservableProperty] private string _title = string.Empty;

    public FullscreenViewModel(BitmapImage image, string title, Window window)
    {
        _image = image;
        _title = title;
        _window = window;
    }

    [RelayCommand]
    private void Close() => _window.Close();
}