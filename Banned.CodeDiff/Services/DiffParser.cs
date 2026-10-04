using Banned.CodeDiff.Models;
using System.Text.RegularExpressions;

namespace Banned.CodeDiff.Services;

/// <summary>Port of packages/core/src/parse/diff-parse.ts.</summary>
//
// !NOTE: ALL of the diff parse logic copy from desktop, SEE https://github.com/desktop/desktop
// With mirror change
//
// https://en.wikipedia.org/wiki/Diff_utility
//
// @@ -l,s +l,s @@ optional section heading
//
// The hunk range information contains two hunk ranges. The range for the hunk of the original
// file is preceded by a minus symbol, and the range for the new file is preceded by a plus
// symbol. Each hunk range is of the format l,s where l is the starting line number and s is
// the number of lines the change hunk applies to for each respective file.
//
// In many versions of GNU diff, each range can omit the comma and trailing value s,
// in which case s defaults to 1
public static class DiffParserConstants
{
    public const string DiffPrefixAdd     = "+";
    public const string DiffPrefixDelete  = "-";
    public const string DiffPrefixContext = " ";

    public const string DiffPrefixNoNewline = "\\";

    // https://github.com/MrWangJustToDo/git-diff-view/issues/41
    // some line only have a new line symbol without any other character
    public const string DiffPrefixNewLine = "\n";

    // in which case s defaults to 1
    public static readonly Regex DiffHeaderRegex =
        new(@"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@", RegexOptions.Compiled);

    /// <summary>
    ///     Regular expression matching invisible bidirectional Unicode characters that may
    ///     be interpreted or compiled differently than what it appears. More info:
    ///     https://github.co/hiddenchars
    /// </summary>
    public static readonly Regex HiddenBidiCharsRegex = new(@"[\u202A-\u202E]|[\u2066-\u2069]", RegexOptions.Compiled);
}

/// <summary>
///     A parser for the GNU unified diff format.
///     See https://www.gnu.org/software/diffutils/manual/html_node/Detailed-Unified.html
/// </summary>
public sealed class DiffParser
{
    // JS: /\n\\ No newline at end of file/g
    private static readonly Regex NoNewlineMarkerRegex = new(@"\n\\ No newline at end of file", RegexOptions.Compiled);

    /// <summary>
    ///     Line end pointer. The offset into the text property where the current line ends
    ///     (ie it points to the newline character).
    /// </summary>
    private int _le;

    /// <summary>
    ///     Line start pointer. The offset into the text property where the current line
    ///     starts (ie either zero or one character ahead of the last newline character).
    /// </summary>
    private int _ls;

    /// <summary>The text buffer containing the raw, unified diff output to be parsed</summary>
    private string _text = "";

    public DiffParser()
    {
        Reset();
    }

    /// <summary>JS export: parseInstance (a shared parser instance).</summary>
    public static DiffParser Shared { get; } = new();

    /// <summary>Resets the internal parser state so that it can be reused.</summary>
    private void Reset()
    {
        _ls   = 0;
        _le   = -1;
        _text = "";
    }

    /// <summary>
    ///     Aligns the internal character pointers at the boundaries of the next line.
    ///     Returns true if successful or false if the end of the diff has been reached.
    /// </summary>
    private bool NextLine()
    {
        _ls = _le + 1;

        // We've reached the end of the diff
        if (_ls >= _text.Length) return false;

        _le = _text.IndexOf('\n', _ls);

        // If we can't find the next newline character we'll put our
        // end pointer at the end of the diff string
        if (_le == -1) _le = _text.Length;

        // We've succeeded if there's anything to read in between the
        // start and the end
        //
        // https://github.com/MrWangJustToDo/git-diff-view/issues/41
        // some file content may have multiple line without any character
        // such as
        // ```
        //
        //
        //
        // +
        // +
        // ```
        // this will cause ls === le, but we not in the end of file
        return _ls != _le;
    }

