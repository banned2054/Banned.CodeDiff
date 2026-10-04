using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services.TextMate;

namespace Banned.CodeDiff.Services;

/// <summary>
///     内置默认语法高亮器的持有者。JS 侧对应：core 的 file.ts 引入 lowlight 的
///     <c>highlighter</c> 单例作为内置引擎；C# 移植版改为内置基于 TextMate 的
///     <see cref="TextMate.TextMateHighlighter" />（highlight.js 未移植）。
///     与上游一致的全局可变状态——设计上非线程安全。<br />
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
    ///     未向 <see cref="DiffFile.InitSyntax" /> / <see cref="Models.SourceFile.DoSyntax" />
    ///     传入 <c>registerHighlighter</c> 时使用的引擎。<br />
    ///     The engine used when <c>registerHighlighter</c> is not passed to
    ///     <see cref="DiffFile.InitSyntax" /> / <see cref="Models.SourceFile.DoSyntax" />.
    /// </summary>
    public static IDiffHighlighter Default => _default ??= TextMateHighlighter.Instance;
}
