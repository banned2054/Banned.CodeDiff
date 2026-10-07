using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services.TextMate;

namespace Banned.CodeDiff.Services;

/// <summary>
///     内置 TextMate 高亮器的全局持有者;非线程安全。<br />
///     Global holder of the built-in TextMate highlighter; not thread-safe.
/// </summary>
public static class DiffHighlighters
{
    private static IDiffHighlighter? _default;

    /// <summary>
    ///     未向 <see cref="DiffFile.InitSyntax" /> / <see cref="Models.SourceFile.DoSyntax" />
    ///     传入 <c>registerHighlighter</c> 时使用的引擎。<br />
    ///     The engine used when <c>registerHighlighter</c> is not passed to
    ///     <see cref="DiffFile.InitSyntax" /> / <see cref="Models.SourceFile.DoSyntax" />.
    /// </summary>
    public static IDiffHighlighter Default => _default ??= TextMateHighlighter.Instance;
}
