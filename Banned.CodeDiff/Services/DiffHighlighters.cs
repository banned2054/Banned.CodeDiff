using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services.TextMate;

namespace Banned.CodeDiff.Services;

/// <summary>
///     Holder of the built-in default syntax highlighter. JS counterpart: core's
///     file.ts imports the lowlight <c>highlighter</c> singleton as the built-in
///     engine; the C# port ships the TextMate-based <see cref="TextMate.TextMateHighlighter" />
///     as the built-in default instead (highlight.js is not ported).
///     Global mutable state, same as upstream — not thread-safe by design.
/// </summary>
public static class DiffHighlighters
{
    private static IDiffHighlighter? _default;

    /// <summary>
    ///     The engine used when <c>registerHighlighter</c> is not passed to
    ///     <see cref="DiffFile.InitSyntax" /> / <see cref="Models.SourceFile.DoSyntax" />.
    /// </summary>
    public static IDiffHighlighter Default => _default ??= TextMateHighlighter.Instance;
}
