using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Utils;

/// <summary>
/// Port of packages/utils/src/highlightAST.ts (processAST): walks the
/// highlighter-produced tree, splits text nodes containing newlines into
/// per-line child nodes, and buckets everything into 1-based per-line
/// <see cref="SyntaxLine"/> records. Shared by every highlighter engine.
/// </summary>
public static class HighlightAst
{
    public static SyntaxAstResult ProcessAst(SyntaxNode ast)
    {
        var lineNumber = 1;

        var syntaxObj = new Dictionary<int, SyntaxLine>();

        void AppendToLine(int line, SyntaxNode node, SyntaxNode? wrapper)
        {
            var valueLength = node.Value.Length;

            if (!syntaxObj.TryGetValue(line, out var item))
            {
                node.StartIndex = 0;
                node.EndIndex   = valueLength - 1;
                syntaxObj[line] = new SyntaxLine
                {
                    Value       = node.Value,
                    LineNumber  = line,
                    ValueLength = valueLength,
                    NodeList    = [new SyntaxNodeSpan { Node = node, Wrapper = wrapper }],
                };
            }
            else
            {
                node.StartIndex = item.ValueLength;
                node.EndIndex   = node.StartIndex + valueLength - 1;
                item.Value       += node.Value;
                item.ValueLength += valueLength;
                item.NodeList!.Add(new SyntaxNodeSpan { Node = node, Wrapper = wrapper });
            }
        }

        void LoopAst(List<SyntaxNode> nodes, SyntaxNode? wrapper)
        {
            foreach (var node in nodes)
            {
                if (node.Type == "text")
                {
                    if (!node.Value.Contains('\n'))
                    {
                        AppendToLine(lineNumber, node, wrapper);

                        node.LineNumber = lineNumber;

                        continue;
                    }

                    var lines = node.Value.Split('\n');

                    node.Children ??= [];

                    for (var i = 0; i < lines.Length; i++)
                    {
                        // JS: i === lines.length - 1 ? lines[i] : lines[i] + "\n"
                        var value       = i == lines.Length - 1 ? lines[i] : lines[i] + "\n";

                        // JS: i === 0 ? lineNumber : ++lineNumber
                        var line        = i == 0 ? lineNumber : ++lineNumber;

                        var child = new SyntaxNode
                        {
                            Type       = "text",
                            Value      = value,

                            // JS: Infinity placeholder — immediately overwritten by
                            // AppendToLine below, never used in computation.
                            StartIndex = int.MaxValue,
                            EndIndex   = int.MaxValue,
                            LineNumber = line,
                        };

                        AppendToLine(line, child, wrapper);

                        node.Children.Add(child);
                    }

                    node.LineNumber = lineNumber;

                    continue;
                }

                if (node.Children != null)
                {
                    LoopAst(node.Children, node);

                    node.LineNumber = lineNumber;
                }
            }
        }

        LoopAst(ast.Children ?? [], null);

        return new SyntaxAstResult(syntaxObj, lineNumber);
    }
}
