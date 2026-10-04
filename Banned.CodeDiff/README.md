# Banned.CodeDiff

[English](#english) | [简体中文](#简体中文)

## English

`Banned.CodeDiff` is a .NET library for parsing unified Git diff text and building structured diff models. It has no UI framework dependency and can be used independently of Avalonia.

### Features

- Parse unified diff text into headers, hunks, and line models.
- Build split and unified line models, including expandable hunk data.
- Calculate word-level changes for modified lines.
- Provide built-in TextMate syntax highlighting, with support for custom `IDiffHighlighter` implementations.

### Install

```powershell
dotnet add package Banned.CodeDiff
```

### Quick start

```csharp
using Banned.CodeDiff.Services;

const string diffText = """
diff --git a/Program.cs b/Program.cs
--- a/Program.cs
+++ b/Program.cs
@@ -1 +1 @@
-Console.WriteLine("Old");
+Console.WriteLine("New");
""";

var diff = new DiffFile(
    "a/Program.cs",
    "Console.WriteLine(\"Old\");",
    "b/Program.cs",
    "Console.WriteLine(\"New\");",
    [diffText]);

diff.Init();
diff.BuildSplitDiffLines();

Console.WriteLine($"Old-side rows: {diff.SplitLeftLines.Count}");
Console.WriteLine($"New-side rows: {diff.SplitRightLines.Count}");
```

Use `BuildUnifiedDiffLines()` to build the unified view model. `DiffFile.Init()` also initializes syntax highlighting; provide a custom `IDiffHighlighter` to `InitSyntax()` when you need another engine.

### Thread safety

`DiffParser.Shared` and `TemplateOptions` hold global mutable state and must not be used concurrently. Creating one `DiffParser` instance per operation does not remove the shared-state constraint from `TemplateOptions`.

### Links

- [Full project README](https://github.com/banned2054/Banned.CodeDiff/blob/master/README.md)
- [Changelog](https://github.com/banned2054/Banned.CodeDiff/blob/master/Docs/CHANGELOG.md)
- [License](https://github.com/banned2054/Banned.CodeDiff/blob/master/LICENSE)
- [Third-party notices](https://github.com/banned2054/Banned.CodeDiff/blob/master/NOTICE)

## 简体中文

`Banned.CodeDiff` 是用于解析 Git unified diff 文本并生成结构化 diff 模型的 .NET 库。它不依赖 UI 框架，可独立于 Avalonia 使用。

### 功能

- 将 unified diff 文本解析为文件头、hunk 和行模型。
- 生成 split 与 unified 行模型，并提供 hunk 展开数据。
- 计算修改行的词级变更区间。
- 提供内置 TextMate 语法高亮，并支持自定义 `IDiffHighlighter` 实现。

### 安装

```powershell
dotnet add package Banned.CodeDiff
```

### 快速入门

```csharp
using Banned.CodeDiff.Services;

const string diffText = """
diff --git a/Program.cs b/Program.cs
--- a/Program.cs
+++ b/Program.cs
@@ -1 +1 @@
-Console.WriteLine("Old");
+Console.WriteLine("New");
""";

var diff = new DiffFile(
    "a/Program.cs",
    "Console.WriteLine(\"Old\");",
    "b/Program.cs",
    "Console.WriteLine(\"New\");",
    [diffText]);

diff.Init();
diff.BuildSplitDiffLines();

Console.WriteLine($"旧侧行数：{diff.SplitLeftLines.Count}");
Console.WriteLine($"新侧行数：{diff.SplitRightLines.Count}");
```

调用 `BuildUnifiedDiffLines()` 可生成 unified 视图模型。`DiffFile.Init()` 也会初始化语法高亮；需要使用其他引擎时，可将自定义 `IDiffHighlighter` 传给 `InitSyntax()`。

### 线程安全

`DiffParser.Shared` 和 `TemplateOptions` 都持有全局可变状态，禁止并发使用。为每个操作创建独立的 `DiffParser` 实例，并不能消除 `TemplateOptions` 的共享状态限制。

### 链接

- [项目完整 README](https://github.com/banned2054/Banned.CodeDiff/blob/master/README.md)
- [更新日志](https://github.com/banned2054/Banned.CodeDiff/blob/master/Docs/CHANGELOG.md)
- [许可证](https://github.com/banned2054/Banned.CodeDiff/blob/master/LICENSE)
- [第三方声明](https://github.com/banned2054/Banned.CodeDiff/blob/master/NOTICE)
