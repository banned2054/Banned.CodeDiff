using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Avalonia.Utils;

/// <summary>
///     从 <see cref="DiffLine" /> 提取词级高亮范围,对应上游的 template 逻辑:优先使用
///     fast-diff 文本段(<c>diffChanges</c>),不可用时回退到单一的相对变更范围
///     (<c>changes</c>);只保留与单元格类别匹配的文本段——新增行取 insert 段,删除行取
///     delete 段。<br />
///     Extracts word-level highlight ranges from a <see cref="DiffLine" />, mirroring the upstream
///     template logic: fast-diff segments (<c>diffChanges</c>) are preferred when available, falling
///     back to the single relative-changes range (<c>changes</c>). Only the segments matching the
///     cell kind are kept — insert segments on added lines, delete segments on deleted lines.
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
