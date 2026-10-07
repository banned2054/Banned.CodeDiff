using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     评论的文件、侧别与 1 基闭区间锚点;删除用旧侧,新增用新侧,上下文保留所选侧。<br />
///     Comment anchor with file, side and inclusive 1-based range; deletions use the old side, additions the new side, context keeps the selected side.
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
