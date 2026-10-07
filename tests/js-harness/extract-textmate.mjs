// Extracts TextMate grammar JSON files (@shikijs/langs) and the GitHub light/dark
// theme JSON files (@shikijs/themes) into the C# core library for embedding.
// Same grammar sources as the upstream shiki engine, so the C# TextMate port and
// the JS golden fixtures tokenize identical inputs. Dev-only; not shipped.
//
// Bundled lang modules export [embedded-dep grammars..., main grammar]; every
// entry is extracted (shiki loads them all — e.g. vue pulls in html-derivative,
// vue-directives, ...). Each grammar is written as <name>.json.
import { mkdirSync, writeFileSync } from "node:fs";
import { fileURLToPath, pathToFileURL } from "node:url";

const langs = [
  "csharp", "typescript", "tsx", "javascript", "jsx", "json", "html", "css",
  "markdown", "python", "java", "go", "rust", "c", "cpp", "shellscript",
  "yaml", "xml", "vue", "diff",
];

const themes = ["github-light", "github-dark"];

const grammarsDir = new URL("../../Banned.CodeDiff/Resources/TextMate/grammars/", import.meta.url);
const themesDir = new URL("../../Banned.CodeDiff/Resources/TextMate/themes/", import.meta.url);

mkdirSync(fileURLToPath(grammarsDir), { recursive: true });
mkdirSync(fileURLToPath(themesDir), { recursive: true });

// TextMateSharp's JSON parser requires capture entries to be objects; some
// grammars (e.g. vue) use the string shorthand vscode-textmate also accepts.
// Normalize {"1": "scope.name"} → {"1": {"name": "scope.name"}} — regexes and
// scopes are untouched.
function normalizeCaptureShorthand(node) {
  if (Array.isArray(node)) {
    node.forEach(normalizeCaptureShorthand);
    return;
  }
  if (node && typeof node === "object") {
    for (const key of Object.keys(node)) {
      const value = node[key];
      if ((key === "captures" || key === "beginCaptures" || key === "endCaptures") &&
          value && typeof value === "object" && !Array.isArray(value)) {
        for (const capture of Object.keys(value)) {
          if (typeof value[capture] === "string") {
            value[capture] = { name: value[capture] };
          }
        }
      }
      normalizeCaptureShorthand(value);
    }
  }
}

// Begin/while rules are kept verbatim: TextMateSharp 2.x natively supports them
// (its Rule model compiles `while` / `whileCaptures` like vscode-textmate's
// BeginWhileRule). Dropping or approximating them broke whole rule families —
// e.g. the C# `//` and `///` comment rules are begin/while rules, and replacing
// them left comments untokenized.

// vscode-textmate tolerates rules with `begin` but neither `end` nor `while`:
// the compiled rule simply has no end pattern and stays on the state stack
// (observed with real shiki: every following line stays inside the rule's
// scope). TextMateSharp instead compiles a BeginEndRule with a null end regex
// and throws ArgumentNullException at compile time. Normalize those rules to an
// end that can never match — the observable token stream is the same.
function normalizeBeginOnlyRules(node) {
  if (Array.isArray(node)) {
    node.forEach(normalizeBeginOnlyRules);
    return;
  }
  if (node && typeof node === "object") {
    if (node.begin !== undefined && node.end === undefined && node.while === undefined) {
      node.end = "(?!)";
    }
    for (const value of Object.values(node)) {
      normalizeBeginOnlyRules(value);
    }
  }
}

const byName = new Map();

for (const id of langs) {
  const mod = await import(
    pathToFileURL(fileURLToPath(new URL(`node_modules/@shikijs/langs/dist/${id}.mjs`, import.meta.url))).href
  );

  for (const grammar of mod.default) {
    if (!grammar || !grammar.scopeName) {
      throw new Error(`unexpected grammar export for ${id}`);
    }

    byName.set(grammar.name ?? id, grammar);
  }

  console.log(`lang ${id}: ${mod.default.length} grammar(s), main -> ${mod.default[mod.default.length - 1].scopeName}`);
}

for (const [name, grammar] of byName) {
  normalizeCaptureShorthand(grammar);
  normalizeBeginOnlyRules(grammar);

  writeFileSync(
    fileURLToPath(new URL(`${name}.json`, grammarsDir)),
    JSON.stringify(grammar)
  );

  console.log(`grammar ${name} -> ${grammar.scopeName}`);
}

for (const id of themes) {
  const mod = await import(
    pathToFileURL(fileURLToPath(new URL(`node_modules/@shikijs/themes/dist/${id}.mjs`, import.meta.url))).href
  );

  const theme = mod.default;

  if (!theme || !theme.tokenColors) {
    throw new Error(`unexpected theme export for ${id}`);
  }

  writeFileSync(
    fileURLToPath(new URL(`${id}.json`, themesDir)),
    JSON.stringify(theme)
  );

  console.log(`theme ${id} (${theme.type}) rules=${theme.tokenColors.length}`);
}
