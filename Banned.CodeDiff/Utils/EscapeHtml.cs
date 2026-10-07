namespace Banned.CodeDiff.Utils;

// Port of packages/core/src/escape-html.ts
// copy from https://github.com/vuejs/core/blob/main/packages/shared/src/escapeHtml.ts
/// <summary>
///     packages/core/src/escape-html.ts 的移植(源自 Vue escape-html):无状态纯函数,
///     转义 HTML 特殊字符,避免文本被当作 HTML 标记解析。<br />
///     Port of packages/core/src/escape-html.ts (from Vue escape-html): stateless pure
///     helpers that escape HTML special characters so text is not parsed as HTML markup.
/// </summary>
public static class EscapeHtml
{
    /// <summary>
    ///     转义 HTML 特殊字符 <c>&amp;&lt;&gt;"'</c>;<c>null</c> 视为空字符串。<br />
    ///     Escapes HTML characters <c>&amp;&lt;&gt;"'</c>; treats null as an empty string.
    /// </summary>
    /// <param name="input">待转义文本,可为 <c>null</c>。The text to escape; may be <c>null</c>.</param>
    /// <returns>转义后的文本。The escaped text.</returns>
    public static string Escape(string? input)
    {
        var str = input ?? "";

        var firstIndex = IndexOfSpecialChar(str);

        if (firstIndex < 0) return str;

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

            if (lastIndex != index) html += str.Substring(lastIndex, index - lastIndex);

            lastIndex =  index + 1;
            html      += escaped;
        }

        return lastIndex != index ? html + str.Substring(lastIndex, index - lastIndex) : html;
    }

    private static int IndexOfSpecialChar(string str)
    {
        for (var i = 0; i < str.Length; i++)
            switch (str[i])
            {
                case '"' :
                case '&' :
                case '\'' :
                case '<' :
                case '>' :
                    return i;
            }

        return -1;
    }
}
