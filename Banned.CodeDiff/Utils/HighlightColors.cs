namespace Banned.CodeDiff.Utils;

// Port of packages/utils/src/color.ts
public static class HighlightColors
{
    public const string AddContentBgName = "--diff-add-content--";

    public const string DelContentBgName = "--diff-del-content--";

    public const string BorderColorName = "--diff-border--";

    public const string AddLineNumberBgName = "--diff-add-lineNumber--";

    public const string DelLineNumberBgName = "--diff-del-lineNumber--";

    public const string PlainContentBgName = "--diff-plain-content--";

    public const string ExpandContentBgName = "--diff-expand-content--";

    public const string PlainLineNumberColorName = "--diff-plain-lineNumber-color--";

    public const string ExpandLineNumberColorName = "--diff-expand-lineNumber-color--";

    public const string PlainLineNumberBgName = "--diff-plain-lineNumber--";

    public const string ExpandLineNumberBgName = "--diff-expand-lineNumber--";

    public const string HunkContentBgName = "--diff-hunk-content--";

    public const string HunkContentColorName = "--diff-hunk-content-color--";

    public const string HunkLineNumberBgName = "--diff-hunk-lineNumber--";

    public const string HunkLineNumberBgHoverName = "--diff-hunk-lineNumber-hover--";

    public const string AddContentHighlightBgName = "--diff-add-content-highlight--";

    public const string DelContentHighlightBgName = "--diff-del-content-highlight--";

    public const string AddWidgetBgName = "--diff-add-widget--";

    public const string AddWidgetColorName = "--diff-add-widget-color--";

    public const string EmptyBgName = "--diff-empty-content--";

    public static string GetContentBg(bool isAdded, bool isDelete, bool hasDiff) => isAdded
        ? $"var({AddContentBgName})"
        : isDelete
            ? $"var({DelContentBgName})"
            : hasDiff
                ? $"var({PlainContentBgName})"
                : $"var({ExpandContentBgName})";


    public static string GetLineNumberBg(bool isAdded, bool isDelete, bool hasDiff) => isAdded
        ? $"var({AddLineNumberBgName})"
        : isDelete
            ? $"var({DelLineNumberBgName})"
            : hasDiff
                ? $"var({PlainLineNumberBgName})"
                : $"var({ExpandLineNumberBgName})";
}