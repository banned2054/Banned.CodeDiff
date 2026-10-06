using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     行范围评论的锚点——评论在 diff 中定位所需的最小信息:文件身份、侧别与起止行号。
///     删除行锚定旧侧,新增行锚定新侧,上下文行保留发起选区时的侧别;行号为 1 基且起止
///     都含端点(与 <see cref="MultiSelectRange" /> 一致)。视图按锚点稳定定位,视图模式
///     切换与 hunk 展开/收起后按行号重新对位。<br />
///     The anchor of a line-range comment — the minimal information a comment needs to locate
///     itself in a diff: file identity, side, and the start/end line numbers. Deleted lines anchor
///     the old side, added lines the new one, context lines keep the side the selection was made
///     on; numbers are 1-based with both ends inclusive (like <see cref="MultiSelectRange" />).
///     The view relocates comments by their stable anchors across view-mode switches and hunk
///     expands/collapses.
/// </summary>
/// <param name="FilePath">
///     文件身份,由宿主提供(如仓库相对路径);未知时为 <c>null</c>。<br />
///     The file identity supplied by the host (e.g. a repo-relative path); <c>null</c> when unknown.
/// </param>
/// <param name="Side">锚点所在侧(旧侧/新侧)。<br />The anchored side (old / new).</param>
/// <param name="StartLineNumber">起始行号(1 基,含)。<br />The start line number (1-based, inclusive).</param>
/// <param name="EndLineNumber">结束行号(1 基,含)。<br />The end line number (1-based, inclusive).</param>
public sealed record DiffCommentAnchor(string? FilePath, SplitSide Side, int StartLineNumber, int EndLineNumber)
{
    /// <summary>
    ///     返回起止行号已归一化(Start ≤ End)的副本。<br />
    ///     Returns a copy with the start/end order normalized (Start ≤ End).
    /// </summary>
    public DiffCommentAnchor Normalize()
    {
        return StartLineNumber <= EndLineNumber
            ? this
            : this with { StartLineNumber = EndLineNumber, EndLineNumber = StartLineNumber };
    }
}
