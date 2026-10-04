using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Avalonia.Utils;

/// <summary>
///     Extracts word-level highlight ranges from a <see cref="DiffLine" />, mirroring the upstream
///     template logic: fast-diff segments (<c>diffChanges</c>) are preferred when available, falling
///     back to the single relative-changes range (<c>changes</c>). Only the segments matching the
///     cell kind are kept — insert segments on added lines, delete segments on deleted lines.
/// </summary>
internal static class DiffHighlights
{
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
