using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Utils;

/// <summary>
///     语法树逐行处理工具,移植自 packages/utils/src/highlightAST.ts。<br />
///     Per-line syntax tree helpers ported from packages/utils/src/highlightAST.ts.
/// </summary>
public static class HighlightAst
{
    /// <summary>
    ///     按 1 基行号整理 AST,就地标注节点位置并拆分含换行的文本节点。<br />
    ///     Groups the AST by 1-based line number, annotating positions in place and splitting multiline text nodes.
    /// </summary>
    /// <param name="ast">高亮器产出的根节点。The root node produced by the highlighter.</param>
    /// <returns>逐行文本段集合与总行数。The per-line span records and the total line count.</returns>
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
                    NodeList    = [new SyntaxNodeSpan { Node = node, Wrapper = wrapper }]
                };
            }
            else
            {
                node.StartIndex  =  item.ValueLength;
                node.EndIndex    =  node.StartIndex + valueLength - 1;
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
                        var value = i == lines.Length - 1 ? lines[i] : lines[i] + "\n";

                        var line = i == 0 ? lineNumber : ++lineNumber;

                        var child = new SyntaxNode
                        {
                            Type  = "text",
                            Value = value,

                            // JS 的 Infinity 占位值会立即被 AppendToLine 覆盖,不参与计算。
                            StartIndex = int.MaxValue,
                            EndIndex   = int.MaxValue,
                            LineNumber = line
                        };

                        AppendToLine(line, child, wrapper);

                        node.Children.Add(child);
                    }

                    node.LineNumber = lineNumber;

                    continue;
                }

                if (node.Children == null) continue;
                LoopAst(node.Children, node);

                node.LineNumber = lineNumber;
            }
        }

        LoopAst(ast.Children ?? [], null);

        return new SyntaxAstResult(syntaxObj, lineNumber);
    }
}
