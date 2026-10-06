# Banned.CodeDiff

[**English**](https://github.com/banned2054/Banned.CodeDiff/blob/master/README.md) | 简体中文

[![NuGet](https://img.shields.io/nuget/v/Banned.CodeDiff.Avalonia.svg)](https://www.nuget.org/packages/Banned.CodeDiff.Avalonia) [![下载量](https://img.shields.io/nuget/dt/Banned.CodeDiff.Avalonia.svg)](https://www.nuget.org/packages/Banned.CodeDiff.Avalonia) [![许可证](https://img.shields.io/badge/license-Apache_2.0-green)](https://github.com/banned2054/Banned.CodeDiff/blob/master/LICENSE)

面向 Avalonia 的 unified diff 查看控件库，可渲染 split 或 unified 视图，支持行级和词级高亮、
hunk 展开、语法高亮、行选择、复制与长行换行。宿主可以对行范围发起行内评论，并通过统一的
配色入口覆盖主要语义颜色。

首发包版本为 `0.1.0`，两个包均面向 .NET 10.0。当前内置视觉风格遵循 GitHub diff 习惯，
并提供明暗配色；后续版本可以扩展其他视觉风格。核心包不依赖 UI，也可供其他 .NET 应用使用。

本项目是 [`git-diff-view`](https://github.com/MrWangJustToDo/git-diff-view) 的 C# 移植版；
上游署名见 [NOTICE](https://github.com/banned2054/Banned.CodeDiff/blob/master/NOTICE)。

## 功能

- 解析 unified diff 文本，构建 split 或 unified 行模型。
- 计算新增行与删除行之间的字符级差异。
- 虚拟化渲染 diff，支持 hunk 展开、语法高亮和明暗配色。
- 选择行、复制选区或完整文件内容，并按视图宽度换行。
- 对行范围发起行内评论：视图渲染评论卡片与锚点持久高亮，草稿、提交与持久化由宿主负责。
- 通过 `DiffView.Palette` 按明暗主题覆盖主要语义颜色（增删行、上下文行、选区与评论卡片）。
- 核心解析与数据模型可脱离 Avalonia 使用，也可注入自定义 `IDiffHighlighter`。

## 安装

安装 Avalonia 控件包：

```powershell
dotnet add package Banned.CodeDiff.Avalonia
```

`Banned.CodeDiff.Avalonia` 依赖 `Banned.CodeDiff`，NuGet 会自动还原核心包。
只需要 diff 解析和数据处理时，也可以单独安装核心包。从源码构建时，可直接引用对应项目。

## 快速开始

在应用中引入控件主题；Avalonia 不会自动加载它：

```xml
<Application xmlns="https://github.com/avaloniaui">
    <Application.Styles>
        <FluentTheme />
        <StyleInclude Source="avares://Banned.CodeDiff.Avalonia/Themes/Generic.axaml" />
    </Application.Styles>
</Application>
```

用 diff 文本和完整的新旧文件内容创建 `DiffFile`：

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

绑定到 Avalonia 控件：

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:views="clr-namespace:Banned.CodeDiff.Avalonia.Views;assembly=Banned.CodeDiff.Avalonia">
    <views:DiffView DiffFile="{Binding DiffFile}" />
</Window>
```

## 可选功能

- 在 `Init()` 前调用 `TemplateOptions.SetEnableFastDiffTemplate(true)`，启用 fast-diff 词级区间。
- 将 `DiffView.ViewMode` 设为 `Unified`，切换到单栏视图。
- `DiffFile.InitSyntax()` 使用内置 TextMate 高亮器；也可传入自定义 `IDiffHighlighter`。
- 设置 `DiffView.IsSelectionEnabled` 开启行选择，通过 `SelectionCompleted` 读取结果。
- 使用 `CopySelectionAsync()`、`CopyOldFileAsync()` 或 `CopyNewFileAsync()` 复制内容；快捷键由宿主应用绑定。
- 设置 `DiffView.Wrap`，在保留行虚拟化的同时换行长文本。
- 设置 `DiffView.UseSingleLineNumberColumn`，将统一视图的新旧两个行号列合并为一列：删除行显示旧行号，上下文行与新增行显示新行号。分栏模式不受影响。
- 对行范围发起行内评论（M7）：
  1. 开启行选择并设置 `DiffView.FilePath`，让评论锚点携带文件身份。
  2. 选区完成后，把宿主按钮绑定到 `DiffView.BeginCommentCommand`，并订阅 `CommentRequested`——事件携带由当前选区推导的锚点（侧别 + 起止行号 + 文件路径）。
  3. 由宿主呈现评论编辑器，再把评论列表经 `DiffView.Comments` 交回视图。每个锚点在锚定范围最后一个可见行下方渲染一张卡片，锚点行带持久高亮，不受后续选区替换影响，视图模式切换与 hunk 展开后按行号重新对位。评论按文件身份隔离——仅 `Anchor.FilePath` 与 `DiffView.FilePath` 一致的评论参与渲染，切换文件不会把评论串到同号行上。评论集合变化后重新赋值 `Comments`（换成新集合实例）即可刷新；草稿、提交、回复、删除与持久化由宿主负责。
- 通过 `DiffView.Palette` 覆盖主要语义颜色（`Light`/`Dark` 两套槽位：增删行与上下文行背景、
  选区覆盖层与边条、评论高亮与评论卡片颜色）。槽位为 `null` 时保留内置上游配色，默认外观不变。
- 通过 `OnSplitHunkExpand` 或 `OnUnifiedHunkExpand` 展开 hunk；展开需要完整的新旧文件内容。

`DiffParser.Shared` 和 `TemplateOptions` 持有全局状态，请勿跨线程并发使用。

## Demo 与测试

运行 Demo：

```powershell
dotnet run --project Banned.CodeDiff.Avalonia.Demo/Banned.CodeDiff.Avalonia.Demo.csproj
```

运行全部测试：

```powershell
dotnet test Banned.CodeDiff.slnx --configuration Release
```

## 更新日志

[查看 CHANGELOG](https://github.com/banned2054/Banned.CodeDiff/blob/master/Docs/CHANGELOG.md)

## 许可证

Copyright (c) 2026 banned。本项目基于 Apache License 2.0 授权，详见
[LICENSE](https://github.com/banned2054/Banned.CodeDiff/blob/master/LICENSE)。
上游署名与第三方声明见 [NOTICE](https://github.com/banned2054/Banned.CodeDiff/blob/master/NOTICE)。
