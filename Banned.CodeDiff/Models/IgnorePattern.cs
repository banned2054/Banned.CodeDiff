using System.Text.RegularExpressions;

namespace Banned.CodeDiff.Models;

/// <summary>
///     Port of the JS <c>(string | RegExp)[]</c> ignore list on
///     <c>DiffHighlighter.ignoreSyntaxHighlightList</c>: each entry is matched against
///     the file name — a string entry compares for equality, a RegExp entry is tested
///     with <c>RegExp.test</c>. Modeled as a small discriminated union so the two
///     match semantics stay explicit at the consumption site.
/// </summary>
public abstract record IgnorePattern;

/// <summary>JS: plain string entry — matches when exactly equal to the file name.</summary>
public sealed record FileNameIgnorePattern(string FileName) : IgnorePattern;

/// <summary>JS: RegExp entry — matches when <c>Regex.IsMatch(fileName)</c>.</summary>
public sealed record RegexIgnorePattern(Regex Regex) : IgnorePattern;
