namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     宿主提供的评论数据;宿主负责编辑与持久化。集合变更后重新赋值 <c>DiffView.Comments</c> 刷新。<br />
///     Host-provided comment data; editing and persistence belong to the host. Reassign <c>DiffView.Comments</c> after collection changes to refresh.
/// </summary>
/// <param name="Anchor">评论的行范围锚点。<br />The line-range anchor of the comment.</param>
/// <param name="Author">作者名;匿名评论为 <c>null</c>。<br />The author name, or <c>null</c> for anonymous comments.</param>
/// <param name="Content">评论正文。<br />The comment body.</param>
public sealed record DiffComment(DiffCommentAnchor Anchor, string? Author, string Content);
