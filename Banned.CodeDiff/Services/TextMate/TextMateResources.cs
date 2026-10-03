using System.Reflection;
using System.Text;
using System.Text.Json;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;

namespace Banned.CodeDiff.Services.TextMate;

/// <summary>
/// Loads the TextMate grammars and GitHub light/dark themes embedded from
/// @shikijs/langs / @shikijs/themes (see tests/js-harness/extract-textmate.mjs)
/// and builds the language table (lang id + aliases → scope name) the same way
/// the upstream shiki engine resolves languages. Also the IRegistryOptions
/// implementation feeding the TextMateSharp <see cref="Registry"/>.
/// </summary>
internal sealed class TextMateResources : IRegistryOptions
{
    internal const string LightThemeName = "github-light";
    internal const string DarkThemeName  = "github-dark";

    // Keep in sync with tests/js-harness/extract-textmate.mjs.
    private const string GrammarResourcePrefix = "Banned.CodeDiff.Resources.TextMate.grammars.";

    private static readonly Lazy<TextMateResources> _instance = new(() => new TextMateResources());

    public static TextMateResources Instance => _instance.Value;

    private readonly Dictionary<string, IRawGrammar> _grammars = new();

    // lang id / alias ("csharp", "cs", "c#", ...) → scope name; first registration
    // wins, matching the shiki bundle's unambiguous language table.
    private readonly Dictionary<string, string> _langToScope = new();

    private readonly Dictionary<string, ScopeThemeMatcher> _themes = new();

    private Registry? _registry;

    private ScopeThemeMatcher? _lightTheme;

    private ScopeThemeMatcher? _darkTheme;

    private TextMateResources()
    {
        var assembly = typeof(TextMateResources).Assembly;

        // Every embedded grammar registers, including dependency grammars pulled
        // in by the shiki bundles (e.g. vue needs html-derivative, vue-directives).
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
            {
                _langToScope.TryAdd(nameElement.GetString()!, scopeName);
            }

            if (root.TryGetProperty("aliases", out var aliasesElement) &&
                aliasesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var alias in aliasesElement.EnumerateArray())
                {
                    if (alias.ValueKind == JsonValueKind.String)
                    {
                        _langToScope.TryAdd(alias.GetString()!, scopeName);
                    }
                }
            }
        }

        foreach (var themeName in new[] { LightThemeName, DarkThemeName })
        {
            _themes[themeName] =
                ScopeThemeMatcher.FromThemeJson(ReadResource($"Banned.CodeDiff.Resources.TextMate.themes.{themeName}.json"));
        }
    }

    /// <summary>Resolves a language id / alias (e.g. "cs", "ts", "vue") to a scope name;
    /// <c>null</c> when the language is not registered (mirrors shiki getLanguage).</summary>
    public string? ResolveScope(string? lang)
    {
        return lang != null && _langToScope.TryGetValue(lang, out var scope) ? scope : null;
    }

    /// <summary>The shared <see cref="Registry"/> (single oniguruma state; global by design).</summary>
    public Registry GetRegistry()
    {
        return _registry ??= new Registry(this);
    }

    public (ScopeThemeMatcher Light, ScopeThemeMatcher Dark) GetThemes()
    {
        if (_lightTheme == null || _darkTheme == null)
        {
            _lightTheme = _themes[LightThemeName];
            _darkTheme  = _themes[DarkThemeName];
        }

        return (_lightTheme, _darkTheme);
    }

    IRawGrammar IRegistryOptions.GetGrammar(string scopeName)
    {
        return _grammars.TryGetValue(scopeName, out var grammar) ? grammar : null!;
    }

    TextMateSharp.Themes.IRawTheme IRegistryOptions.GetTheme(string name)
    {
        return EmptyTheme.Instance;
    }

    TextMateSharp.Themes.IRawTheme IRegistryOptions.GetDefaultTheme()
    {
        return EmptyTheme.Instance;
    }

    /// <summary>
    /// The registry only uses the theme to build token metadata; token colors
    /// are resolved by <see cref="ScopeThemeMatcher"/> instead, so an empty
    /// theme suffices here.
    /// </summary>
    private sealed class EmptyTheme : TextMateSharp.Themes.IRawTheme
    {
        public static readonly EmptyTheme Instance = new();

        public string GetName() => "empty";

        public string GetInclude() => "";

        public ICollection<TextMateSharp.Themes.IRawThemeSetting> GetSettings() => [];

        public ICollection<TextMateSharp.Themes.IRawThemeSetting> GetTokenColors() => [];

        public ICollection<KeyValuePair<string, object>> GetGuiColors() => [];
    }

    ICollection<string> IRegistryOptions.GetInjections(string scopeName)
    {
        return Array.Empty<string>();
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
}
