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

// TextMateSharp has no support for begin/while rules (no `end`). The vue
// bundle uses two shapes:
// - negative while "^(?!X)" (continue while X does not match) → approximately
//   "end: (?=X)" (stop at X); used for embedded script blocks;
// - other while shapes (e.g. the /// comment continuation in
//   vue-sfc-style-variable-injection) have no faithful end equivalent and the
//   rule is dropped (the region falls through to the remaining rules).
function normalizeWhileRules(node) {
  if (Array.isArray(node)) {
    for (let i = node.length - 1; i >= 0; i--) {
      if (node[i] && typeof node[i] === "object") {
        const replacement = transformWhileRule(node[i]);
        if (replacement === null) {
          node.splice(i, 1);
        } else if (replacement !== undefined) {
          node[i] = replacement;
        }
      }
      normalizeWhileRules(node[i]);
    }
    return;
  }
  if (node && typeof node === "object") {
    for (const key of Object.keys(node)) {
      const value = node[key];
      if (value && typeof value === "object" && !Array.isArray(value) &&
          (key === "repository" || key === "patterns" || (value.begin !== undefined))) {
        const replacement = transformWhileRule(value);
        if (replacement !== undefined) {
          node[key] = replacement;
        }
      }
      normalizeWhileRules(value);
    }
  }
}

// Returns a replacement rule, null to drop the rule, or undefined to keep as-is.
function transformWhileRule(rule) {
  if (!(rule.begin !== undefined && rule.while !== undefined && rule.end === undefined)) {
    return undefined;
  }
  const m = /^\^\(\?\!(.+)\)$/.exec(rule.while);
  if (m) {
    rule.end = `(?=${m[1]})`;
    delete rule.while;
    return rule;
  }
  console.warn(`dropping unsupported while rule: ${rule.while} (${rule.name ?? "unnamed"})`);
  return {};
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
  normalizeWhileRules(grammar);

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
