namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     <see cref="Views.DiffView.CommentRequested" /> 事件的载荷——由宿主显式触发的
///     <see cref="Views.DiffView.BeginCommentCommand" /> 引发,携带从当前选区推导的锚点;
///     普通选区完成本身不会引发该事件。<br />
///     Payload of <see cref="Views.DiffView.CommentRequested" /> — raised by the host-triggered
///     <see cref="Views.DiffView.BeginCommentCommand" /> with the anchor derived from the current
///     selection; completing an ordinary selection never raises it by itself.
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
