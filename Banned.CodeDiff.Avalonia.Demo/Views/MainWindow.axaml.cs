using Avalonia.Controls;
using Banned.CodeDiff.Avalonia.Demo.ViewModels;
using Banned.CodeDiff.Avalonia.Views;

namespace Banned.CodeDiff.Avalonia.Demo.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        // The VM stays view-model-pure; the control event reaches it from the code-behind.
        DiffView.SelectionCompleted += (_, e) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.OnSelectionCompleted(e.Result);
            }
        };
    }
}
