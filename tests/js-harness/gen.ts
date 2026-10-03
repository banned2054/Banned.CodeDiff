// Golden-case generator: runs the ORIGINAL git-diff-view TS sources (plus the real
// fast-diff package) and emits JSON fixtures the C# port tests replay.
// C:/Code/JavaScript/git-diff-view / C:/Code/JavaScript/git-diff-view/ui placeholders are rewritten by build.mjs to absolute paths.
import fastDiff from "fast-diff";
import * as demo from "C:/Code/JavaScript/git-diff-view/ui/vue-example/src/data";

import { parseInstance, relativeChanges, diffChanges, getLang } from "C:/Code/JavaScript/git-diff-view/packages/core/src/parse/index";
import { DiffFile } from "C:/Code/JavaScript/git-diff-view/packages/core/src/diff-file";

// ---------------- helpers ----------------

/** undefined / NaN → dropped keys, stable output */
function norm(v: any): any {
  if (v === undefined) return undefined;
  if (typeof v === "number" && Number.isNaN(v)) return undefined;
  if (Array.isArray(v)) return v.map(norm);
  if (v && typeof v === "object") {
    const out: any = {};
    for (const k of Object.keys(v)) {
      const nv = norm(v[k]);
      if (nv !== undefined) out[k] = nv;
    }
    return out;
  }
  return v;
}

function dumpLineRange(r: any) {
  if (!r) return undefined;
  return {
    range: { location: r.range.location, length: r.range.length },
    hasLineChange: r.hasLineChange,
    newLineSymbol: r.newLineSymbol,
  };
}

function dumpDiffRange(r: any) {
  if (!r) return undefined;
  return {
    range: (r.range || []).map((i: any) => ({
      type: i.type,
      str: i.str,
      startIndex: i.startIndex,
      endIndex: i.endIndex,
      length: i.length,
    })),
    hasLineChange: r.hasLineChange,
    newLineSymbol: r.newLineSymbol,
  };
}

function dumpDiffLine(l: any): any {
  if (!l) return undefined;
  return {
    text: l.text,
    type: l.type,
    originalLineNumber: l.originalLineNumber,
    oldLineNumber: l.oldLineNumber,
    newLineNumber: l.newLineNumber,
    noTrailingNewLine: l.noTrailingNewLine ?? false,
    index: l.index,
    isFirst: l.isFirst,
    isLast: l.isLast,
    changes: dumpLineRange(l.changes),
    diffChanges: dumpDiffRange(l.diffChanges),
    internalDiffChanges: dumpDiffRange(l._diffChanges),
    prevHunkLineIndex: l.prevHunkLine ? l.prevHunkLine.index : undefined,
    hunkInfo: l.hunkInfo ? { ...l.hunkInfo } : undefined,
    splitInfo: l.splitInfo ? { ...l.splitInfo } : undefined,
    unifiedInfo: l.unifiedInfo ? { ...l.unifiedInfo } : undefined,
  };
}

function dumpRawDiff(rd: any) {
  return {
    header: rd.header,
    contents: rd.contents,
    isBinary: rd.isBinary,
    maxLineNumber: rd.maxLineNumber,
    hasHiddenBidiChars: rd.hasHiddenBidiChars,
    hunks: rd.hunks.map((h: any) => ({
      header: {
        oldStartLine: h.header.oldStartLine,
        oldLineCount: h.header.oldLineCount,
        newStartLine: h.header.newStartLine,
        newLineCount: h.header.newLineCount,
      },
      unifiedDiffStart: h.unifiedDiffStart,
      unifiedDiffEnd: h.unifiedDiffEnd,
      expansionType: h.expansionType,
      lines: h.lines.map((l: any) => dumpDiffLine(l)),
    })),
  };
}

function dumpSplitItem(it: any) {
  if (!it) return null;
  return {
    lineNumber: it.lineNumber,
    value: it.value,
    isHidden: it.isHidden ?? false,
    _isHidden: it._isHidden ?? false,
    diff: dumpDiffLine(it.diff),
  };
}

