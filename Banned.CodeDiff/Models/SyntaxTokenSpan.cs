namespace Banned.CodeDiff.Models;

/// <summary>
///     保留原始 token 的区间与 scope 栈,供语法主题重新解析颜色;区间相对包装文本。<br />
///     Retains token ranges and scope stacks for syntax theme resolution; ranges are relative to wrapper text.
/// </summary>
/// <param name="Start">
///     相对 wrapper 文本的起始偏移(0 基)。<br />Zero-based start offset within the wrapper text.
/// </param>
/// <param name="Length">区间长度。<br />Length of the range.</param>
/// <param name="Scopes">从最内到最外的 scope 栈。<br />The scope stack, ordered innermost first.</param>
public sealed record SyntaxTokenSpan(int Start, int Length, IList<string> Scopes);
