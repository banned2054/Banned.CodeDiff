using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Services;

/// <summary>
///     packages/core/src/parse/template.ts 全局开关的移植（模板本体在 M2 阶段构建）。<br />Port of packages/core/src/parse/template.ts
///     global switches (template bodies are M2).
/// </summary>
/// <remarks>
///     这里的开关是进程级全局状态（与 JS 原版一致），禁止并发读写。<br />
///     These switches are process-wide global state (matching the JS original) and must not be read or written
///     concurrently.
/// </remarks>
public static class TemplateOptions
{
    /// <summary>是否启用基于 fast-diff 的模板（对应 JS 的 getEnableFastDiffTemplate）。<br />JS: getEnableFastDiffTemplate.</summary>
    public static bool EnableFastDiffTemplate { get; private set; }

    /// <summary>是否启用普通 diff 模板构建（对应 JS 的 getEnableBuildTemplate）。<br />JS: getEnableBuildTemplate.</summary>
    public static bool EnableBuildTemplate { get; private set; } = true;

    /// <summary>设置是否启用基于 fast-diff 的模板。<br />Sets whether the fast-diff based template is enabled.</summary>
    /// <param name="enable">是否启用。Whether to enable.</param>
    public static void SetEnableFastDiffTemplate(bool enable)
    {
        EnableFastDiffTemplate = enable;
    }

    /// <summary>将 fast-diff 模板开关重置为默认值（关闭）。<br />Resets the fast-diff template switch to its default value (off).</summary>
    public static void ResetEnableFastDiffTemplate()
    {
        EnableFastDiffTemplate = false;
    }

    /// <summary>设置是否启用普通 diff 模板构建。<br />Sets whether plain diff template building is enabled.</summary>
    /// <param name="enable">是否启用。Whether to enable.</param>
    public static void SetEnableBuildTemplate(bool enable)
    {
        EnableBuildTemplate = enable;
    }

    /// <summary>将普通 diff 模板构建开关重置为默认值（开启）。<br />Resets the plain diff template building switch to its default value (on).</summary>
    public static void ResetEnableBuildTemplate()
    {
        EnableBuildTemplate = true;
    }
}

/// <summary>packages/core/src/parse/diff-tool.ts 的移植。<br />Port of packages/core/src/parse/diff-tool.ts.</summary>
public static class DiffTool
{
    /// <summary>默认向一个 diff hunk 新增的行数。<br />How many new lines will be added to a diff hunk by default.</summary>
    public const int DefaultDiffExpansionStep = 40;

    /// <summary>
    ///     工具函数：返回一组 diff hunk 中最大的行号（自后向前找到第一行非 hunk 头的行）。<br />Utility function for getting the digit count of the
    ///     largest line number in an array of diff hunks
    /// </summary>
    /// <param name="hunks">hunk 列表。The list of hunks.</param>
    /// <returns>最大的行号；没有可用行时为 0。<br />The largest line number, or 0 when no usable line exists.</returns>
    public static int GetLargestLineNumber(IReadOnlyList<DiffHunk> hunks)
    {
        if (hunks.Count == 0) return 0;

        for (var i = hunks.Count - 1; i >= 0; i--)
        {
            var hunk = hunks[i];

            for (var j = hunk.Lines.Count - 1; j >= 0; j--)
            {
                var line = hunk.Lines[j];

                if (line.Type == DiffLineType.Hunk) continue;

                var newLineNumber = line.NewLineNumber ?? 0;
                var oldLineNumber = line.OldLineNumber ?? 0;
                return newLineNumber > oldLineNumber ? newLineNumber : oldLineNumber;
            }
        }

        return 0;
    }

    /// <summary>
    ///     计算 hunk 头部可以向上、向下或双向展开，还是空间过短以致展开会与上方 hunk 合并。<br />
    ///     Calculates whether or not a hunk header can be expanded up, down, both, or if
    ///     the space represented by the hunk header is short and expansion there would
    ///     mean merging with the hunk above.
    /// </summary>
    /// <param name="hunkIndex">hunk 在整个 diff 中的索引。<br />Index of the hunk to evaluate within the whole diff.</param>
    /// <param name="hunkHeader">待求值 hunk 的头部。<br />Header of the hunk to evaluate.</param>
    /// <param name="previousHunk">
    ///     待求值 hunk 的前一个 hunk；若是第一个 hunk 则为 null。<br />Hunk previous to the one to evaluate. Null if
    ///     the evaluated hunk is the first one.
    /// </param>
    /// <returns>该 hunk 头部的展开类型。<br />The expansion type of the hunk header.</returns>
    public static DiffHunkExpansionType GetHunkHeaderExpansionType(
        int            hunkIndex,
        DiffHunkHeader hunkHeader,
        DiffHunk?      previousHunk
    )
    {
        var distanceToPrevious =
            previousHunk == null
                ? double.PositiveInfinity
                : hunkHeader.OldStartLine - previousHunk.Header.OldStartLine - previousHunk.Header.OldLineCount;

        // In order to simplify the whole logic around expansion, only the hunk at the
        // top can be expanded up exclusively, and only the hunk at the bottom (the
        // dummy one, see getTextDiffWithBottomDummyHunk) can be expanded down
        // exclusively.
        // The rest of the hunks can be expanded both ways, except those which are too
        // short and therefore the direction of expansion doesn't matter.
        if (hunkIndex == 0)
            // The top hunk can only be expanded if there is content above it
            return hunkHeader is { OldStartLine: > 1, NewStartLine: > 1 }
                ? DiffHunkExpansionType.Up
                : DiffHunkExpansionType.None;

        return distanceToPrevious <= DefaultDiffExpansionStep
            ? DiffHunkExpansionType.Short
            : DiffHunkExpansionType.Both;
    }

