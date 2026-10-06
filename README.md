# Banned.CodeDiff

English | [**简体中文**](https://github.com/banned2054/Banned.CodeDiff/blob/master/Docs/README.zh-CN.md)

[![NuGet](https://img.shields.io/nuget/v/Banned.CodeDiff.Avalonia.svg)](https://www.nuget.org/packages/Banned.CodeDiff.Avalonia) [![Downloads](https://img.shields.io/nuget/dt/Banned.CodeDiff.Avalonia.svg)](https://www.nuget.org/packages/Banned.CodeDiff.Avalonia) [![License](https://img.shields.io/badge/license-Apache_2.0-green)](https://github.com/banned2054/Banned.CodeDiff/blob/master/LICENSE)

An Avalonia diff viewer for unified diff text. It renders split or unified views with line and word
highlights, expandable hunks, syntax highlighting, line selection, copy, and long-line wrapping.
Hosts can attach inline comments to line ranges and override the main semantic colors through a
single palette entry.

The first package version is `0.1.0`; both packages target .NET 10.0. The built-in visual style
currently follows GitHub diff conventions and includes light and dark palettes. Additional styles
may be added in future releases. The core package has no UI dependency and can be used by other
.NET applications.

This project is a C# port of [`git-diff-view`](https://github.com/MrWangJustToDo/git-diff-view).
See [NOTICE](./NOTICE) for upstream acknowledgements.

## Features

- Parse unified diff text and build split or unified line models.
- Calculate character-level changes for paired added and removed lines.
- Render virtualized diffs with hunk expansion, syntax highlighting, and light/dark palettes.
- Select lines, copy selected or complete file contents, and wrap long lines.
- Attach inline comments to line ranges: the view renders comment cards and persistent anchor
  highlights while the host owns drafting, submitting, and persistence.
- Override the main semantic colors (line kinds, selection, comment cards) per theme variant
  through `DiffView.Palette`.
- Use the core parser and models without Avalonia, or provide a custom `IDiffHighlighter`.

## Installation

Install the Avalonia control package:

```powershell
dotnet add package Banned.CodeDiff.Avalonia
```

`Banned.CodeDiff.Avalonia` depends on `Banned.CodeDiff`; NuGet restores the core package automatically.
The core package can also be installed alone for diff parsing and data processing. When building from
source, reference either project directly.

## Quick start

Include the control library's theme in your application. Avalonia does not load it automatically:

```xml
<Application xmlns="https://github.com/avaloniaui">
    <Application.Styles>
        <FluentTheme />
        <StyleInclude Source="avares://Banned.CodeDiff.Avalonia/Themes/Generic.axaml" />
    </Application.Styles>
</Application>
```

Create a `DiffFile` from the diff and full old/new file contents:

```csharp
using Banned.CodeDiff.Services;

const string diffText = """
diff --git a/Program.cs b/Program.cs
--- a/Program.cs
+++ b/Program.cs
@@ -1 +1 @@
-Console.WriteLine("old");
+Console.WriteLine("new");
""";

var file = new DiffFile(
    "a/Program.cs",
    "Console.WriteLine(\"old\");",
    "b/Program.cs",
    "Console.WriteLine(\"new\");",
    [diffText]);

file.Init();
file.BuildSplitDiffLines();
```

Bind it to the Avalonia control:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:views="clr-namespace:Banned.CodeDiff.Avalonia.Views;assembly=Banned.CodeDiff.Avalonia">
    <views:DiffView DiffFile="{Binding DiffFile}" />
</Window>
```

## Optional features

- Enable fast word-level ranges before `Init()` with `TemplateOptions.SetEnableFastDiffTemplate(true)`.
- Set `DiffView.ViewMode` to `Unified` for a single-column view.
- `DiffFile.InitSyntax()` uses the built-in TextMate highlighter; pass an `IDiffHighlighter` to use another engine.
- Set `DiffView.IsSelectionEnabled` to enable line selection; use `SelectionCompleted` to read the result.
- Use `CopySelectionAsync()`, `CopyOldFileAsync()`, or `CopyNewFileAsync()` to copy content. Bind keyboard shortcuts in the host application.
- Set `DiffView.Wrap` to wrap long lines while preserving row virtualization.
- Set `DiffView.UseSingleLineNumberColumn` to merge the unified view's old/new line-number columns into one: deleted lines show the old number, context and added lines the new one. Split view is unaffected.
- Attach inline comments to line ranges (M7):
  1. Enable line selection and set `DiffView.FilePath` so anchors carry the file identity.
  2. After a selection, bind a host button to `DiffView.BeginCommentCommand` and handle `CommentRequested` — it delivers the anchor (side + start/end line numbers + file path) derived from the selection.
  3. Render the comment editor in the host, then hand the comment list back through `DiffView.Comments`. Each anchor renders one card below the last visible line of its range, and its lines keep a persistent highlight that survives new selections, view-mode switches, and hunk expansions. Comments are file-scoped: only those whose `Anchor.FilePath` matches `DiffView.FilePath` render, so switching files never leaks comments onto same-numbered lines. Reassign `Comments` (a new collection instance) after the comment set changes; drafting, submitting, replying, deleting, and persistence stay in the host.
- Override the main semantic colors with `DiffView.Palette` (`Light`/`Dark` slots for added,
  deleted, and context line backgrounds, the selection overlay and edge strip, and the comment
  highlight and card colors). `null` slots keep the built-in upstream colors; the default
  appearance is unchanged.
- Expand hunks with `OnSplitHunkExpand` or `OnUnifiedHunkExpand`. Expansion requires full old/new file contents.

`DiffParser.Shared` and `TemplateOptions` hold global state. Avoid using them concurrently across threads.

## Demo and tests

Run the demo:

```powershell
dotnet run --project Banned.CodeDiff.Avalonia.Demo/Banned.CodeDiff.Avalonia.Demo.csproj
```

Run all tests:

```powershell
dotnet test Banned.CodeDiff.slnx --configuration Release
```

## Changelog

See the [CHANGELOG](https://github.com/banned2054/Banned.CodeDiff/blob/master/Docs/CHANGELOG.md).

## License

Copyright (c) 2026 banned. Licensed under the Apache License 2.0; see [LICENSE](https://github.com/banned2054/Banned.CodeDiff/blob/master/LICENSE).
Upstream acknowledgements and third-party notices are in [NOTICE](./NOTICE).