function dumpUnifiedItem(it: any) {
  if (!it) return null;
  return {
    oldLineNumber: it.oldLineNumber,
    newLineNumber: it.newLineNumber,
    value: it.value,
    isHidden: it.isHidden ?? false,
    _isHidden: it._isHidden ?? false,
    diff: dumpDiffLine(it.diff),
  };
}

function dumpModel(df: any) {
  const splitLeft: any[] = [];
  const splitRight: any[] = [];
  const unified: any[] = [];
  const splitHunks: any[] = [];
  const unifiedHunks: any[] = [];
  for (let i = 0; i < df.splitLineLength; i++) {
    splitLeft.push(dumpSplitItem(df.getSplitLeftLine(i)));
    splitRight.push(dumpSplitItem(df.getSplitRightLine(i)));
    const h = df.getSplitHunkLine(i);
    if (h) splitHunks.push({ index: i, hunk: dumpDiffLine(h) });
  }
  for (let i = 0; i < df.unifiedLineLength; i++) {
    unified.push(dumpUnifiedItem(df.getUnifiedLine(i)));
    const h = df.getUnifiedHunkLine(i);
    if (h) unifiedHunks.push({ index: i, hunk: dumpDiffLine(h) });
  }
  // reconstruct old/new diff line maps (lineNumber → line), sorted by line number
  const oldDiffLines: any[] = [];
  const newDiffLines: any[] = [];
  const oldSeen = new Map<number, any>();
  const newSeen = new Map<number, any>();
  for (const it of splitLeft) {
    if (it?.diff && it.diff.oldLineNumber != null) oldSeen.set(it.diff.oldLineNumber, it.diff);
    if (it?.diff && it.diff.newLineNumber != null) newSeen.set(it.diff.newLineNumber, it.diff);
  }
  for (const it of splitRight) {
    if (it?.diff && it.diff.oldLineNumber != null) oldSeen.set(it.diff.oldLineNumber, it.diff);
    if (it?.diff && it.diff.newLineNumber != null) newSeen.set(it.diff.newLineNumber, it.diff);
  }
  // NB: values in oldSeen/newSeen are already dumpDiffLine outputs — do not re-dump
  for (const [n, l] of [...oldSeen.entries()].sort((a, b) => a[0] - b[0])) oldDiffLines.push({ lineNumber: n, line: l });
  for (const [n, l] of [...newSeen.entries()].sort((a, b) => a[0] - b[0])) newDiffLines.push({ lineNumber: n, line: l });

  return {
    oldFileName: df._oldFileName,
    newFileName: df._newFileName,
    oldFileLang: df._oldFileLang,
    newFileLang: df._newFileLang,
    oldFileContent: df.getOldFileContent(),
    newFileContent: df.getNewFileContent(),
    diffLineLength: df.diffLineLength,
    splitLineLength: df.splitLineLength,
    unifiedLineLength: df.unifiedLineLength,
    fileLineLength: df.fileLineLength,
    additionLength: df.additionLength,
    deletionLength: df.deletionLength,
    hasSomeLineCollapsed: df.hasSomeLineCollapsed,
    expandEnabled: df.getExpandEnabled(),
    splitLeft,
    splitRight,
    unified,
    splitHunks,
    unifiedHunks,
    oldDiffLines,
    newDiffLines,
  };
}

// ---------------- 1. fastDiff cases ----------------

