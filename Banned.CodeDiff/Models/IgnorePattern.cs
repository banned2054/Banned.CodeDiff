using System.Text.RegularExpressions;

namespace Banned.CodeDiff.Models;

/// <summary>
///     <c>DiffHighlighter.ignoreSyntaxHighlightList</c> 的 JS <c>(string | RegExp)[]</c> 忽略列表的
///     移植:每一项都与文件名匹配——字符串项按相等比较,RegExp 项用 <c>RegExp.test</c> 测试。建模为
///     小型判别联合,使两种匹配语义在消费侧保持显式。<br />
///     Port of the JS <c>(string | RegExp)[]</c> ignore list on
///     <c>DiffHighlighter.ignoreSyntaxHighlightList</c>: each entry is matched against
///     the file name — a string entry compares for equality, a RegExp entry is tested
///     with <c>RegExp.test</c>. Modeled as a small discriminated union so the two
///     match semantics stay explicit at the consumption site.
/// </summary>
public abstract record IgnorePattern;

/// <summary>JS 字符串项:与文件名完全相等时匹配。<br />JS: plain string entry — matches when exactly equal to the file name.</summary>
/// <param name="FileName">要匹配的文件名。<br />The file name to match.</param>
public sealed record FileNameIgnorePattern(string FileName) : IgnorePattern;

/// <summary>
///     JS RegExp 项:<c>Regex.IsMatch(fileName)</c> 命中时匹配。<br />JS: RegExp entry — matches when
///     <c>Regex.IsMatch(fileName)</c>.
/// </summary>
/// <param name="Regex">用于测试文件名的正则表达式。<br />The regex tested against the file name.</param>
public sealed record RegexIgnorePattern(Regex Regex) : IgnorePattern;
