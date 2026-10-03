using Avalonia.Controls;
using Banned.CodeDiff.Avalonia.Demo.ViewModels;

namespace Banned.CodeDiff.Avalonia.Demo.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
