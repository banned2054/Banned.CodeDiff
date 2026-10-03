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
| M2 | `Banned.CodeDiff.Avalonia` minimal split view | Next |
| M3 | Word-level highlight rendering | Planned |
| M4 | Hunk expand/collapse + virtualization | Planned |
| M5 | Syntax highlighting (TextMateSharp) | Planned |
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
- Zero UI dependencies in the core library

## Installation

```powershell
dotnet add package Banned.CodeDiff.Avalonia
```

> The control package is not published yet (M2 in development). Until then, consume the core logic
> library via a project reference to `Banned.CodeDiff/Banned.CodeDiff.csproj`. Whether
> `Banned.CodeDiff` also ships as a standalone NuGet package has not been decided yet.

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
```

577 cases, including golden tests that compare field-by-field against the JS original
(`@git-diff-view/core` + `fast-diff@1.3.0`).

## 📜 Changelog

[🧾 View CHANGELOG](./Docs/CHANGELOG.md)

## ⚖️ License

Copyright (c) 2026 banned.

This project is licensed under the Apache License 2.0. See the [LICENSE](./LICENSE) file for details.
See [NOTICE](./NOTICE) for upstream acknowledgements and third-party notices.
