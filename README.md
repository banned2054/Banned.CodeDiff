# Banned.CodeDiff

English | [**简体中文**](https://github.com/banned2054/Banned.CodeDiff/blob/master/Docs/README.zh-CN.md)

[![NuGet](https://img.shields.io/nuget/v/Banned.CodeDiff.Avalonia.svg)](https://www.nuget.org/packages/Banned.CodeDiff.Avalonia) [![Downloads](https://img.shields.io/nuget/dt/Banned.CodeDiff.Avalonia.svg)](https://www.nuget.org/packages/Banned.CodeDiff.Avalonia) [![License](https://img.shields.io/badge/license-Apache_2.0-green)](./LICENSE)

An Avalonia control library for rendering code diffs: GitHub-style split and unified views with
line- and word-level highlighting, expandable hunks, and syntax highlighting.

The host application owns the diff text and the surrounding UI. This repository handles diff parsing,
split/unified line pairing, word-level change ranges, hunk expand/collapse state, and rendering.
The core logic library has no UI dependency, so it can also drive WPF, console, or any other .NET host.

The project is a C# port of [`git-diff-view`](https://github.com/MrWangJustToDo/git-diff-view); see
[NOTICE](./NOTICE) for upstream acknowledgements.

## Status

Development follows the milestones in [plan.md](./plan.md):

| Milestone | Scope | State |
|---|---|---|
| M1 | Core logic library `Banned.CodeDiff` | Done — 577 tests green, golden-tested against the JS original |
| M2 | `Banned.CodeDiff.Avalonia` minimal split view | Done — `DiffView` control + demo |
| M3 | Word-level highlight rendering | Done |
| M4 | Hunk expand/collapse + virtualization | Done |
| M5 | Syntax highlighting (TextMateSharp) | Done — golden-tested against shiki |
| M6 | Themes, wrap mode, copy | Planned |

## Features

- Unified diff parsing — GNU unified diff text into a structured `RawDiff` (multi-file, multi-hunk,
  CRLF support, `\ No newline at end of file`, binary markers, bidi hidden-character detection)
- Split / unified line models — left/right paired rows and single-column rows with line numbers,
  add/delete/modify types, and collapsed placeholders, ready to feed a list control
- Word-level changes — character-level ranges for paired added/removed lines, from two algorithms:
  relative-changes (`DiffLine.Changes`) and fast-diff (`DiffLine.DiffChanges`)
- Hunk expansion — up / down / all expansion state machine with configurable step (default 40 lines)
  and an `Updated` notification the host uses to refresh the view
- Global template switches — `TemplateOptions` toggles fast-diff word-level diff and template building
- Syntax highlighting — a built-in TextMate engine (TextMateSharp + the shiki-bundled grammars and GitHub
  light/dark themes) tokenizes the full old/new files and exposes per-line colored spans
  (`DiffFile.InitSyntax` / `GetOldSyntaxLine` / `GetNewSyntaxLine`); pluggable via `IDiffHighlighter`
- Zero UI dependencies in the core library
- `DiffView` Avalonia control — read-only GitHub-style diff view with line-level add/delete
  backgrounds, word-level highlight blocks inside changed lines, collapsed hunk placeholders with
  clickable expand affordances (expand up / down / all, matching GitHub's placement rules), row
  virtualization for large diffs, and light/dark palettes; renders split (two columns) or unified
  (single column, dual line numbers, deleted lines above the added ones)

## Installation

```powershell
dotnet add package Banned.CodeDiff.Avalonia
```

> The control package is not published yet. Until then, consume the libraries via project references
> (`Banned.CodeDiff.Avalonia` brings the core library with it). Whether `Banned.CodeDiff` also ships
> as a standalone NuGet package has not been decided yet.

## Avalonia Control

`DiffView` renders a `DiffFile` as a read-only split view. Include the control theme once in your
application (Avalonia does not auto-discover control-library themes), then place the control:

```xml
<Application xmlns="https://github.com/avaloniaui">
    <Application.Styles>
        <FluentTheme />
        <StyleInclude Source="avares://Banned.CodeDiff.Avalonia/Themes/Generic.axaml" />
    </Application.Styles>
</Application>
```

```xml
<Window xmlns:views="clr-namespace:Banned.CodeDiff.Avalonia.Views;assembly=Banned.CodeDiff.Avalonia">
    <views:DiffView DiffFile="{Binding DiffFile}" />
</Window>
```

Assign a `DiffFile` (built or not — the control invokes `Init` / `Build*DiffLines` on demand) and
it stays in sync through the model's `Updated` event. `ViewMode` switches between the default
`Split` and `Unified` rendering; `SyntaxHighlight` (default on) runs `InitSyntax` so lines render
with syntax colors that follow the light/dark theme, and `Highlighter` injects a custom
`IDiffHighlighter` engine. The control uses a monospace font by default, auto-sizes the
line-number columns, renders collapsed hunk placeholder rows, switches its light/dark palette
with `ActualThemeVariant`, and paints word-level highlight blocks inside changed lines when
`DiffLine.DiffChanges` (fast-diff) or `DiffLine.Changes` (relative) ranges are available.

Hunk placeholder rows carry expand buttons (single Expand Up on the first hunk, Expand Down on
the trailing strip, a stacked down+up pair or a single Expand All otherwise — mirroring the
upstream git-diff-view placement rules) wired to the model's expand API through the control's
`ExpandHunkUpCommand` / `ExpandHunkDownCommand` / `ExpandHunkAllCommand`. Expansion requires a
model built with real old/new file contents: paste-only diffs (empty file contents) compose from
the diff text itself and cannot expand. Rows render through a `VirtualizingStackPanel`, so large
diffs realize only the visible slice of containers.

Run the included demo to paste a diff and see it rendered:

```powershell
dotnet run --project Banned.CodeDiff.Avalonia.Demo/Banned.CodeDiff.Avalonia.Demo.csproj
```

## Basic Usage

Feed the raw diff text plus the full old/new file contents into `DiffFile`, then read the built
line models:

```csharp
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

const string diffText = """
    diff --git a/Program.cs b/Program.cs
    --- a/Program.cs
    +++ b/Program.cs
    @@ -1,2 +1,3 @@
     using System;
    -Console.WriteLine("Hello");
    +Console.WriteLine("Hello, World!");
    +Console.ReadLine();
    """;

const string oldText = "using System;\nConsole.WriteLine(\"Hello\");";
const string newText = "using System;\nConsole.WriteLine(\"Hello, World!\");\nConsole.ReadLine();";

var file = new DiffFile(
    oldFileName: "a/Program.cs",
    oldFileContent: oldText,
    newFileName: "b/Program.cs",
    newFileContent: newText,
    diffList: [diffText]);

file.Init();
file.BuildSplitDiffLines();

for (var i = 0; i < file.SplitLineLength; i++)
{
    var left = file.GetSplitLeftLine(i);
    var right = file.GetSplitRightLine(i);

    foreach (var item in new[] { left, right })
    {
        if (item?.Diff is not { } line)
        {
            continue; // collapsed placeholder row, etc.
        }

        Console.WriteLine($"{line.OldLineNumber}/{line.NewLineNumber} [{line.Type}] {item.Value?.TrimEnd('\r', '\n')}");
    }
}

// Unified view: file.BuildUnifiedDiffLines(); file.GetUnifiedLine(i);
```

Statistics: `file.AdditionLength` / `DeletionLength` (added/removed line counts), `DiffLineLength`
(total diff lines), and `DiffTool.GetLang(fileName)` (language detection from a file name).

## Word-Level Changes

Enable the fast-diff word-level ranges before `Init()` (optional):

```csharp
TemplateOptions.SetEnableFastDiffTemplate(true);
```

After `Init()`, paired added/removed lines expose character ranges that drive inline highlighting
(`null` on context lines). Each `DiffItem` carries the operation `Type` plus `StartIndex` / `Length`
within the line text:

```csharp
foreach (var item in line.DiffChanges?.Range ?? [])
{
    // item.Type: equal / insert / delete
    // item.StartIndex .. item.StartIndex + item.Length: span inside the line text
}
```

`DiffLine.Changes` holds the alternative relative-changes result with the same purpose.

## Hunk Expansion

Collapsed hunks can be expanded at runtime; the host refreshes on `Updated`:

```csharp
file.Updated += () => Console.WriteLine($"model updated ({file.UpdateCount})");

file.OnSplitHunkExpand(HunkExpandDirection.All, 0);
// unified counterpart: file.OnUnifiedHunkExpand(HunkExpandDirection.Up, index);
```

The `DiffView` control wires this automatically: each hunk placeholder row exposes the
appropriate expand buttons, and `file.OnAllExpand` / `OnAllCollapse` can be bound to toolbar
actions (the demo does this). A leading `@@` header whose hidden range is empty (a hunk starting
at line 1) is not rendered, matching GitHub and the upstream view components.

## Syntax Highlighting

The core library ships a built-in syntax engine: TextMateSharp tokenization with the same
TextMate grammars and GitHub light/dark themes the upstream shiki engine bundles. `InitSyntax`
tokenizes the *whole* old/new files (rule state carries across lines, so block comments and
template literals keep their state across collapsed hunks) and falls back to plain text for
unregistered languages or files over the 2000-line limit:

```csharp
var file = new DiffFile("a/Program.cs", oldText, "b/Program.cs", newText, [diffText]);
file.Init();          // InitRaw + InitSyntax (syntax included)

var syntaxLine = file.GetNewSyntaxLine(12); // null when the file is unhighlighted
// syntaxLine.Value — the raw line text; syntaxLine.NodeList — ordered spans:
//   { Node: { StartIndex, EndIndex (inclusive) }, Wrapper: { Properties: { Style } } }
// Style strings carry both theme colors: "--diff-view-dark:#F97583;--diff-view-light:#D73A49".
```

The language is detected from the file name (`DiffTool.GetLang`); registered languages cover
C#, TypeScript/TSX, JavaScript/JSX, JSON, HTML, CSS, Markdown, Python, Java, Go, Rust, C, C++,
shell, YAML, XML, Vue, and diff output. Replace the engine with `DiffFile.InitSyntax(myHighlighter)`
(`IDiffHighlighter`) — the AST contract mirrors the upstream `DiffHighlighter` interface, and
spans from both built-in and injected engines feed the same renderer.

## Usage Notes

- `DiffParser.Shared` and `TemplateOptions` are globally stateful (matching the JS original); do not
  call them concurrently across threads. Create a dedicated `DiffParser` instance per thread instead.
- Line text (`SplitLineItem.Value` / `DiffLine.Text`) keeps the original trailing newline (except on
  the source file's last line); render as-is and `TrimEnd` when concatenating elsewhere.
- The character-level diff (fast-diff) has a recursion-depth guard: pathological inputs throw a
  catchable `InvalidOperationException` instead of overflowing the stack.

## Testing

```powershell
dotnet test tests/Banned.CodeDiff.Tests/Banned.CodeDiff.Tests.csproj
dotnet test tests/Banned.CodeDiff.Avalonia.Tests/Banned.CodeDiff.Avalonia.Tests.csproj
```

589 core cases (NUnit; golden tests comparing field-by-field against the JS original
`@git-diff-view/core` + `fast-diff@1.3.0`, plus syntax goldens replayed from the real shiki
engine with the same codeToHast options as `@git-diff-view/shiki`) — plus 28 headless Avalonia
UI tests (NUnit + Avalonia.Headless.NUnit) covering the control theme, template instantiation,
row building in both view modes, mode switching, word-level highlight ranges and rectangle
computation, hunk expansion in all directions with button-placement rules, command wiring, row
virtualization on a 10k-line model, and syntax-run wiring with theme-switched colors.

## 📜 Changelog

[🧾 View CHANGELOG](./Docs/CHANGELOG.md)

## ⚖️ License

Copyright (c) 2026 banned.

This project is licensed under the Apache License 2.0. See the [LICENSE](./LICENSE) file for details.
See [NOTICE](./NOTICE) for upstream acknowledgements and third-party notices.
