namespace Banned.CodeDiff.Models;

// Port of packages/utils/src/highlightAST.ts (types only; processAST belongs to
// the lowlight/highlighter milestone, not the core/parse port).

/// <summary>
/// JS type: SyntaxNode.properties (className + free-form extras).
/// Only className and style are consumed by the template builders.
/// </summary>
public sealed class SyntaxNodeProperties
{
    public List<string>? ClassName { get; set; }

    public string? Style { get; set; }
}

public sealed class SyntaxNode
{
    public string Type { get; set; } = "";

    public string Value { get; set; } = "";

    public int LineNumber { get; set; }

    public int StartIndex { get; set; }

    public int EndIndex { get; set; }

    public SyntaxNodeProperties? Properties { get; set; }

    public List<SyntaxNode>? Children { get; set; }
}

public sealed class SyntaxNodeSpan
{
    public SyntaxNode Node { get; set; } = new();

    public SyntaxNode? Wrapper { get; set; }
}

public sealed class SyntaxLine
{
    public string Value { get; set; } = "";

    public int LineNumber { get; set; }

    public int ValueLength { get; set; }

    public List<SyntaxNodeSpan>? NodeList { get; set; }
}