namespace Banned.CodeDiff.Utils;

// Port of packages/core/src/escape-html.ts
// copy from https://github.com/vuejs/core/blob/main/packages/shared/src/escapeHtml.ts
public static class EscapeHtml
{
    public static string Escape(string? input)
    {
        var str = input ?? "";

        var firstIndex = IndexOfSpecialChar(str);

        if (firstIndex < 0)
        {
            return str;
        }

        var html      = "";
        var lastIndex = 0;
        int index;

        for (index = firstIndex; index < str.Length; index++)
        {
            string escaped;
            switch (str[index])
            {
                case '"' :
                    escaped = "&quot;";
                    break;
                case '&' :
                    escaped = "&amp;";
                    break;
                case '\'' :
                    escaped = "&#39;";
                    break;
                case '<' :
                    escaped = "&lt;";
                    break;
                case '>' :
                    escaped = "&gt;";
                    break;
                default :
                    continue;
            }

            if (lastIndex != index)
            {
                html += str.Substring(lastIndex, index - lastIndex);
            }

            lastIndex =  index + 1;
            html      += escaped;
        }

        return lastIndex != index ? html + str.Substring(lastIndex, index - lastIndex) : html;
    }

    private static int IndexOfSpecialChar(string str)
    {
        for (var i = 0; i < str.Length; i++)
        {
            switch (str[i])
            {
                case '"' :
                case '&' :
                case '\'' :
                case '<' :
                case '>' :
                    return i;
            }
        }

        return -1;
    }
}
