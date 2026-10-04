using Avalonia.Controls;
using Banned.CodeDiff.Avalonia.Demo.ViewModels;
using Banned.CodeDiff.Avalonia.Views;

namespace Banned.CodeDiff.Avalonia.Demo.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainWindowViewModel();
        DataContext = vm;

        // The VM stays view-model-pure; the control event reaches it from the code-behind.
        DiffView.SelectionCompleted += (_, e) =>
        {
            if (DataContext is MainWindowViewModel current)
            {
                current.OnSelectionCompleted(e.Result);
            }
        };

        // Copy bridge: the VM requests, the code-behind runs the control's copy API and reports
        // back — the host-integration pattern for the copy commands and public methods.
        vm.CopySelectionRequested += async () =>
        {
            if (await DiffView.CopySelectionAsync())
            {
                vm.OnSelectionCopied();
            }
        };
        vm.CopyOldFileRequested += async () =>
        {
            if (await DiffView.CopyOldFileAsync())
            {
                vm.OnOldFileCopied();
            }
        };
        vm.CopyNewFileRequested += async () =>
        {
            if (await DiffView.CopyNewFileAsync())
            {
                vm.OnNewFileCopied();
            }
        };
    }
}
