using System.Windows;
using ImageAligner.Wpf.ViewModels;

namespace ImageAligner.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();

    private void OnAboutClick(object sender, RoutedEventArgs e)
        => MessageBox.Show(
            "ImageAligner — полу-автоматическое выравнивание изображения.\n\n" +
            "Стек: .NET 10, WPF, OpenCvSharp4.\n" +
            "Проект: лабораторная работа.",
            "О программе",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
}