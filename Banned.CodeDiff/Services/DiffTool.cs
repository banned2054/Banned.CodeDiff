using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Services;

/// <summary>Port of packages/core/src/parse/template.ts global switches (template bodies are M2).</summary>
public static class TemplateOptions
{
    private static bool _enableFastDiffTemplate;

    /// <summary>JS: getEnableFastDiffTemplate.</summary>
    public static bool EnableFastDiffTemplate => _enableFastDiffTemplate;

    public static void SetEnableFastDiffTemplate(bool enable)
    {
        _enableFastDiffTemplate = enable;
    }

    public static void ResetEnableFastDiffTemplate()
    {
        _enableFastDiffTemplate = false;
    }

    private static bool _enableBuildTemplate = true;

    /// <summary>JS: getEnableBuildTemplate.</summary>
    public static bool EnableBuildTemplate => _enableBuildTemplate;

    public static void SetEnableBuildTemplate(bool enable)
    {
        _enableBuildTemplate = enable;
    }

    public static void ResetEnableBuildTemplate()
    {
        _enableBuildTemplate = true;
    }
}

/// <summary>Port of packages/core/src/parse/diff-tool.ts.</summary>
public static class DiffTool
{
    /// <summary>How many new lines will be added to a diff hunk by default.</summary>
    public const int DefaultDiffExpansionStep = 40;

    /// <summary>Utility function for getting the digit count of the largest line number in an array of diff hunks</summary>
    public static int GetLargestLineNumber(IReadOnlyList<DiffHunk> hunks)
    {
        if (hunks.Count == 0)
        {
            return 0;
        }

        for (var i = hunks.Count - 1; i >= 0; i--)
        {
            var hunk = hunks[i];

            for (var j = hunk.Lines.Count - 1; j >= 0; j--)
            {
                var line = hunk.Lines[j];

                if (line.Type == DiffLineType.Hunk)
                {
                    continue;
                }

                var newLineNumber = line.NewLineNumber ?? 0;
                var oldLineNumber = line.OldLineNumber ?? 0;
                return newLineNumber > oldLineNumber ? newLineNumber : oldLineNumber;
            }
        }

        return 0;
    }

    /// <summary>
    /// Calculates whether or not a hunk header can be expanded up, down, both, or if
    /// the space represented by the hunk header is short and expansion there would
    /// mean merging with the hunk above.
    /// </summary>
    /// <param name="hunkIndex">Index of the hunk to evaluate within the whole diff.</param>
    /// <param name="hunkHeader">Header of the hunk to evaluate.</param>
    /// <param name="previousHunk">Hunk previous to the one to evaluate. Null if the evaluated hunk is the first one.</param>
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
        {
            // The top hunk can only be expanded if there is content above it
            return hunkHeader is { OldStartLine: > 1, NewStartLine: > 1 }
                ? DiffHunkExpansionType.Up
                : DiffHunkExpansionType.None;
        }
        else if (distanceToPrevious <= DefaultDiffExpansionStep)
        {
            return DiffHunkExpansionType.Short;
        }
        else
        {
            return DiffHunkExpansionType.Both;
        }
    }

    public static List<T> NumIterator<T>(int num, Func<int, T> cb)
    {
        var re = new List<T>();
        for (var i = 0; i < num; i++)
        {
            re.Add(cb(i));
        }

        return re;
    }

    public static string GetLang(string fileName)
    {
        var dotIndex = fileName.LastIndexOf('.');
        // JS: fileName.slice(dotIndex + 1) — slice(0) when no dot, i.e. the whole name
        return dotIndex >= 0 ? fileName[(dotIndex + 1)..] : fileName;
    }

    /// <summary>JS: a || b || "" (first non-empty string wins).</summary>
    private static string JsOr(string? a, string? b)
    {
        if (!string.IsNullOrEmpty(a))
        {
            return a;
        }

        return !string.IsNullOrEmpty(b) ? b : "";
    }

    /// <summary>
    /// Compute the word-level diff ranges for each (addition, deletion) pair.
    ///
    /// Port of parse/diff-tool.ts getDiffRange. The syntax/template parts
    /// (getSyntaxDiffTemplate etc.) are built in M2; this port keeps the
    /// relativeChanges / diffChanges computation and its raw-line cloning semantics.
    /// </summary>
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
