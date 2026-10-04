using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Services;

/// <summary>Port of packages/core/src/parse/transform.ts.</summary>
public static class Transform
{
    private static readonly Func<string, string> Default = f => f;

    private static Func<string, string> _transformContent = Default;

    private static Func<string, string> _transformFile = Default;

    /// <summary>Checks whether content transformation is currently enabled.</summary>
    public static bool EnableTransform { get; private set; }

    /// <summary>
    ///     ⚠️ **WARNING: DANGEROUS OPERATION** ⚠️
    ///     This function modifies global state and may cause unexpected side effects.
    ///     You may also need escapeHTML for the content.
    /// </summary>
    public static void SetTransformForTemplateContent(Func<string, string> fn)
    {
        _transformContent = fn ?? throw new InvalidOperationException("Transform must be a function");

        EnableTransform = true;
    }

    /// <summary>
    ///     ⚠️ **WARNING: DANGEROUS OPERATION** ⚠️
    /// </summary>
    public static void SetTransformForFile(Func<string, string> fn)
    {
        _transformFile = fn ?? throw new InvalidOperationException("Transform must be a function");

        EnableTransform = true;

        // Cached source files carry the previously transformed raw — drop them so the next
        // construction picks up the new transform (fresh-instance behavior without the cache).
        SourceFile.ClearFileCache();
    }

    /// <summary>Resets all transformation functions to their default state and disables transformation.</summary>
    public static void ResetTransform()
    {
        EnableTransform = false;

        _transformContent = Default;

        _transformFile = Default;

        SourceFile.ClearFileCache();
    }

    /// <summary>
    ///     Applies the transformation function to the provided content if transformation
    ///     is enabled and configured, otherwise returns the original content.
    /// </summary>
    public static string ProcessTransformTemplateContent(string content)
    {
        if (EnableTransform && !ReferenceEquals(Default, _transformContent)) return _transformContent(content);

        return content;
    }

    /// <summary>
    ///     Applies the file transformation function to the provided content if
    ///     transformation is enabled and configured, otherwise returns the original content.
    /// </summary>
    public static string ProcessTransformForFile(string content)
    {
        if (EnableTransform && !ReferenceEquals(Default, _transformFile)) return _transformFile(content);

        return content;
    }
}
