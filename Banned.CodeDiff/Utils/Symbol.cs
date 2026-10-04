namespace Banned.CodeDiff.Utils;

// Port of packages/utils/src/symbol.ts
/// <summary>
///     换行符显示符号(packages/utils/src/symbol.ts 的移植)。<br />
///     Newline display symbols (port of packages/utils/src/symbol.ts).
/// </summary>
public enum NewLineSymbol
{
    /// <summary>回车 + 换行,显示符号 ␍␊。<br />Carriage return + line feed, shown as ␍␊.</summary>
    CRLF = 1,

    /// <summary>回车,显示符号 ␍。<br />Carriage return, shown as ␍.</summary>
    CR = 2,

    /// <summary>换行,显示符号 ␊。<br />Line feed, shown as ␊.</summary>
    LF = 3,

    /// <summary>泛指换行符,无固定显示符号。<br />Generic newline, no fixed display symbol.</summary>
    NEWLINE = 4,

    /// <summary>常规取值,无固定显示符号。<br />Normal value, no fixed display symbol.</summary>
    NORMAL = 5,

    /// <summary>空取值,无显示符号。<br />Null value, no display symbol.</summary>
    NULL = 6
}

/// <summary>
///     packages/utils/src/symbol.ts 的移植:换行符显示符号辅助,无状态纯函数。<br />
///     Port of packages/utils/src/symbol.ts: newline display-symbol helper; stateless pure functions.
/// </summary>
public static class Symbol
{
    /// <summary>
    ///     返回换行符的显示符号(LF ␊ / CR ␍ / CRLF ␍␊);其余取值(含 <c>null</c>)返回空字符串。<br />Returns the display symbol for a newline
    ///     symbol (LF ␊ / CR ␍ / CRLF ␍␊); any other value (including <c>null</c>) yields the empty string.
    /// </summary>
    /// <param name="symbol">换行符取值,可为 <c>null</c>。The newline symbol value; may be <c>null</c>.</param>
    public static string GetSymbol(NewLineSymbol? symbol)
    {
        return symbol switch
        {
            NewLineSymbol.LF   => "␊",
            NewLineSymbol.CR   => "␍",
            NewLineSymbol.CRLF => "␍␊",
            _                  => ""
        };
    }
}
