using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     <see cref="Views.DiffView" /> 在任一视图模式下渲染的行基类型。<br />
///     Base row type rendered by <see cref="Views.DiffView" /> in either view mode.
/// </summary>
public abstract class DiffRow;

/// <summary>
///     Port of the expand-button ternary chain in the upstream hunk-line components
///     (DiffSplitHunkLine/DiffUnifiedHunkLine): the first hunk shows a single Expand Up button, the
///     last one Expand Down, a remaining hidden range shorter than the compose length shows Expand
///     All, and anything else shows the stacked Down+Up pair.
/// </summary>
internal static class DiffHunkExpand
{
    /// <summary>
    ///     Upstream render gate (<c>currentIsShow || currentIsPureHunk</c>): the hunk row
    ///     shows while its hidden range is non-empty, or as a plain header in pure-diff mode where
    ///     splitInfo/unifiedInfo is null.
    /// </summary>
    public static bool IsRendered(HunkLineInfo? info, bool pureDiffRender)
    {
        return info != null ? info.StartHiddenIndex < info.EndHiddenIndex : pureDiffRender;
    }

    public static (bool Up, bool Down, bool All) Buttons(
        bool expandEnabled, bool isFirst, bool isLast, int hiddenCount, int composeLength)
    {
        if (!expandEnabled) return (false, false, false);

        if (isFirst) return (true, false, false);

        if (isLast) return (false, true, false);

        return hiddenCount < composeLength ? (false, false, true) : (true, true, false);
    }
}
