using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Avalonia.Utils;

/// <summary>
///     提取新增/删除行的词级高亮:优先 fast-diff 文本段,否则使用相对变更范围。<br />
///     Extracts added/deleted word highlights, preferring fast-diff segments and falling back to relative change ranges.
/// </summary>
internal static class DiffHighlights
{
    /// <summary>
    ///     从 <paramref name="diff" /> 提取与 <paramref name="kind" /> 匹配的词级高亮范围。<br />
    ///     Extracts the word-level highlight ranges from <paramref name="diff" /> that match
    ///     <paramref name="kind" />.
    /// </summary>
    /// <param name="diff">当前行的 diff 模型。The line's diff model.</param>
    /// <param name="kind">单元格类别;仅 Add / Delete 有效。The cell kind; only Add / Delete apply.</param>
    /// <returns>高亮范围列表;上下文或空单元格返回空列表。<br />The highlight ranges; empty for context or empty cells.</returns>
    public static IReadOnlyList<DiffHighlight> Extract(DiffLine diff, DiffCellKind kind)
    {
        if (kind is not (DiffCellKind.Add or DiffCellKind.Delete)) return [];

        if (diff.DiffChanges is { HasLineChange: true } fastDiff)
        {
            var operation = kind == DiffCellKind.Add ? DiffOp.Insert : DiffOp.Delete;

            return fastDiff.Range
                           .Where(item => item.Type == operation && item.Length > 0)
                           .Select(item => new DiffHighlight(item.StartIndex, item.Length))
                           .ToArray();
        }

        if (diff.Changes is { HasLineChange: true, Range.Length: > 0 } relative)
            return [new DiffHighlight(relative.Range.Location, relative.Range.Length)];

        return [];
    }
}
