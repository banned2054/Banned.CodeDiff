# Changelog

## Unreleased

### M5 — Syntax Highlighting

- **`Banned.CodeDiff`**: syntax highlighting state ported from `file.ts` / `diff-file.ts` —
  `SourceFile.DoSyntax` (max-line threshold, language check, idempotence), `DiffFile.InitSyntax`
  (with optional injected engine), and `GetOldSyntaxLine` / `GetNewSyntaxLine`. `Init()` now runs
  both `InitRaw` and `InitSyntax`, matching the upstream `init()`.
- Built-in `TextMateHighlighter` engine: TextMateSharp 2.x tokenization carrying the rule stack
  across lines (block comments / template literals keep their state across collapsed hunks),
  producing shiki-shaped wrapper styles with both theme colors
  (`--diff-view-dark:#…;--diff-view-light:#…`). Theme matching is a straight port of the
  vscode-textmate matcher (`ScopeThemeMatcher`); TextMateSharp's own `Theme.Match` mis-resolves
  descendant selectors on scope stacks, and is not used.
- Grammars and themes embed the shiki-bundled sources (@shikijs/langs / @shikijs/themes,
  20 languages plus their dependency grammars, github-light/dark). Files over 2000 raw lines or
  in unregistered languages render plain, mirroring the upstream guard.
- Golden tests replay the REAL shiki engine (same `codeToHast` options as `@git-diff-view/shiki`)
  plus the upstream `processAST` against the C# engine, field-by-field, for C# / TypeScript /
  JSON / Vue samples. Three lines carry known TextMateSharp↔vscode-oniguruma engine divergences
  (C# interpolation capture boundaries; the CSS class-selector misjudgment inside `<style>`),
  explicitly pinned and relaxed in `SyntaxGoldenTests` — the port cannot correct those inside
  the embedded grammars.
- Upstream behavior difference (intentional): when the injected highlighter does not know the
  language, the JS falls back to lowlight (highlight.js); the C# port has no lowlight port, so
  such files simply stay unhighlighted.
- **`Banned.CodeDiff.Avalonia`**: `DiffView.SyntaxHighlight` (default on) drives `InitSyntax`;
  `DiffView.Highlighter` injects a custom engine. Content rows carry syntax runs resolved from
  the wrapper styles per theme variant (dark/light picked at row-build time, theme switches
  rebuild rows without re-tokenizing), rendered as per-run foregrounds on top of the existing
  word-level highlight rectangles. Lines with more than 150 spans degrade to plain text
  (upstream render guard).
- Demo: syntax toggle, a paste-mode filename extraction from the diff headers (so pasted
  `.cs` diffs colorize), and a C#/TypeScript/JSON sample cycler.
- NOTICE updated for TextMateSharp, the vscode-textmate theme-matching port, and the embedded
  shiki grammars/themes.

### Tests

- Migrated both test suites from xunit to NUnit 5.0.0 (constraint-model asserts,
  `NUnit3TestAdapter`/VSTest execution). Parallelization is disabled via
  `[assembly: Parallelizable(ParallelScope.None)]` because the core library holds global state;
  the Avalonia suite uses `Avalonia.Headless.NUnit` with `[AvaloniaTest]`. All 577 core cases and
  11 UI cases pass unchanged.

### Banned.CodeDiff

- Initial implementation of the core logic library, ported from
  [`@git-diff-view/core`](https://github.com/MrWangJustToDo/git-diff-view) and `fast-diff@1.3.0`:
  - Unified diff parsing (`RawDiff`, multi-file / multi-hunk, CRLF, no-newline markers, binary
    markers, bidi detection)
  - Split / unified line models with expandable hunks (up / down / all, configurable step,
    `Updated` notification)
  - Word-level change ranges from two algorithms (relative-changes and fast-diff), behind the
    `TemplateOptions` global switches
  - Language detection via `DiffTool.GetLang`
- 577 unit and golden tests comparing field-by-field against the JS original.

### Banned.CodeDiff.Avalonia

- Initial implementation (M2): the `DiffView` control renders a read-only, GitHub-style split view
  from a `DiffFile` — line-level add/delete/context backgrounds, collapsed hunk placeholder rows,
  a monospace default font (`Menlo, Consolas, monospace`, 14px), auto-sized line-number columns,
  and light/dark palettes mirroring the upstream color variables. Assigning the model invokes
  `Init`/`BuildSplitDiffLines` on demand and the view refreshes through the model's `Updated` event.
- Demo app (`Banned.CodeDiff.Avalonia.Demo`): paste a unified diff text and render it, with a
  built-in sample, light/dark theme toggle, and add/delete statistics.
- Word-level highlight rendering (M3): the changed ranges inside paired add/delete lines are
  highlighted with nested background blocks (`DiffSegmentText`, a custom-drawn text control based
  on `TextLayout.HitTestTextPosition` — Avalonia text runs expose no per-run background). Fast-diff
  segments are preferred with the relative-changes single range as fallback; both view modes and
  both palettes are covered, and the demo gains a highlight toggle.
- Unified view mode: `DiffView.ViewMode` switches between split (default) and unified rendering —
  single column with dual (old/new) line-number columns, deleted lines above the added ones,
  matching the GitHub unified layout. The demo gains a mode toggle and dual-model statistics.
- Hunk expand/collapse UI (M4): hunk placeholder rows now carry expand buttons wired to the
  model's expand API through `DiffView.ExpandHunkUpCommand` / `ExpandHunkDownCommand` /
  `ExpandHunkAllCommand`. Button placement mirrors the upstream git-diff-view components:
  a single Expand Up on the first hunk, a single Expand Down on the trailing collapse strip,
  a single Expand All when fewer than 40 hidden lines remain, and a stacked down+up pair
  otherwise. The synthetic trailing hunk (previously not rendered) now shows as the bottom
  expand strip. Expansion requires a model built with real old/new file contents; paste-only
  diffs render hunk rows without buttons.
- Row virtualization (M4): the row list renders through a `VirtualizingStackPanel`; a 10k-line
  model (785 visible rows) realizes only ~25 containers in an 800×600 viewport.
- Behavior alignment (M4): a hunk placeholder row is only rendered while it still hides lines
  (`startHiddenIndex < endHiddenIndex`), matching GitHub and the upstream view components —
  in particular the leading `@@` header of a hunk starting at line 1 is no longer shown, and
  fully expanded placeholders disappear. Raw gap lines revealed by expansion (no `DiffLine`,
  but a line number and file text) now render as plain context rows instead of empty cells.
- Demo: new "load expandable sample" action (a synthetic multi-hunk file with real contents
  exercising every button shape) and expand-all / collapse-all toolbar buttons.
- Hosts include the control theme explicitly — Avalonia does not auto-discover control-library
  themes: `<StyleInclude Source="avares://Banned.CodeDiff.Avalonia/Themes/Generic.axaml" />`.
- New headless UI test suite (`tests/Banned.CodeDiff.Avalonia.Tests`, now NUnit +
  `Avalonia.Headless.NUnit`): asserts the theme loads through the consumer-style include, the
  template instantiates, row models build correctly in both split and unified modes, mode
  switching rebuilds rows, and palette colors match the upstream values. Grown to 22 cases with
  M4 (expand directions, button placement, command wiring, virtualization, 10k-line performance
  baseline); the test app now also loads the Fluent theme like a real consumer — previously
  only the library theme was loaded, so `ItemsControl`/`Button` never received themes and row
  containers were never instantiated.
