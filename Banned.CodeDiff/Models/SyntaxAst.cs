namespace Banned.CodeDiff.Models;

// Port of packages/utils/src/highlightAST.ts (types only; processAST belongs to
// the lowlight/highlighter milestone, not the core/parse port).

/// <summary>
///     JS 类型 SyntaxNode.properties(className + 自由扩展字段)的移植,模板构建器只消费
///     className 与 style。<br />
///     JS type: SyntaxNode.properties (className + free-form extras).
///     Only className and style are consumed by the template builders.
/// </summary>
public sealed class SyntaxNodeProperties
{
    /// <summary>
    ///     CSS 类名列表(hast properties.className);可为 <c>null</c>。<br />CSS class list (hast properties.className); may be
    ///     <c>null</c>.
    /// </summary>
    public List<string>? ClassName { get; set; }

    /// <summary>内联样式(hast properties.style);可为 <c>null</c>。<br />Inline style (hast properties.style); may be <c>null</c>.</summary>
    public string? Style { get; set; }

    /// <summary>
    ///     原始 token 与 scope 栈,区间相对包装文本;未提供时使用 <see cref="Style" /> 颜色。<br />
    ///     Original tokens and scope stacks, with ranges relative to wrapper text; absent tokens fall back to <see cref="Style" /> colors.
    /// </summary>
    public List<SyntaxTokenSpan>? Tokens { get; set; }
}

/// <summary>
///     highlightAST.ts 类型 SyntaxNode 的移植:扩展的 hast 节点(类型 / 文本 / 行号 / 索引)。<br />Port of highlightAST.ts type
///     SyntaxNode: an extended hast node (type / value / line number / indexes).
/// </summary>
public sealed class SyntaxNode
{
    /// <summary>节点类型:"text" / "element" / "root"。<br />The node type: "text" / "element" / "root".</summary>
    public string Type { get; set; } = "";

    /// <summary>节点文本内容(仅 text 节点有意义)。<br />The node's text content (meaningful for text nodes).</summary>
    public string Value { get; set; } = "";

    /// <summary>节点所属行号(1 基,processAST 填充)。<br />Line number the node belongs to (1-based, filled by processAST).</summary>
    public int LineNumber { get; set; }

    /// <summary>节点文本在所在行内的起始索引(0 基)。<br />Start index of the node text within its line (0-based).</summary>
    public int StartIndex { get; set; }

    /// <summary>节点文本在所在行内的结束索引(含)。<br />End index of the node text within its line (inclusive).</summary>
    public int EndIndex { get; set; }

    /// <summary>节点属性(类名 / 样式等)。<br />Node properties (class names, style, ...).</summary>
    public SyntaxNodeProperties? Properties { get; set; }

    /// <summary>子节点。<br />Child nodes.</summary>
    public List<SyntaxNode>? Children { get; set; }
}

/// <summary>一行内的语法文本段:节点及其外层包装元素。<br />One syntax span on a line: the node plus its wrapping element.</summary>
public sealed class SyntaxNodeSpan
{
    /// <summary>text 叶子节点。<br />The text leaf node.</summary>
    public SyntaxNode Node { get; set; } = new();

    /// <summary>
    ///     外层包装的 element 节点(直接挂在行上的元素);可为 <c>null</c>。<br />The wrapping element node (the element attached directly to
    ///     the line); may be <c>null</c>.
    /// </summary>
    public SyntaxNode? Wrapper { get; set; }
}

/// <summary>
///     highlightAST.ts 类型 SyntaxLine 的移植:一行的语法拆分结果。<br />Port of highlightAST.ts type SyntaxLine: the per-line syntax
///     split result.
/// </summary>
public sealed class SyntaxLine
{
    /// <summary>整行文本(各文本段拼接)。<br />The full line text (segments concatenated).</summary>
    public string Value { get; set; } = "";

    /// <summary>行号(1 基)。<br />Line number (1-based).</summary>
    public int LineNumber { get; set; }

    /// <summary>行文本长度。<br />Length of the line text.</summary>
    public int ValueLength { get; set; }

    /// <summary>该行的文本段列表。<br />The spans of this line.</summary>
    public List<SyntaxNodeSpan>? NodeList { get; set; }
}
