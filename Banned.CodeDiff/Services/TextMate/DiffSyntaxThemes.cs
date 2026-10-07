namespace Banned.CodeDiff.Services.TextMate;

/// <summary>
///     按资源名访问共享只读的内置语法主题匹配器。<br />
///     Accesses shared read-only bundled syntax theme matchers by resource name.
/// </summary>
public static class DiffSyntaxThemes
{
    /// <summary>GitHub 浅色主题资源名。<br />The GitHub light theme resource name.</summary>
    public const string GitHubLight = "github-light";

    /// <summary>GitHub 深色主题资源名。<br />The GitHub dark theme resource name.</summary>
    public const string GitHubDark = "github-dark";

    /// <summary>Monokai 主题资源名(仅深色)。<br />The Monokai theme resource name (dark only).</summary>
    public const string Monokai = "monokai";

    /// <summary>Visual Studio Dark+ 主题资源名。<br />The Visual Studio Dark+ theme resource name.</summary>
    public const string VisualStudioDark = "vs-dark";

    /// <summary>Visual Studio Light+ 主题资源名。<br />The Visual Studio Light+ theme resource name.</summary>
    public const string VisualStudioLight = "vs-light";

    /// <summary>Codex 深色主题资源名(初版占位)。<br />The Codex dark theme resource name (initial placeholder).</summary>
    public const string CodexDark = "codex-dark";

    /// <summary>
    ///     按资源名取内置语法主题的匹配器;名称未注册时返回 <c>null</c>。<br />
    ///     Gets the matcher of a bundled syntax theme by resource name; <c>null</c> when the name
    ///     is not registered.
    /// </summary>
    /// <param name="name">主题资源名(如 <see cref="GitHubLight" />)。The theme resource name (e.g. <see cref="GitHubLight" />).</param>
    public static ScopeThemeMatcher? GetMatcher(string name)
    {
        return TextMateResources.Instance.GetSyntaxTheme(name);
    }
}
