using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Banned.CodeDiff.Avalonia.Tests;

// Compiled-XAML styles that include the control library theme exactly the way a consumer's
// App.axaml does — this is the regression gate for theme discoverability.
public class TestAppStyles : Styles
{
    public TestAppStyles()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
