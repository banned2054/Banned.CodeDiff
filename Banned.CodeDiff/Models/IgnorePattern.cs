using System.Text.RegularExpressions;

namespace Banned.CodeDiff.Models;

/// <summary>
///     文件名忽略规则:字符串按相等匹配,正则表达式按模式匹配。<br />
///     File-name ignore rules: exact string equality or regular-expression matching.
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
