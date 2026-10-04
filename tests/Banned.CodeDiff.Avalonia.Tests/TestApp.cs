using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Banned.CodeDiff.Avalonia.Tests;
using NUnit.Framework;

[assembly : AvaloniaTestApplication(typeof(TestAppBuilder))]

// DiffParser.Shared and TemplateOptions are global mutable state; keep tests sequential.
[assembly : Parallelizable(ParallelScope.None)]

namespace Banned.CodeDiff.Avalonia.Tests;

// Headless application that loads the control library theme exactly the way a consumer does:
// via an explicit StyleInclude. This is the regression gate for the M2 incident where the
// ControlTheme was never discovered and DiffView rendered nothing.
public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new TestAppStyles());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new Window();

        base.OnFrameworkInitializationCompleted();
    }
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
