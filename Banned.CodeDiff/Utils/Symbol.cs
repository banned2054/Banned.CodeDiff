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

public enum DiffModeEnum
{
    // github like
    SplitGitHub = 1,

    // gitlab like
    SplitGitLab = 2,
    Split       = 1 | 2,
    Unified     = 4,
}