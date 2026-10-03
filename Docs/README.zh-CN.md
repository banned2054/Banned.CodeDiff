# Banned.CodeDiff

[**English**](https://github.com/banned2054/Banned.CodeDiff/blob/master/README.md) | 简体中文

[![NuGet](https://img.shields.io/nuget/v/Banned.CodeDiff.Avalonia.svg)](https://www.nuget.org/packages/Banned.CodeDiff.Avalonia) [![Downloads](https://img.shields.io/nuget/dt/Banned.CodeDiff.Avalonia.svg)](https://www.nuget.org/packages/Banned.CodeDiff.Avalonia) [![License](https://img.shields.io/badge/license-Apache_2.0-green)](https://github.com/banned2054/Banned.CodeDiff/blob/master/LICENSE)

Avalonia 代码 diff 渲染控件库：GitHub 风格的 split / unified 视图，支持行级与词级高亮、
hunk 展开以及语法高亮。

宿主应用负责提供 diff 文本和外围 UI；本仓库负责 diff 解析、split / unified 行配对、
词级变更区间、hunk 展开/收起状态和渲染。核心逻辑库零 UI 依赖，也可以驱动 WPF、
控制台或其他任何 .NET 宿主。

本项目是 [`git-diff-view`](https://github.com/MrWangJustToDo/git-diff-view) 的 C# 移植版；
上游署名见 [NOTICE](https://github.com/banned2054/Banned.CodeDiff/blob/master/NOTICE)。

## 状态

开发按 [plan.md](https://github.com/banned2054/Banned.CodeDiff/blob/master/plan.md) 的里程碑推进：

| 里程碑 | 范围 | 状态 |
|---|---|---|
| M1 | 核心逻辑库 `Banned.CodeDiff` | 已完成 — 577 测试全绿，含与 JS 原版对比的黄金基准 |
| M2 | `Banned.CodeDiff.Avalonia` 最小 split 视图 | 已完成 — `DiffView` 控件 + Demo |
| M3 | 词级高亮渲染 | 已完成 |
| M4 | hunk 展开/收起 + 虚拟化 | 已完成 |
| M5 | 语法高亮（TextMateSharp） | 下一步 |
| M6 | 深浅主题、wrap、复制 | 计划中 |

## 功能

- **统一 diff 解析** — GNU unified diff 文本 → 结构化 `RawDiff`（多文件、多 hunk，
  支持 CRLF、`\ No newline at end of file`、二进制标记、bidi 隐藏字符检测）。
- **split / unified 行模型** — 构建左右配对的双栏行与单栏行（行号、增/删/改类型、
  收起占位），附带行号→索引查找等访问器，直接喂给列表控件渲染。
- **行内词级 diff** — 配对的增/删行的字符级差异区间，提供 relative-changes 与
  fast-diff 两种算法结果（`DiffLine.Changes` / `DiffLine.DiffChanges`），可直接驱动行内高亮。
- **hunk 展开/收起** — up / down / all 方向的展开状态机，步长可配（默认 40 行）；
  每次变更触发 `Updated` 事件，宿主刷新界面即可。
- **全局模板开关** — `TemplateOptions` 控制 fast-diff 词级 diff 与模板构建的启用。
- 核心库**零 UI 依赖**。
- **`DiffView` Avalonia 控件** — 只读 GitHub 风格 diff 视图，行级增删背景色、
  变更行内的词级高亮块、带可点击展开入口的收起 hunk 占位行（up / down / all，
  按钮摆放规则与上游/GitHub 一致）、面向大 diff 的行虚拟化、明暗两套配色；
  支持 split（双栏）与 unified（单栏、双行号、删除行在新增行上方）两种视图模式。

## 安装

```powershell
dotnet add package Banned.CodeDiff.Avalonia
```

> 控件包尚未发布。在此之前请通过项目引用使用（`Banned.CodeDiff.Avalonia`
> 会一并带上核心库）。`Banned.CodeDiff` 是否单独发布 NuGet 包尚未决定。

## Avalonia 控件

`DiffView` 把 `DiffFile` 渲染为只读 split 视图。先在应用里引入一次控件主题
（Avalonia 不会自动发现控件库的主题），再放置控件：

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

赋值 `DiffFile` 即渲染（未构建也没关系，控件会按需调用 `Init` / `Build*DiffLines`），
并通过模型的 `Updated` 事件保持同步。`ViewMode` 在默认的 `Split` 与 `Unified`
（单栏统一视图）之间切换。控件默认等宽字体、行号列宽自适应、渲染收起 hunk 占位行，
并随 `ActualThemeVariant` 切换明暗配色。

hunk 占位行自带展开按钮（首个 hunk 单个向上展开、文件尾部折叠条单个向下展开、
其余按剩余隐藏行数显示「上下成对」或单个全部展开——与上游 git-diff-view 的摆放规则
一致），经由控件的 `ExpandHunkUpCommand` / `ExpandHunkDownCommand` /
`ExpandHunkAllCommand` 接到模型的展开 API。展开需要以真实新旧文件内容构建模型：
纯 diff 文本（文件内容为空）从 diff 自身合成两侧，无法展开。行渲染走
`VirtualizingStackPanel`，大 diff 只实化可见范围内的行容器。

运行 Demo 体验「粘贴 diff → 出界面」：

```powershell
dotnet run --project Banned.CodeDiff.Avalonia.Demo/Banned.CodeDiff.Avalonia.Demo.csproj
```

## 基本用法

把原始 diff 文本和完整的新旧文件内容交给 `DiffFile`，再读取构建好的行模型：

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
            continue; // 收起占位行等
        }

        Console.WriteLine($"{line.OldLineNumber}/{line.NewLineNumber} [{line.Type}] {item.Value?.TrimEnd('\r', '\n')}");
    }
}

// 单栏视图同理：file.BuildUnifiedDiffLines(); file.GetUnifiedLine(i);
```

统计信息：`file.AdditionLength` / `DeletionLength`（增删行数）、`DiffLineLength`
（diff 总行数）、`DiffTool.GetLang(fileName)`（由文件名探测语言）。

## 词级变更

在 `Init()` 前开启 fast-diff 词级区间（可选）：

```csharp
TemplateOptions.SetEnableFastDiffTemplate(true);
```

`Init()` 之后，配对的增/删行会携带可用于行内高亮的字符区间（Context 行为 `null`）。
每个 `DiffItem` 包含操作类型 `Type`，以及行内文本的 `StartIndex` / `Length`：

```csharp
foreach (var item in line.DiffChanges?.Range ?? [])
{
    // item.Type：equal / insert / delete
    // item.StartIndex .. item.StartIndex + item.Length：行内文本的区间
}
```

`DiffLine.Changes` 保存用途相同的 relative-changes 算法结果。

## hunk 展开

收起的 hunk 可以在运行时展开；宿主在 `Updated` 回调里刷新：

```csharp
file.Updated += () => Console.WriteLine($"模型已更新（{file.UpdateCount}）");

file.OnSplitHunkExpand(HunkExpandDirection.All, 0);
// 单栏对应：file.OnUnifiedHunkExpand(HunkExpandDirection.Up, index);
```

`DiffView` 控件已自动接线：每个 hunk 占位行会显示对应方向的展开按钮，
`file.OnAllExpand` / `OnAllCollapse` 可绑定到工具栏动作（Demo 即如此）。
隐藏区间为空的首个 `@@` 头（hunk 从第 1 行开始）不渲染，与 GitHub 及上游一致。

## 使用注意

- `DiffParser.Shared` 与 `TemplateOptions` 是全局有状态的（与 JS 原版一致），
  请勿跨线程并发调用；并发场景请为每个线程创建独立的 `DiffParser` 实例。
- 行文本（`SplitLineItem.Value` / `DiffLine.Text`）保留原始行尾换行符
  （源文件最后一行除外），按原样渲染即可；拼接到其他字符串时注意 `TrimEnd`。
- 字符级 diff（fast-diff）内置递归深度护栏：对病态输入抛出可捕获的
  `InvalidOperationException`，而不是栈溢出。

## 测试

```powershell
dotnet test tests/Banned.CodeDiff.Tests/Banned.CodeDiff.Tests.csproj
dotnet test tests/Banned.CodeDiff.Avalonia.Tests/Banned.CodeDiff.Avalonia.Tests.csproj
```

577 个核心用例（NUnit，含与 JS 原版 `@git-diff-view/core` + `fast-diff@1.3.0` 逐字段对比的黄金基准）
+ 22 个 headless Avalonia UI 测试（NUnit + Avalonia.Headless.NUnit，覆盖主题加载、模板实例化、
两种视图行构建、模式切换、词级高亮区间与矩形计算、三方向 hunk 展开与按钮摆放规则、
命令接线，以及万行级模型的行虚拟化）。

## 📜 更新日志

[🧾 查看 CHANGELOG](https://github.com/banned2054/Banned.CodeDiff/blob/master/Docs/CHANGELOG.md)

## ⚖️ 许可证

Copyright (c) 2026 banned.

本项目基于 Apache License 2.0 授权，详见
[LICENSE](https://github.com/banned2054/Banned.CodeDiff/blob/master/LICENSE)。
上游署名与第三方声明见 [NOTICE](https://github.com/banned2054/Banned.CodeDiff/blob/master/NOTICE)。