function mulberry32(a: number) {
  return function () {
    let t = (a += 0x6d2b79f5);
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const fastDiffCases: any[] = [];

function addFastDiffCase(a: string, b: string, cursor: any, cleanup: boolean) {
  let out: any;
  try {
    out = fastDiff(a, b, cursor, cleanup).map(([op, text]) => ({ op, text }));
  } catch (e: any) {
    // fast-diff has a known pathological-recursion case (RangeError: maximum call
    // stack); such inputs are excluded from the golden set and noted, because a
    // .NET StackOverflowException is process-fatal and cannot be matched bug-for-bug.
    fastDiffCases.push({ a, b, cursor, cleanup, out: null, pathological: true, error: String(e?.message ?? e) });
    return;
  }
  fastDiffCases.push({ a, b, cursor, cleanup, out });
}

// fixed cases
addFastDiffCase("", "", null, false);
addFastDiffCase("abc", "abc", null, false);
addFastDiffCase("abc", "", null, false);
addFastDiffCase("", "abc", null, false);
addFastDiffCase("Hello world.", "Goodbye world.", null, false);
addFastDiffCase("ABCABBA", "CBABAC", null, false);
addFastDiffCase("abc", "abd", null, true);
addFastDiffCase("The cat came.", "The came.", null, true);
addFastDiffCase("abcdef", "abXdXf", null, true);
addFastDiffCase("a\nb\nc", "a\rc\rc", null, true);
addFastDiffCase("😀𐐷a", "😀𐐷b", null, true);
addFastDiffCase("a😀b", "ab😀", null, true);
addFastDiffCase("中文测试", "中文试试", null, true);
addFastDiffCase("ab", "abc", 0, false);
addFastDiffCase("abc", "ab", 2, false);
addFastDiffCase("abc", "ab", 3, false);
addFastDiffCase("xxx", "xx", 1, true);
addFastDiffCase("one two three", "one  three", 4, true);
addFastDiffCase("keep this text", "keep that text", null, false);
addFastDiffCase("keep this text", "keep that text", null, true);

// fuzz
{
  const rnd = mulberry32(0x9e3779b9);
  // Atoms are BMP units or whole surrogate pairs, so generated strings never contain
  // lone surrogates (fast-diff infinite-recurses on those and System.Text.Json
  // refuses to decode them — excluded from coverage by construction).
  const atoms = ["a", "b", "c", " ", "\n", "\t", "中", "😀", "𐐷", "x", "y"];
  const randAtom = () => atoms[Math.floor(rnd() * atoms.length)];
  const randAtoms = (maxLen: number) => {
    const len = Math.floor(rnd() * maxLen);
    const arr: string[] = [];
    for (let i = 0; i < len; i++) arr.push(randAtom());
    return arr;
  };
  for (let i = 0; i < 420; i++) {
    const a = randAtoms(48);
    const b = a.slice();
    const ops = 1 + Math.floor(rnd() * 4);
    for (let j = 0; j < ops; j++) {
      const op = rnd();
      const pos = Math.floor(rnd() * (b.length + 1));
      const count = 1 + Math.floor(rnd() * 5);
      const ins = randAtoms(count);
      if (op < 0.35) b.splice(pos, 0, ...ins);
      else if (op < 0.7) b.splice(pos, count);
      else b.splice(pos, count, ...ins);
    }
    const useCursor = rnd() < 0.4;
    const cleanup = rnd() < 0.5;
    // cursor offsets are UTF-16 code-unit positions; keep them on atom boundaries
    const aStr = a.join("");
    const cuOffsets = [0];
    for (const atom of a) cuOffsets.push(cuOffsets[cuOffsets.length - 1] + atom.length);
    const cursor = useCursor ? cuOffsets[Math.floor(rnd() * cuOffsets.length)] : null;
    addFastDiffCase(aStr, b.join(""), cursor, cleanup);
  }
  // object-form cursor cases
  for (let i = 0; i < 60; i++) {
    const a = randAtoms(30);
    const b = a.slice();
    const pos = Math.floor(rnd() * (b.length + 1));
    b.splice(pos, 0, ...randAtoms(3));
    const aStr = a.join("");
    const cuOffsets = [0];
    for (const atom of a) cuOffsets.push(cuOffsets[cuOffsets.length - 1] + atom.length);
    const oldIndex = cuOffsets[Math.floor(rnd() * cuOffsets.length)];
    const oldLen = Math.floor(rnd() * 3);
    const cursor: any = { oldRange: { index: oldIndex, length: oldLen } };
    if (rnd() < 0.5) {
      const bCuOffsets = [0];
      for (const atom of b) bCuOffsets.push(bCuOffsets[bCuOffsets.length - 1] + atom.length);
      cursor.newRange = { index: bCuOffsets[Math.floor(rnd() * bCuOffsets.length)], length: 0 };
    }
    addFastDiffCase(aStr, b.join(""), cursor, rnd() < 0.5);
  }
}

// ---------------- 2. relativeChanges / diffChanges ----------------

type PairCase = {
  addition: { text: string; noTrailingNewLine: boolean };
  deletion: { text: string; noTrailingNewLine: boolean };
};

const linePairCases: PairCase[] = [];

function addPair(delText: string, addText: string, delNtl = false, addNtl = false) {
  linePairCases.push({
    addition: { text: addText, noTrailingNewLine: addNtl },
    deletion: { text: delText, noTrailingNewLine: delNtl },
  });
}

// handcrafted
addPair("const a = 1;\n", "const a = 2;\n");
addPair("same text\n", "same text\n");
addPair("same text\n", "same text\r\n");
addPair("same text\r\n", "same text\n");
addPair("same text", "same text\n", true, false);
addPair("same text\n", "same text", false, true);
addPair("same", "same", true, true);
addPair("", "abc\n");
addPair("abc\n", "");
addPair("   \n", "abc\n");
addPair("x", "y");
addPair("hello world foo", "hello brave foo");
addPair("hello world foo", "hello brave new foo");
addPair("a".repeat(999), "a".repeat(999) + "b");
addPair("a".repeat(1000), "a".repeat(1000) + "b"); // >= maxLengthToIgnoreLineDiff
addPair("a\nb\nc\n", "a\nX\nc\n");
addPair("line with trailing spaces   \n", "line with trailing spaces\t\n");
addPair("\n", "\r\n");
addPair("\n\n", "\n");
addPair("foo()", "bar()");

// auto-extracted from the demo diffs
const demoNames = Object.keys(demo);
const demoHunkStrings: string[] = [];
for (const name of demoNames) {
  const d: any = (demo as any)[name];
  for (const h of d.hunks ?? []) demoHunkStrings.push(h);
}

for (const hunkText of demoHunkStrings) {
  const rd = parseInstance.parse(hunkText);
  for (const hunk of rd.hunks) {
    let additions: any[] = [];
    let deletions: any[] = [];
    const flush = () => {
      if (additions.length === deletions.length && additions.length > 0) {
        for (let i = 0; i < additions.length; i++) {
          addPair(deletions[i].text, additions[i].text, deletions[i].noTrailingNewLine, additions[i].noTrailingNewLine);
        }
      }
      additions = [];
      deletions = [];
    };
    for (const line of hunk.lines) {
      if (line.type === 1) additions.push(line);
      else if (line.type === 2) deletions.push(line);
      else flush();
    }
    flush();
  }
  if (linePairCases.length > 220) break;
}

const linePairResults = linePairCases.map((c) => {
  const addition = { text: c.addition.text, type: 1, originalLineNumber: null, oldLineNumber: null, newLineNumber: 1, noTrailingNewLine: c.addition.noTrailingNewLine };
  const deletion = { text: c.deletion.text, type: 2, originalLineNumber: null, oldLineNumber: 1, newLineNumber: null, noTrailingNewLine: c.deletion.noTrailingNewLine };
  const rel = relativeChanges(addition as any, deletion as any);
  const fast = diffChanges(addition as any, deletion as any);
  return {
    input: c,
    relative: { addRange: dumpLineRange(rel.addRange), delRange: dumpLineRange(rel.delRange) },
    fastDiff: { addRange: dumpDiffRange(fast.addRange), delRange: dumpDiffRange(fast.delRange) },
  };
});

// ---------------- 3. parse cases ----------------

const parseCases: { name: string; text: string }[] = [
  { name: "empty", text: "" },
  { name: "header-only", text: "diff --git a/f.txt b/f.txt\nindex 123..456 100644\n--- a/f.txt\n+++ b/f.txt\n" },
  {
    name: "binary",
    text: "diff --git a/img.png b/img.png\nindex 123..456 100644\nBinary files a/img.png and b/img.png differ\n",
  },
  {
    name: "no-newline-both",
    text: "--- a/t.txt\n+++ b/t.txt\n@@ -1 +1 @@\n-old line\n\\ No newline at end of file\n+new line\n\\ No newline at end of file\n",
  },
  {
    name: "single-count-omitted",
    text: "--- a/t.txt\n+++ b/t.txt\n@@ -1 +1 @@\n-a\n+b\n",
  },
  {
    name: "empty-lines-41",
    text: "--- a/t.txt\n+++ b/t.txt\n@@ -1,3 +1,5 @@\n a\n\n\n+b\n\n c\n",
  },
  {
    name: "bidi-chars",
    text: "--- a/t.txt\n+++ b/t.txt\n@@ -1 +1 @@\n-a\u202Ab\n+b\u202Ec\n",
  },
  {
    name: "with-hunk-heading",
    text: "--- a/t.txt\n+++ b/t.txt\n@@ -1,4 +1,4 @@ function foo() {\n a\n-b\n+c\n d\n",
  },
  {
    name: "multi-hunk-gap",
    text:
      "--- a/t.txt\n+++ b/t.txt\n@@ -2,3 +2,3 @@\n a\n-b\n+B\n c\n@@ -60,3 +60,3 @@\n x\n-y\n+Y\n z\n",
  },
  {
    name: "crlf",
    text: "--- a/t.txt\r\n+++ b/t.txt\r\n@@ -1,2 +1,2 @@\r\n-a\r\n+A\r\n b\r\n",
  },
  {
    name: "no-trailing-newline-in-diff-text",
    text: "--- a/t.txt\n+++ b/t.txt\n@@ -1,2 +1,1 @@\n a\n-b\n+c",
  },
  {
    name: "expandable-top",
    text: "--- a/t.txt\n+++ b/t.txt\n@@ -5,3 +5,3 @@\n a\n-b\n+c\n d\n",
  },
  ...demoHunkStrings.slice(0, 8).map((t, i) => ({ name: `demo-hunk-${i}`, text: t })),
];

const parseResults = parseCases.map((c) => {
  let error: string | undefined;
  let out: any;
  try {
    out = dumpRawDiff(parseInstance.parse(c.text));
  } catch (e: any) {
    error = String(e && e.message ? e.message : e);
  }
  return { name: c.name, text: c.text, error, out };
});

// ---------------- 4. getLang cases ----------------

const langCases = ["a.ts", "noExt", "", ".gitignore", "a.b.c", "README", "x.JSON"].map((f) => ({
  input: f,
  output: getLang(f),
}));

// ---------------- 5. DiffFile pipeline (demo data) ----------------

const diffFileCases: any[] = [];

function runDiffFileCase(name: string, data: any, steps: any[] = []) {
  const df: any = DiffFile.createInstance(JSON.parse(JSON.stringify(data)));
  df.initRaw();
  df.buildSplitDiffLines();
  df.buildUnifiedDiffLines();
  const models: any[] = [{ step: "initial", model: dumpModel(df) }];
  for (const step of steps) {
    if (step.op === "split-expand") df.onSplitHunkExpand(step.dir, step.index, true);
    else if (step.op === "unified-expand") df.onUnifiedHunkExpand(step.dir, step.index, true);
    else if (step.op === "all-expand") df.onAllExpand(step.mode);
    else if (step.op === "all-collapse") df.onAllCollapse(step.mode);
    models.push({ step, model: dumpModel(df) });
  }
  diffFileCases.push({ name, models });
}

demoNames.forEach((name) => {
  runDiffFileCase(`demo-${name}`, (demo as any)[name]);
});

// handcrafted: expandable case with file contents (expansion enabled)
{
  const mkLine = (i: number, tag: string) => `line ${i} ${tag}\n`;
  const oldLines: string[] = [];
  const newLines: string[] = [];
  for (let i = 1; i <= 120; i++) {
    oldLines.push(mkLine(i, "old"));
    newLines.push(mkLine(i, "old"));
  }
  // change 1: lines 5-8
  for (let i = 5; i <= 8; i++) newLines[i - 1] = mkLine(i, "new");
  // change 2: lines 100-103
  for (let i = 100; i <= 103; i++) newLines[i - 1] = mkLine(i, "new2");
  const buildHunk = (oldStart: number, oldEnd: number, newStart: number) => {
    const oldCount = oldEnd - oldStart + 1;
    let body = "";
    let oc = 0;
    let nc = 0;
    for (let i = oldStart; i <= oldEnd; i++) {
      const o = oldLines[i - 1];
      const n = newLines[i - 1];
      if (o === n) {
        body += " " + o;
        oc++;
        nc++;
      } else {
        body += "-" + o;
        body += "+" + n;
        oc++;
        nc++;
      }
    }
    return `@@ -${oldStart},${oc} +${newStart},${nc} @@\n` + body;
  };
  const hunk1 = buildHunk(1, 12, 1);
  const hunk2 = buildHunk(96, 107, 96);
  const diffText = `--- a/big.txt\n+++ b/big.txt\n${hunk1}${hunk2}`;
  const data = {
    oldFile: { fileName: "big.txt", content: oldLines.join("") },
    newFile: { fileName: "big.txt", content: newLines.join("") },
    hunks: [diffText],
  };
  runDiffFileCase("expand-split", data, [
    { op: "split-expand", dir: "down", index: 0 },
    { op: "split-expand", dir: "down", index: 0 },
    { op: "split-expand", dir: "down", index: 0 },
    { op: "split-expand", dir: "all", index: 0 },
    { op: "split-expand", dir: "up-all", index: 0 },
    { op: "all-collapse", mode: "split" },
    { op: "all-expand", mode: "split" },
    { op: "all-collapse", mode: "split" },
  ]);
  runDiffFileCase("expand-unified", JSON.parse(JSON.stringify(data)), [
    { op: "unified-expand", dir: "down", index: 0 },
    { op: "unified-expand", dir: "down", index: 0 },
    { op: "unified-expand", dir: "down", index: 0 },
    { op: "unified-expand", dir: "all", index: 0 },
    { op: "unified-expand", dir: "up-all", index: 0 },
    { op: "all-collapse", mode: "unified" },
    { op: "all-expand", mode: "unified" },
    { op: "all-collapse", mode: "unified" },
  ]);
}

// handcrafted: single-line change "@@ -1 +1 @@" with contents
{
  const data = {
    oldFile: { fileName: "one.txt", content: "only line\n" },
    newFile: { fileName: "one.txt", content: "only line changed\n" },
    hunks: ["--- a/one.txt\n+++ b/one.txt\n@@ -1 +1 @@\n-only line\n+only line changed\n"],
  };
  runDiffFileCase("single-count-omitted", data, [
    { op: "split-expand", dir: "down", index: 0 },
    { op: "unified-expand", dir: "down", index: 0 },
  ]);
}

// ---------------- output ----------------

const golden = {
  meta: { generatedAt: "generated by tests/js-harness", fastDiffVersion: "1.3.0" },
  fastDiff: fastDiffCases,
  linePairs: linePairResults,
  parse: parseResults,
  lang: langCases,
  diffFile: diffFileCases,
};

process.stdout.write(JSON.stringify(norm(golden)));
