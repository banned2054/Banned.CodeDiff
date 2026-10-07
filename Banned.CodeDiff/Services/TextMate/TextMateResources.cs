using System.Text;
using System.Text.Json;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace Banned.CodeDiff.Services.TextMate;

/// <summary>
///     Loads the TextMate grammars and GitHub light/dark themes embedded from
///     @shikijs/langs / @shikijs/themes (see tests/js-harness/extract-textmate.mjs)
///     and builds the language table (lang id + aliases → scope name) the same way
///     the upstream shiki engine resolves languages. Also the IRegistryOptions
///     implementation feeding the TextMateSharp <see cref="Registry" />.
/// </summary>
internal sealed class TextMateResources : IRegistryOptions
{
    internal const string LightThemeName = "github-light";
    internal const string DarkThemeName  = "github-dark";

    // Keep in sync with tests/js-harness/extract-textmate.mjs.
    private const string GrammarResourcePrefix = "Banned.CodeDiff.Resources.TextMate.grammars.";

    private static readonly Lazy<TextMateResources> _instance = new(() => new TextMateResources());

    private readonly Dictionary<string, IRawGrammar> _grammars = new();

    // 语言别名首次注册生效,与 shiki 一致。
    private readonly Dictionary<string, string> _langToScope = new();

    private readonly Dictionary<string, ScopeThemeMatcher> _themes = new();

    private ScopeThemeMatcher? _darkTheme;

    private ScopeThemeMatcher? _lightTheme;

    private Registry? _registry;

    private TextMateResources()
    {
        var assembly = typeof(TextMateResources).Assembly;

        // 同时注册依赖语法,如 Vue 所需的 HTML。
        foreach (var resourceName in assembly.GetManifestResourceNames()
                                             .Where(n => n.StartsWith(GrammarResourcePrefix, StringComparison.Ordinal))
                                             .OrderBy(n => n, StringComparer.Ordinal))
        {
            var bytes = ReadResource(resourceName);

            IRawGrammar grammar;

            using (var stream = new MemoryStream(bytes))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                grammar = GrammarReader.ReadGrammarSync(reader);
            }

            var scopeName = grammar.GetScopeName();

            _grammars[scopeName] = grammar;

            using var meta = JsonDocument.Parse(bytes);

            var root = meta.RootElement;

            if (root.TryGetProperty("name", out var nameElement) &&
                nameElement.ValueKind == JsonValueKind.String)
                _langToScope.TryAdd(nameElement.GetString()!, scopeName);

            if (!root.TryGetProperty("aliases", out var aliasesElement) ||
                aliasesElement.ValueKind != JsonValueKind.Array) continue;
            foreach (var alias in aliasesElement.EnumerateArray()
                                                .Where(alias => alias.ValueKind == JsonValueKind.String))
                _langToScope.TryAdd(alias.GetString()!, scopeName);
        }

        foreach (var themeName in new[] { LightThemeName, DarkThemeName })
            _themes[themeName] =
                ScopeThemeMatcher
                   .FromThemeJson(ReadResource($"Banned.CodeDiff.Resources.TextMate.themes.{themeName}.json"));

        foreach (var themeName in new[] { "monokai", "vs-dark", "vs-light", "codex-dark" })
            _themes[themeName] =
                ScopeThemeMatcher
                   .FromThemeJson(ReadResource($"Banned.CodeDiff.Resources.TextMate.themes.{themeName}.json"));
    }

    /// <summary>进程级懒加载单例。<br />The lazily initialized process-wide singleton.</summary>
    public static TextMateResources Instance => _instance.Value;

    IRawGrammar IRegistryOptions.GetGrammar(string scopeName)
    {
        return _grammars.TryGetValue(scopeName, out var grammar) ? grammar : null!;
    }

    IRawTheme IRegistryOptions.GetTheme(string name)
    {
        return EmptyTheme.Instance;
    }

    IRawTheme IRegistryOptions.GetDefaultTheme()
    {
        return EmptyTheme.Instance;
    }

    ICollection<string> IRegistryOptions.GetInjections(string scopeName)
    {
        return Array.Empty<string>();
    }

    /// <summary>
    ///     把语言 id/别名(如 "cs"、"ts"、"vue")解析为 scope 名;语言未注册时返回
    ///     <c>null</c>(对应 shiki 的 getLanguage)。<br />
    ///     Resolves a language id / alias (e.g. "cs", "ts", "vue") to a scope name;
    ///     <c>null</c> when the language is not registered (mirrors shiki getLanguage).
    /// </summary>
    /// <param name="lang">语言 id 或别名,可为 <c>null</c>。The language id or alias; may be <c>null</c>.</param>
    /// <returns>scope 名;未注册时为 <c>null</c>。The scope name, or <c>null</c> when not registered.</returns>
    public string? ResolveScope(string? lang)
    {
        return lang != null && _langToScope.TryGetValue(lang, out var scope) ? scope : null;
    }

    /// <summary>
    ///     共享的 <see cref="Registry" />(单一 oniguruma 状态;设计上即为全局)。<br />The shared <see cref="Registry" /> (single
    ///     oniguruma state; global by design).
    /// </summary>
    public Registry GetRegistry()
    {
        return _registry ??= new Registry(this);
    }

    /// <summary>
    ///     返回明暗两套主题匹配器(github-light / github-dark),首次调用时初始化。<br />Returns the light and dark theme matchers (github-light
    ///     / github-dark), initialized on first call.
    /// </summary>
    public (ScopeThemeMatcher Light, ScopeThemeMatcher Dark) GetThemes()
    {
        if (_lightTheme != null && _darkTheme != null) return (_lightTheme, _darkTheme);
        _lightTheme = _themes[LightThemeName];
        _darkTheme  = _themes[DarkThemeName];

        return (_lightTheme, _darkTheme);
    }

    /// <summary>
    ///     返回共享只读的内置主题匹配器;未注册的名称返回 <c>null</c>。<br />
    ///     Returns a shared read-only bundled theme matcher, or null for an unregistered name.
    /// </summary>
    /// <param name="name">主题资源名(不含扩展名)。The theme resource name (without extension).</param>
    public ScopeThemeMatcher? GetSyntaxTheme(string name)
    {
        return _themes.GetValueOrDefault(name);
    }

    private static byte[] ReadResource(string resourceName)
    {
        var assembly = typeof(TextMateResources).Assembly;

        using var stream = assembly.GetManifestResourceStream(resourceName) ??
                           throw new InvalidOperationException($"Missing embedded TextMate resource: {resourceName}");

        using var memory = new MemoryStream();

        stream.CopyTo(memory);

        return memory.ToArray();
    }

    /// <summary>
    ///     The registry only uses the theme to build token metadata; token colors
    ///     are resolved by <see cref="ScopeThemeMatcher" /> instead, so an empty
    ///     theme suffices here.
    /// </summary>
    private sealed class EmptyTheme : IRawTheme
    {
        public static readonly EmptyTheme Instance = new();

        public string GetName()
        {
            return "empty";
        }

        public string GetInclude()
        {
            return "";
        }

        public ICollection<IRawThemeSetting> GetSettings()
        {
            return [];
        }

        public ICollection<IRawThemeSetting> GetTokenColors()
        {
            return [];
        }

        public ICollection<KeyValuePair<string, object>> GetGuiColors()
        {
            return [];
        }
    }
}
