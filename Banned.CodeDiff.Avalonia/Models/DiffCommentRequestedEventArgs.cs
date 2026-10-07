namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     显式评论命令的锚点载荷;普通选区完成不引发评论请求。<br />
///     Anchor payload of an explicit comment command; ordinary selection completion does not request a comment.
/// </summary>
public sealed class DiffCommentRequestedEventArgs : EventArgs
{
    internal DiffCommentRequestedEventArgs(DiffCommentAnchor anchor)
    {
        Anchor = anchor;
    }

    /// <summary>获取评论请求的行范围锚点。<br />Gets the line-range anchor of the comment request.</summary>
    public DiffCommentAnchor Anchor { get; }
}
