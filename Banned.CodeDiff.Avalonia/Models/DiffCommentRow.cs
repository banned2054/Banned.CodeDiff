using Avalonia.Media;
using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     同锚点评论的卡片行,位于范围内最后一个可见行之后。<br />
///     Card row grouping comments with the same anchor, placed after its last visible line.
/// </summary>
public abstract class DiffCommentRow : DiffRow
{
    internal DiffCommentRow(DiffCommentAnchor anchor, IReadOnlyList<DiffComment> comments, DiffBrushSet brushes)
    {
        Anchor         = anchor;
        Comments       = comments;
        CardBackground = brushes.CommentCardBackground;
        CardBorder     = brushes.CommentCardBorder;
    }

    /// <summary>获取评论锚点。<br />Gets the comment anchor.</summary>
    public DiffCommentAnchor Anchor { get; }

    /// <summary>获取该锚点上的评论(按宿主提供的顺序)。<br />Gets the comments on the anchor (in host-provided order).</summary>
    public IReadOnlyList<DiffComment> Comments { get; }

    /// <summary>获取评论卡片背景。<br />Gets the comment card background.</summary>
    public IBrush CardBackground { get; }

    /// <summary>获取评论卡片边框。<br />Gets the comment card border.</summary>
    public IBrush CardBorder { get; }
}

/// <summary>
///     分栏视图的评论卡片行:卡片位于锚点侧的内容列之下,另一侧留空。<br />
///     A split comment card row: the card sits under the anchored side's content column and the
///     other side stays empty.
/// </summary>
public sealed class DiffSplitCommentRow : DiffCommentRow
{
    internal DiffSplitCommentRow(DiffCommentAnchor anchor, IReadOnlyList<DiffComment> comments, SplitSide side,
                                 DiffBrushSet brushes) : base(anchor, comments, brushes)
    {
        // 0-based template columns: 1 = old content, 4 = new content.
        CardColumn = side == SplitSide.Old ? 1 : 4;
    }

    /// <summary>
    ///     获取承载卡片的内容列(0 基,供主题模板绑定),非公开 API。<br />
    ///     Gets the 0-based content column hosting the card (bound by the theme template); not a
    ///     public API.
    /// </summary>
    internal int CardColumn { get; }
}

/// <summary>
///     统一视图的评论卡片行:卡片横跨内容列,行号列留空。<br />
///     A unified comment card row: the card spans the content column and the number columns stay
///     empty.
/// </summary>
public sealed class DiffUnifiedCommentRow : DiffCommentRow
{
    internal DiffUnifiedCommentRow(DiffCommentAnchor anchor, IReadOnlyList<DiffComment> comments,
                                   DiffBrushSet brushes) : base(anchor, comments, brushes) { }
}
