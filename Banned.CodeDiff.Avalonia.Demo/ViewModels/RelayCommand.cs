using System.Windows.Input;

namespace Banned.CodeDiff.Avalonia.Demo.ViewModels;

/// <summary>Minimal parameterless command for the demo.</summary>
public sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