    /// <summary>
    ///     Advances to the next line and returns it as a substring of the raw diff text.
    ///     Returns null if end of diff was reached.
    /// </summary>
    private string? ReadLine(bool header)
    {
        if (header) return NextLine() ? JsSubstring(_text, _ls, _le) : null;

        // JS: text.substring(ls + 1, le + 1) — substring clamps end at the string
        // boundary (last line without a trailing newline)
        return NextLine()
            ? JsSubstring(_text, _ls + 1, _le + 1)
            : _text.Length > _ls
                ? "\n"
                : null;
    }

    /// <summary>Tests if the current line starts with the given search text</summary>
    private bool LineStartsWith(string searchString)
    {
        return _text.AsSpan(_ls).StartsWith(searchString, StringComparison.Ordinal);
    }

    /// <summary>Tests if the current line ends with the given search text</summary>
    private bool LineEndsWith(string searchString)
    {
        return _le >= 0 && _text.AsSpan(0, _le).EndsWith(searchString, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Returns the starting character of the next line without advancing the internal
    ///     state. Returns null if advancing would mean reaching the end of the diff.
    /// </summary>
    private char? Peek()
    {
        var p = _le + 1;
        return p < _text.Length ? _text[p] : null;
    }

    /// <summary>
    ///     Parse the diff header, meaning everything from the start of the diff output to
    ///     the end of the line beginning with +++
    ///     Example diff header:
    ///     diff --git a/app/src/lib/diff-parser.ts b/app/src/lib/diff-parser.ts
    ///     index e1d4871..3bd3ee0 100644
    ///     --- a/app/src/lib/diff-parser.ts
    ///     +++ b/app/src/lib/diff-parser.ts
    ///     Returns header info extracted from the diff header (currently whether it's a
    ///     binary patch) or null if the end of the diff was reached before the +++ line
    ///     could be found (which is a valid state).
    /// </summary>
    private DiffHeaderInfo? ParseDiffHeader()
    {
        // TODO: There's information in here that we might want to
        // capture, such as mode changes
        // check valid header (JS keeps `hasMinus` only for a __DEV__ console error)
        while (NextLine())
        {
            if (LineStartsWith("Binary files ") && LineEndsWith("differ"))
                return new DiffHeaderInfo { IsBinary = true };

            if (LineStartsWith("---"))
            {
                // JS: hasMinus = true (only consumed by a __DEV__ console error)
            }

            if (LineStartsWith("+++")) return new DiffHeaderInfo { IsBinary = false };
        }

        // It's not an error to not find the +++ line, see the
        // 'parses diff of empty file' test in diff-parser-tests.ts
        return null;
    }

    /// <summary>
    ///     Attempts to convert a RegExp capture group into a number. If the group doesn't
    ///     exist or wasn't captured the function will return the value of the defaultValue
    ///     parameter or throw an error if no default value was provided. If the captured
    ///     string can't be converted to a number an error will be thrown.
    /// </summary>
    private static int NumberFromGroup(Match m, int group, int? defaultValue = null)
    {
        var g = m.Groups[group];
        if (g.Success && g.Value.Length != 0)
            return !int.TryParse(g.Value, out var num)
                ? throw new InvalidOperationException($"Could not parse capture group {group} into number: {g.Value}")
                : num;
        if (defaultValue == null)
            throw new
                InvalidOperationException($"Group {group} missing from regexp match and no defaultValue was provided");

        return defaultValue.Value;
    }

    /// <summary>
    ///     Parses a hunk header or throws an error if the given line isn't a well-formed
    ///     hunk header.
    ///     We currently only extract the line number information and ignore any hunk
    ///     headings.
    ///     Example hunk header (text within ``):
    ///     `@@ -84,10 +82,8 @@ export function parseRawDiff(lines: ReadonlyArray&lt;string&gt;): Diff {`
    ///     Where everything after the last @@ is what's known as the hunk, or section, heading
    /// </summary>
    private static DiffHunkHeader ParseHunkHeader(string line)
    {
        var m = DiffParserConstants.DiffHeaderRegex.Match(line);
        if (!m.Success) throw new InvalidOperationException("Invalid hunk header format");

        // If endLines are missing default to 1, see diffHeaderRe docs
        var oldStartLine = NumberFromGroup(m, 1);
        var oldLineCount = NumberFromGroup(m, 2, 1);
        var newStartLine = NumberFromGroup(m, 3);
        var newLineCount = NumberFromGroup(m, 4, 1);

        return new DiffHunkHeader(oldStartLine, oldLineCount, newStartLine, newLineCount);
    }

    /// <summary>
    ///     Convenience function which lets us leverage the type system to prove exhaustive
    ///     checks in parseHunk.
    ///     Takes an arbitrary string and checks to see if the first character of that string
    ///     is one of the allowed prefix characters for diff lines (ie lines in between hunk
    ///     headers).
    /// </summary>
    private static char? ParseLinePrefix(char? c)
    {
        if (c.HasValue && (c.Value == DiffParserConstants.DiffPrefixAdd[0]       ||
                           c.Value == DiffParserConstants.DiffPrefixDelete[0]    ||
                           c.Value == DiffParserConstants.DiffPrefixContext[0]   ||
                           c.Value == DiffParserConstants.DiffPrefixNoNewline[0] ||
                           c.Value == DiffParserConstants.DiffPrefixNewLine[0]))
            return c;

        return null;
    }

    /// <summary>
    ///     Parses a hunk, including its header or throws an error if the diff doesn't
    ///     contain a well-formed diff hunk at the current position.
    ///     Expects that the position has been advanced to the beginning of a presumed diff
    ///     hunk header.
    /// </summary>
    /// <param name="linesConsumed">
    ///     The number of unified diff lines consumed up until this point by the diff
    ///     parser. Used to give the position and length (in lines) of the parsed hunk
    ///     relative to the overall parsed diff. These numbers have no real meaning in the
    ///     context of a diff and are only used to aid the app in line-selections.
    /// </param>
    private DiffHunk ParseHunk(int linesConsumed, int hunkIndex, DiffHunk? previousHunk)
    {
        var headerLine = ReadLine(true);
        if (headerLine == null) throw new InvalidOperationException("Expected hunk header but reached end of diff");

        var header = ParseHunkHeader(headerLine);
        var lines  = new List<DiffLine> { new(headerLine, DiffLineType.Hunk, 1, null, null) };

        char? c;

        var rollingDiffBeforeCounter = header.OldStartLine;
        var rollingDiffAfterCounter  = header.NewStartLine;

        var diffLineNumber = linesConsumed;
        while ((c = ParseLinePrefix(Peek())) != null)
        {
            var line = ReadLine(false);

            if (line == null) throw new InvalidOperationException("Expected unified diff line but reached end of diff");

            // A marker indicating that the last line in the original or the new file
            // is missing a trailing newline. In other words, the presence of this marker
            // means that the new and/or original file lacks a trailing newline.
            //
            // When we find it we have to look up the previous line and set the
            // noTrailingNewLine flag
            if (c == DiffParserConstants.DiffPrefixNoNewline[0])
            {
                // See https://github.com/git/git/blob/21f862b498925194f8f1ebe8203b7a7df756555b/apply.c#L1725-L1732
                if (line.Length < 12)
                    throw new
                        InvalidOperationException("Expected \"no newline at end of file\" marker to be at least 12 bytes long");

                var previousLineIndex = lines.Count - 1;
                var previousLine      = lines[previousLineIndex];
                lines[previousLineIndex] = previousLine.WithNoTrailingNewLine(true);

                continue;
            }

            // We must increase `diffLineNumber` only when we're certain that the line
            // is not a "no newline" marker. Otherwise, we'll end up with a wrong
            // `diffLineNumber` for the next line. This could happen if the last line
            // in the file doesn't have a newline before the change.
            diffLineNumber++;

            DiffLine diffLine;

            if (c == DiffParserConstants.DiffPrefixAdd[0])
                diffLine = new DiffLine(line, DiffLineType.Add, diffLineNumber, null, rollingDiffAfterCounter++);
            else if (c == DiffParserConstants.DiffPrefixDelete[0])
                diffLine = new DiffLine(line, DiffLineType.Delete, diffLineNumber, rollingDiffBeforeCounter++, null);
            else if (c == DiffParserConstants.DiffPrefixContext[0] || c == DiffParserConstants.DiffPrefixNewLine[0])
                diffLine = new DiffLine(line, DiffLineType.Context, diffLineNumber, rollingDiffBeforeCounter++,
                                        rollingDiffAfterCounter++);
            else
                return AssertNever($"Unknown DiffLinePrefix: {c}");

            lines.Add(diffLine);
        }

        if (lines.Count == 1) throw new InvalidOperationException("Malformed diff, empty hunk");

        return new DiffHunk(header, lines, linesConsumed, linesConsumed + lines.Count - 1,
                            DiffTool.GetHunkHeaderExpansionType(hunkIndex, header, previousHunk));
    }

    private static DiffHunk AssertNever(string message)
    {
        throw new InvalidOperationException(message);
    }

    /// <summary>
    ///     Parse a well-formed unified diff into hunks and lines.
    /// </summary>
    /// <param name="text">
    ///     A unified diff produced by git diff, git log --patch or any other git plumbing
    ///     command that produces unified diffs.
    /// </param>
    public RawDiff Parse(string text)
    {
        _text = text;

        try
        {
            var headerInfo = ParseDiffHeader();

            var headerEnd = _le;
            var header    = JsSubstring(_text, 0, headerEnd);

            // empty diff
            if (headerInfo == null)
                return new RawDiff
                {
                    Header             = header,
                    Contents           = "",
                    Hunks              = [],
                    IsBinary           = false,
                    MaxLineNumber      = 0,
                    HasHiddenBidiChars = false
                };

            if (headerInfo.IsBinary)
                return new RawDiff
                {
                    Header             = header,
                    Contents           = "",
                    Hunks              = [],
                    IsBinary           = true,
                    MaxLineNumber      = 0,
                    HasHiddenBidiChars = false
                };

            var       hunks         = new List<DiffHunk>();
            var       linesConsumed = 0;
            DiffHunk? previousHunk  = null;

            while (Peek() != null)
            {
                var hunk = ParseHunk(linesConsumed, hunks.Count, previousHunk);
                hunks.Add(hunk);
                previousHunk  =  hunk;
                linesConsumed += hunk.Lines.Count;
            }

            var contents = NoNewlineMarkerRegex.Replace(JsSubstring(_text, headerEnd + 1, _le), "");

            return new RawDiff
            {
                Header             = header,
                Contents           = contents,
                Hunks              = hunks,
                IsBinary           = headerInfo.IsBinary,
                MaxLineNumber      = DiffTool.GetLargestLineNumber(hunks),
                HasHiddenBidiChars = DiffParserConstants.HiddenBidiCharsRegex.IsMatch(text)
            };
        }
        finally
        {
            Reset();
        }
    }

    /// <summary>
    ///     JS String.prototype.substring(start, end) — swaps start/end when start &gt; end
    ///     and clamps out-of-range values instead of throwing.
    /// </summary>
    private static string JsSubstring(string s, int start, int end)
    {
        if (start > end) (start, end) = (end, start);

        start = Math.Max(0, Math.Min(start, s.Length));
        end   = Math.Max(0, Math.Min(end, s.Length));
        return s.Substring(start, end - start);
    }

    private sealed class DiffHeaderInfo
    {
        /// <summary>
        ///     Whether or not the diff header contained a marker indicating that a diff
        ///     couldn't be produced due to the contents of the new and/or old file was binary.
        /// </summary>
        public bool IsBinary { get; init; }
    }
}
