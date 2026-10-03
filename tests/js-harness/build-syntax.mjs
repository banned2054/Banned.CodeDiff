// Bundles gen-syntax.ts (real shiki engine + upstream processAST) and runs it.
// Usage: node build-syntax.mjs   (env GDV_REPO overrides the JS repo location)
import { build } from "esbuild";
import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));

const gdvRepo = resolve(process.env.GDV_REPO ?? "C:/Code/JavaScript/git-diff-view");

console.log("git-diff-view repo:", gdvRepo);

// Rewrite the __GDV__ placeholder to the absolute JS repo path.
const srcPath = join(here, "gen-syntax.ts");
let src = readFileSync(srcPath, "utf8");
src = src.replaceAll("__GDV__", gdvRepo.replaceAll("\\", "/"));
writeFileSync(srcPath, src);

mkdirSync(join(here, "out"), { recursive: true });

await build({
  entryPoints: [srcPath],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: join(here, "out/gen-syntax.mjs"),
  logLevel: "warning",
  define: { __DEV__: "false" },
  // shiki + its wasm engine stay external and resolve from node_modules at
  // runtime (bundling the oniguruma wasm loader breaks it).
  external: ["shiki", "@shikijs/*"],
  nodePaths: [join(here, "node_modules")],
});

console.log("bundle ok, generating syntax-golden.json ...");

const stdout = execFileSync(process.execPath, [join(here, "out/gen-syntax.mjs")], {
  maxBuffer: 1024 * 1024 * 64,
  encoding: "utf8",
});

const outPath = join(here, "..", "Banned.CodeDiff.Tests", "Golden", "syntax-golden.json");

mkdirSync(join(here, "..", "Banned.CodeDiff.Tests", "Golden"), { recursive: true });

writeFileSync(outPath, stdout);

console.log("syntax-golden.json written:", outPath, `${(stdout.length / 1024).toFixed(0)} KB`);
