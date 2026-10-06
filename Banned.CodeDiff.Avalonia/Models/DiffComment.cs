namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     一条行内评论的展示数据:锚点加上宿主提供的内容。草稿、提交、回复、删除与持久化
///     都由宿主负责——视图只按锚点呈现评论卡片与持久高亮;评论集合变更后重新赋值
///     <c>DiffView.Comments</c> 即可刷新呈现。<br />
///     The display data of one inline comment: its anchor plus host-provided content. Drafting,
///     submitting, replying, deleting, and persistence are the host's responsibility — the view
///     only renders comment cards and the persistent highlight by anchor; reassign
///     <c>DiffView.Comments</c> after the comment set changes to refresh the presentation.
/// </summary>
/// <param name="Anchor">评论的行范围锚点。<br />The line-range anchor of the comment.</param>
/// <param name="Author">作者名;匿名评论为 <c>null</c>。<br />The author name, or <c>null</c> for anonymous comments.</param>
/// <param name="Content">评论正文。<br />The comment body.</param>
public sealed record DiffComment(DiffCommentAnchor Anchor, string? Author, string Content);
