using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Models;

/// <summary>
/// Port of packages/core/src/file.ts — the raw (non-syntax) part only. Syntax
/// highlighting state (ast / syntaxFile / highlighterName) is M2 scope.
/// The cross-instance file cache (Cache/`getFile`) is a web-specific perf
/// optimization and is intentionally not ported; every source file is fresh.
/// </summary>
public sealed class SourceFile(string row, string lang, string? fileName = null)
{
    public string Raw { get; } = Transform.ProcessTransformForFile(row);

    public string Lang { get; } = lang;

    public string? FileName { get; } = fileName;

    /// <summary>JS: rawFile — 1-based line number → line content (trailing "\n" kept except on the last line).</summary>
    public Dictionary<int, string> RawFile { get; private set; } = new();

    public bool HasDoRaw { get; private set; }

    public int? RawLength { get; private set; }

    public int MaxLineNumber { get; private set; }

    public void DoRaw()
    {
        if (Raw.Length == 0 || HasDoRaw)
        {
            return;
        }

        var rawString = Raw;

        var rawArray = rawString.Split('\n');

        RawLength = rawArray.Length;

        MaxLineNumber = rawArray.Length;

        RawFile = new Dictionary<int, string>(rawArray.Length);

        for (var i = 0; i < rawArray.Length; i++)
        {
            RawFile[i + 1] = i < rawArray.Length - 1 ? rawArray[i] + "\n" : rawArray[i];
        }

        HasDoRaw = true;
    }
}