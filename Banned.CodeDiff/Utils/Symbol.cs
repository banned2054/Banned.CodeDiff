namespace Banned.CodeDiff.Utils;

// Port of packages/utils/src/symbol.ts
public enum NewLineSymbol
{
    CRLF    = 1,
    CR      = 2,
    LF      = 3,
    NEWLINE = 4,
    NORMAL  = 5,
    NULL    = 6,
}

public static class Symbol
{
    public static string GetSymbol(NewLineSymbol? symbol)
    {
        return symbol switch
        {
            NewLineSymbol.LF   => "␊",
            NewLineSymbol.CR   => "␍",
            NewLineSymbol.CRLF => "␍␊",
            _                  => "",
        };
    }
}
