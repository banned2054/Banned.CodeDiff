namespace Banned.CodeDiff.Utils;

// Port of packages/utils/src/color.ts
/// <summary>
///     packages/utils/src/color.ts 的移植:CSS 变量名常量与取值辅助,无状态纯函数。<br />
///     Port of packages/utils/src/color.ts: CSS variable name constants and lookup helpers; stateless pure functions.
/// </summary>
public static class HighlightColors
{
    /// <summary>新增行内容背景的 CSS 变量名。<br />CSS variable name for the added content background.</summary>
    public const string AddContentBgName = "--diff-add-content--";

    /// <summary>删除行内容背景的 CSS 变量名。<br />CSS variable name for the deleted content background.</summary>
    public const string DelContentBgName = "--diff-del-content--";

    /// <summary>边框颜色的 CSS 变量名。<br />CSS variable name for the border color.</summary>
    public const string BorderColorName = "--diff-border--";

    /// <summary>新增行行号背景的 CSS 变量名。<br />CSS variable name for the added line-number background.</summary>
    public const string AddLineNumberBgName = "--diff-add-lineNumber--";

    /// <summary>删除行行号背景的 CSS 变量名。<br />CSS variable name for the deleted line-number background.</summary>
    public const string DelLineNumberBgName = "--diff-del-lineNumber--";

    /// <summary>普通(有变更)行内容背景的 CSS 变量名。<br />CSS variable name for the plain (changed) content background.</summary>
    public const string PlainContentBgName = "--diff-plain-content--";

    /// <summary>展开区内容背景的 CSS 变量名。<br />CSS variable name for the expanded content background.</summary>
    public const string ExpandContentBgName = "--diff-expand-content--";

    /// <summary>普通行行号前景色的 CSS 变量名。<br />CSS variable name for the plain line-number foreground.</summary>
    public const string PlainLineNumberColorName = "--diff-plain-lineNumber-color--";

    /// <summary>展开区行号前景色的 CSS 变量名。<br />CSS variable name for the expanded line-number foreground.</summary>
    public const string ExpandLineNumberColorName = "--diff-expand-lineNumber-color--";

    /// <summary>普通行行号背景的 CSS 变量名。<br />CSS variable name for the plain line-number background.</summary>
    public const string PlainLineNumberBgName = "--diff-plain-lineNumber--";

    /// <summary>展开区行号背景的 CSS 变量名。<br />CSS variable name for the expanded line-number background.</summary>
    public const string ExpandLineNumberBgName = "--diff-expand-lineNumber--";

    /// <summary>hunk 头内容背景的 CSS 变量名。<br />CSS variable name for the hunk content background.</summary>
    public const string HunkContentBgName = "--diff-hunk-content--";

    /// <summary>hunk 头内容前景色的 CSS 变量名。<br />CSS variable name for the hunk content foreground.</summary>
    public const string HunkContentColorName = "--diff-hunk-content-color--";

    /// <summary>hunk 头行号背景的 CSS 变量名。<br />CSS variable name for the hunk line-number background.</summary>
    public const string HunkLineNumberBgName = "--diff-hunk-lineNumber--";

    /// <summary>hunk 头行号悬停背景的 CSS 变量名。<br />CSS variable name for the hunk line-number hover background.</summary>
    public const string HunkLineNumberBgHoverName = "--diff-hunk-lineNumber-hover--";

    /// <summary>新增行内容高亮背景的 CSS 变量名。<br />CSS variable name for the highlighted added content background.</summary>
    public const string AddContentHighlightBgName = "--diff-add-content-highlight--";

    /// <summary>删除行内容高亮背景的 CSS 变量名。<br />CSS variable name for the highlighted deleted content background.</summary>
    public const string DelContentHighlightBgName = "--diff-del-content-highlight--";

    /// <summary>add-widget 背景的 CSS 变量名。<br />CSS variable name for the add-widget background.</summary>
    public const string AddWidgetBgName = "--diff-add-widget--";

    /// <summary>add-widget 前景色的 CSS 变量名。<br />CSS variable name for the add-widget foreground.</summary>
    public const string AddWidgetColorName = "--diff-add-widget-color--";

    /// <summary>空内容背景的 CSS 变量名。<br />CSS variable name for the empty content background.</summary>
    public const string EmptyBgName = "--diff-empty-content--";

    /// <summary>
    ///     返回内容区背景的 CSS var(...) 引用,按新增→删除→普通变更→展开区取值。<br />Returns the CSS var(...) reference for the content
    ///     background, chosen in order: added, deleted, plain change, expanded.
    /// </summary>
    /// <param name="isAdded">是否新增行。Whether the line is added.</param>
    /// <param name="isDelete">是否删除行。Whether the line is deleted.</param>
    /// <param name="hasDiff">是否有变更(否则取展开区变量)。Whether the line has changes (otherwise the expanded variable is used).</param>
    /// <returns><c>var(--diff-...--)</c> 形式的变量引用。The <c>var(--diff-...--)</c> reference.</returns>
    public static string GetContentBg(bool isAdded, bool isDelete, bool hasDiff)
    {
        return isAdded
            ? $"var({AddContentBgName})"
            : isDelete
                ? $"var({DelContentBgName})"
                : hasDiff
                    ? $"var({PlainContentBgName})"
                    : $"var({ExpandContentBgName})";
    }


    /// <summary>
    ///     返回行号背景的 CSS var(...) 引用,按新增→删除→普通变更→展开区取值。<br />Returns the CSS var(...) reference for the line-number
    ///     background, chosen in order: added, deleted, plain change, expanded.
    /// </summary>
    /// <param name="isAdded">是否新增行。Whether the line is added.</param>
    /// <param name="isDelete">是否删除行。Whether the line is deleted.</param>
    /// <param name="hasDiff">是否有变更(否则取展开区变量)。Whether the line has changes (otherwise the expanded variable is used).</param>
    /// <returns><c>var(--diff-...--)</c> 形式的变量引用。The <c>var(--diff-...--)</c> reference.</returns>
    public static string GetLineNumberBg(bool isAdded, bool isDelete, bool hasDiff)
    {
        return isAdded
            ? $"var({AddLineNumberBgName})"
            : isDelete
                ? $"var({DelLineNumberBgName})"
                : hasDiff
                    ? $"var({PlainLineNumberBgName})"
                    : $"var({ExpandLineNumberBgName})";
    }
}