    /// <summary>
    ///     按顺序调用 cb 共 num 次（i = 0..num-1），收集结果为列表。<br />Invokes cb num times in order (i = 0..num-1) and collects the
    ///     results into a list.
    /// </summary>
    /// <typeparam name="T">回调产出元素的类型。<br />The element type produced by the callback.</typeparam>
    /// <param name="num">迭代次数。<br />Number of iterations.</param>
    /// <param name="cb">接收索引并产出元素的回调。<br />Callback receiving the index and producing an element.</param>
    /// <returns>按顺序收集的结果列表。<br />The collected results in order.</returns>
    public static List<T> NumIterator<T>(int num, Func<int, T> cb)
    {
        var re = new List<T>();
        for (var i = 0; i < num; i++) re.Add(cb(i));

        return re;
    }

    /// <summary>
    ///     取文件名最后一个点号之后的扩展名；没有点号时返回整个文件名（复刻 JS slice 语义）。<br />Returns the extension after the last dot of the file name;
    ///     returns the whole name when there is no dot (JS slice semantics).
    /// </summary>
    /// <param name="fileName">文件名。<br />The file name.</param>
    /// <returns>扩展名或整个文件名。<br />The extension, or the whole file name.</returns>
    public static string GetLang(string fileName)
    {
        var dotIndex = fileName.LastIndexOf('.');
        // JS: fileName.slice(dotIndex + 1) — slice(0) when no dot, i.e. the whole name
        return dotIndex >= 0 ? fileName[(dotIndex + 1)..] : fileName;
    }

    /// <summary>JS: a || b || "" (first non-empty string wins).</summary>
    private static string JsOr(string? a, string? b)
    {
        if (!string.IsNullOrEmpty(a)) return a;

        return !string.IsNullOrEmpty(b) ? b : "";
    }

    /// <summary>
    ///     为每一对（新增行，删除行）计算词级 diff 范围。<br />
    ///     Compute the word-level diff ranges for each (addition, deletion) pair.
    ///     Port of parse/diff-tool.ts getDiffRange. The syntax/template parts
    ///     (getSyntaxDiffTemplate etc.) are built in M2; this port keeps the
    ///     relativeChanges / diffChanges computation and its raw-line cloning semantics.
    /// </summary>
    /// <param name="additions">新增行列表。<br />The addition lines.</param>
    /// <param name="deletions">删除行列表。<br />The deletion lines.</param>
    /// <param name="getAdditionRaw">按新行号获取原始行文本的回调。<br />Callback to get the raw line text by new line number.</param>
    /// <param name="getDeletionRaw">按旧行号获取原始行文本的回调。<br />Callback to get the raw line text by old line number.</param>
    /// <remarks>
    ///     结果写回传入的行对象（<c>Changes</c> / <c>DiffChanges</c> 等字段）；两个列表长度不相等时直接返回，不做任何计算。<br />
    ///     Results are written back onto the passed lines (<c>Changes</c> / <c>DiffChanges</c> etc.);
    ///     when the two lists have different lengths the method returns without computing anything.
    /// </remarks>
    public static void GetDiffRange(
        List<DiffLine>     additions,
        List<DiffLine>     deletions,
        Func<int, string?> getAdditionRaw,
        Func<int, string?> getDeletionRaw
    )
    {
        if (additions.Count != deletions.Count) return;
        var len = additions.Count;
        for (var i = 0; i < len; i++)
        {
            var addition = additions[i];
            var deletion = deletions[i];
            if (addition.Changes == null || deletion.Changes == null)
            {
                // use the original text content to computed diff range
                // fix: get diff with ignoreWhiteSpace config
                var _addition = addition.Clone(JsOr(getAdditionRaw(addition.NewLineNumber ?? 0), addition.Text));
                var _deletion = deletion.Clone(JsOr(getDeletionRaw(deletion.OldLineNumber ?? 0), deletion.Text));
                var (addRange, delRange) = ChangeRange.RelativeChanges(_addition, _deletion);
                addition.Changes         = addRange;
                deletion.Changes         = delRange;
            }

            var buildTemplate = TemplateOptions.EnableBuildTemplate;
            if (!TemplateOptions.EnableFastDiffTemplate)
            {
                // M2: getPlainDiffTemplate / getSyntaxDiffTemplate calls happen here
                _ = buildTemplate;
            }
            else
            {
                var _addition = addition.Clone(JsOr(getAdditionRaw(addition.NewLineNumber ?? 0), addition.Text));
                var _deletion = deletion.Clone(JsOr(getDeletionRaw(deletion.OldLineNumber ?? 0), deletion.Text));
                var (addRange, delRange)     = ChangeRange.DiffChanges(_addition, _deletion);
                addition.DiffChanges         = addRange;
                deletion.DiffChanges         = delRange;
                addition.InternalDiffChanges = delRange;
                deletion.InternalDiffChanges = addRange;
                // M2: getPlainDiffTemplateByFastDiff / getSyntaxDiffTemplateByFastDiff calls happen here
            }
        }
    }
}
