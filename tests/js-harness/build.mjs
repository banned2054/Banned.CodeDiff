// Bundles gen.ts against the real git-diff-view TS sources and runs it.
// Usage: node build.mjs   (env GDV_REPO overrides the JS repo location)
import { build } from "esbuild";
import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));

const gdvRepo = resolve(process.env.GDV_REPO ?? "C:/Code/JavaScript/git-diff-view");

console.log("git-diff-view repo:", gdvRepo);

// Rewrite the __GDV_REPO__ placeholders in the shims and gen.ts to absolute POSIX paths.
const rewrite = (rel, map) => {
  const p = join(here, rel);
  let src = readFileSync(p, "utf8");
  for (const [k, v] of Object.entries(map)) {
    src = src.replaceAll(k, v.replaceAll("\\", "/"));
  }
  writeFileSync(p, src);
};

rewrite("shims/utils.ts", { __GDV_REPO__: gdvRepo });
rewrite("gen.ts", { __GDV__: gdvRepo, __GDV_UI__: join(gdvRepo, "ui") });

mkdirSync(join(here, "out"), { recursive: true });

await build({
  entryPoints: [join(here, "gen.ts")],
  bundle: true,
  platform: "node",
  format: "cjs",
  outfile: join(here, "out/gen.cjs"),
  logLevel: "warning",
  define: { __DEV__: "false", __VERSION__: "\"0.1.7\"" },
  // fast-diff is imported from inside the git-diff-view sources, which have no
  // node_modules of their own; point resolution at the harness node_modules.
  nodePaths: [join(here, "node_modules")],
  alias: {
    "@git-diff-view/utils": join(here, "shims/utils.ts"),
    "@git-diff-view/lowlight": join(here, "shims/lowlight.ts"),
  },
});

console.log("bundle ok, generating golden.json ...");

const stdout = execFileSync(process.execPath, [join(here, "out/gen.cjs")], {
  maxBuffer: 1024 * 1024 * 64,
  encoding: "utf8",
});

const outPath = join(here, "..", "Banned.CodeDiff.Tests", "Golden", "golden.json");

mkdirSync(dirname(outPath), { recursive: true });

writeFileSync(outPath, stdout);

console.log("golden.json written:", outPath, `${(stdout.length / 1024).toFixed(0)} KB`);
