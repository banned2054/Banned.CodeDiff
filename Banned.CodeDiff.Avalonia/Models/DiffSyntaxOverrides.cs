namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     scope 到前景色的覆盖规则;颜色支持 <c>#RRGGBB</c> / <c>#RRGGBBAA</c>。<br />
///     Scope-to-foreground override rule; colors support <c>#RRGGBB</c> / <c>#RRGGBBAA</c>.
/// </summary>
public sealed class DiffSyntaxOverride
{
    /// <summary>
    ///     目标 scope 名(单段);与 token scope 栈的任一层按相等匹配。空串匹配全部 token
    ///     (即改写默认前景)。<br />The target scope name (single segment); matches any layer of a token's scope
    ///     stack by equality. An empty string matches every token (rewriting the default
    ///     foreground).
    /// </summary>
    public string Scope { get; set; } = "";

    /// <summary>覆盖前景色(十六进制文本)。<br />The override foreground color (hex text).</summary>
    public string Color { get; set; } = "";
}

/// <summary>
///     宿主语法覆盖;未指定项使用预设,切换预设时保留覆盖。<br />
///     Host syntax overrides; unspecified values use the preset and overrides survive preset changes.
/// </summary>
public sealed class DiffSyntaxOverrides
{
    /// <summary>
    ///     普通文本(无语法色的文本段与未命中任何规则的 token)前景;十六进制文本。
    ///     <c>null</c> 保留预设默认前景。<br />
    ///     The plain-text foreground (uncolored segments and tokens matching no rule); hex text.
    ///     <c>null</c> keeps the preset's default foreground.
    /// </summary>
    public string? DefaultForeground { get; set; }

    /// <summary>scope 覆盖规则;同 scope 的多条规则按列表顺序,后设者赢。<br />The scope override rules; several rules for one scope apply in list order — the last one wins.</summary>
    public List<DiffSyntaxOverride> Overrides { get; set; } = [];
}
