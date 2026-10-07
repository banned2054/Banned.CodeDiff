namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     内置 Diff/语法主题,可组合或独立选择;缺少浅色变体时回退 GitHub 浅色。<br />
///     Built-in diff/syntax themes, selectable together or independently; missing light variants use GitHub light.
/// </summary>
public enum DiffThemePreset
{
    /// <summary>
    ///     GitHub 当前外观(上游 <c>--diff-*--</c> 变量 + github-light/dark 语法主题),也是
    ///     黄金回归基线;所有槽位未定制时的默认值。<br />
    ///     The current GitHub look (the upstream <c>--diff-*--</c> variables plus the
    ///     github-light/dark syntax themes), also the golden regression baseline; the default
    ///     when no slot is customized.
    /// </summary>
    GitHub = 0,

    /// <summary>
    ///     Codex 深色风格的初版参考配色,待校准。<br />
    ///     Initial Codex-style dark reference colors, pending calibration.
    /// </summary>
    Codex = 1,

    /// <summary>
    ///     Monokai 深色语法主题与派生 Diff 配色;浅色时回退 GitHub。<br />
    ///     Monokai dark syntax with derived diff colors; light variants fall back to GitHub.
    /// </summary>
    Monokai = 2,

    /// <summary>
    ///     Visual Studio 风格(参考 VS Code 内置 Dark+ / Light+ 主题的语法色;Diff 行背景
    ///     派生自 VS Code 的 git diff 行色)。明暗两个变体都有定义。<br />
    ///     Visual Studio style (syntax colors per the VS Code built-in Dark+ / Light+ themes;
    ///     the diff row backgrounds derive from VS Code's git diff line colors). Both light and
    ///     dark variants are defined.
    /// </summary>
    VisualStudio = 3
}
