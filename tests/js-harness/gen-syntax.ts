// Syntax-highlight golden generator: runs the REAL shiki engine with the same
// codeToHast parameters as @git-diff-view/shiki's getAST, plus the REAL
// processAST from the upstream utils package, and dumps the per-line span
// structures for the C# port (TextMateHighlighter) to replay field-by-field.
// C:/Code/JavaScript/git-diff-view is rewritten by build-syntax.mjs.
import { createHighlighter } from "shiki";
import { processAST } from "C:/Code/JavaScript/git-diff-view/packages/utils/src/highlightAST";

const cases = [
  {
    name: "csharp",
    lang: "cs",
    fileName: "calculator.cs",
    raw: [
      "using System;",
      "using System.Collections.Generic;",
      "",
      "/* a block comment",
      "   spanning multiple lines */",
      "namespace Demo",
      "{",
      "    public sealed class Calculator",
      "    {",
      "        private readonly Dictionary<string, int> _counts = new();",
      "",
      "        public int Add(int a, int b) => a + b;",
      "",
      "        public string Describe(string name) => $\"{name}: {1 + 2}\";",
      "    }",
      "}",
    ].join("\n") + "\n",
  },
  {
    name: "typescript",
    lang: "ts",
    fileName: "store.ts",
    raw: [
      "export interface Item {",
      "  id: number;",
      "  name: string;",
      "  tags: string[];",
      "}",
      "",
      "const regex = /^[a-z]+\\d{2}$/;",
      "",
      "export function format(items: Item[]): string {",
      "  const summary = items",
      "    .filter((i) => i.tags.length > 0)",
      "    .map((i) => `${i.id}: ${i.name}`)",
      "    .join(\", \");",
      "  return `total ${items.length}",
      "summary ${summary}`;",
      "}",
    ].join("\n") + "\n",
  },
  {
    name: "json",
    lang: "json",
    fileName: "settings.json",
    raw: [
      "{",
      "  \"name\": \"demo\",",
      "  \"version\": \"1.0.0\",",
      "  \"count\": 42,",
      "  \"ratio\": 0.75,",
      "  \"enabled\": true,",
      "  \"nested\": { \"list\": [1, 2, 3], \"escaped\": \"quote: \\\" inside\" },",
      "  \"empty\": null",
      "}",
    ].join("\n") + "\n",
  },
  {
    name: "vue",
    lang: "vue",
    fileName: "panel.vue",
    raw: [
      "<template>",
      "  <div class=\"panel\">",
      "    <h1>{{ title }}</h1>",
      "    <p v-if=\"visible\">hello</p>",
      "  </div>",
      "</template>",
      "",
      "<script setup lang=\"ts\">",
      "import { ref } from \"vue\";",
      "const title = ref<string>(\"panel\");",
      "const visible = ref(true);",
      "</script>",
      "",
      "<style scoped>",
      ".panel { color: #333; }",
      "</style>",
    ].join("\n") + "\n",
  },
];

const highlighter = await createHighlighter({
  themes: ["github-light", "github-dark"],
  // lang ids here match the language table the engine registers; the per-case
  // `lang` uses the file-extension aliases getLang produces ("cs", "ts", ...).
  langs: ["csharp", "typescript", "json", "vue"],
});

const out = { cases: [] as any[] };

for (const c of cases) {
  // Same options as @git-diff-view/shiki getAST.
  const ast = highlighter.codeToHast(c.raw, {
    lang: c.lang,
    themes: { dark: "github-dark", light: "github-light" },
    cssVariablePrefix: "--diff-view-",
    defaultColor: false,
    mergeWhitespaces: false,
  });

  const { syntaxFileObject, syntaxFileLineNumber } = processAST(ast);

  out.cases.push({
    name: c.name,
    lang: c.lang,
    fileName: c.fileName,
    raw: c.raw,
    syntaxFileLineNumber,
    lines: Object.values(syntaxFileObject).map((l: any) => ({
      lineNumber: l.lineNumber,
      value: l.value,
      valueLength: l.valueLength,
      nodeList: l.nodeList.map((s: any) => ({
        startIndex: s.node.startIndex,
        endIndex: s.node.endIndex,
        value: s.node.value,
        style: s.wrapper?.properties?.style ?? null,
      })),
    })),
  });

  console.error(`case ${c.name}: ${syntaxFileLineNumber} lines, ` +
    `${Object.values(syntaxFileObject).reduce((n: number, l: any) => n + l.nodeList.length, 0)} spans`);
}

console.log(JSON.stringify(out));
