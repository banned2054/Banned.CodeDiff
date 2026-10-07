using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Services;

/// <summary>packages/core/src/parse/transform.ts 的移植。<br />Port of packages/core/src/parse/transform.ts.</summary>
/// <remarks>
///     内部使用进程级全局状态，设置或重置转换函数会影响整个进程，禁止并发调用。<br />
///     Internally process-wide global state; setting or resetting the transform functions affects
///     the whole process and must not be done concurrently.
/// </remarks>
public static class Transform
{
    private static readonly Func<string, string> Default = f => f;

    private static Func<string, string> _transformContent = Default;

    private static Func<string, string> _transformFile = Default;

    /// <summary>检查当前是否启用了内容转换。<br />Checks whether content transformation is currently enabled.</summary>
    public static bool EnableTransform { get; private set; }

    /// <summary>
    ///     设置进程级内容转换函数;影响后续内容处理。<br />
    ///     Sets the process-wide content transform, affecting subsequent content processing.
    /// </summary>
    /// <param name="fn">内容转换函数，不能为 null。<br />The content transform function, must not be null.</param>
    public static void SetTransformForTemplateContent(Func<string, string> fn)
    {
        _transformContent = fn ?? throw new InvalidOperationException("Transform must be a function");

        EnableTransform = true;
    }

    /// <summary>
    ///     设置进程级文件转换函数。<br />
    ///     Sets the process-wide file transform.
    /// </summary>
    /// <param name="fn">文件转换函数，不能为 null。<br />The file transform function, must not be null.</param>
    /// <remarks>
    ///     同时清空语法缓存,使后续处理使用新转换。<br />
    ///     Also clears syntax caches so subsequent processing uses the new transform.
    /// </remarks>
    public static void SetTransformForFile(Func<string, string> fn)
    {
        _transformFile = fn ?? throw new InvalidOperationException("Transform must be a function");

        EnableTransform = true;

        // 旧语法结果已包含旧转换,更换转换时须清空缓存。
        SourceFile.ClearFileCache();
    }

    /// <summary>
    ///     将所有转换函数重置为默认状态并禁用转换。<br />Resets all transformation functions to their default state and disables
    ///     transformation.
    /// </summary>
    public static void ResetTransform()
    {
        EnableTransform = false;

        _transformContent = Default;

        _transformFile = Default;

        SourceFile.ClearFileCache();
    }

    /// <summary>
    ///     若转换已启用且已配置，则对内容应用内容转换函数，否则原样返回。<br />
    ///     Applies the transformation function to the provided content if transformation
    ///     is enabled and configured, otherwise returns the original content.
    /// </summary>
    /// <param name="content">待转换的内容。<br />The content to transform.</param>
    /// <returns>转换后的内容或原始内容。<br />The transformed content, or the original content.</returns>
    public static string ProcessTransformTemplateContent(string content)
    {
        if (EnableTransform && !ReferenceEquals(Default, _transformContent)) return _transformContent(content);

        return content;
    }

    /// <summary>
    ///     若转换已启用且已配置，则对内容应用文件转换函数，否则原样返回。<br />
    ///     Applies the file transformation function to the provided content if
    ///     transformation is enabled and configured, otherwise returns the original content.
    /// </summary>
    /// <param name="content">待转换的内容。<br />The content to transform.</param>
    /// <returns>转换后的内容或原始内容。<br />The transformed content, or the original content.</returns>
    public static string ProcessTransformForFile(string content)
    {
        if (EnableTransform && !ReferenceEquals(Default, _transformFile)) return _transformFile(content);

        return content;
    }
}
