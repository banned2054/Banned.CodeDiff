# Banned.CodeDiff

[**English**](https://github.com/banned2054/Banned.CodeDiff/blob/master/README.md) | 简体中文

[![NuGet](https://img.shields.io/nuget/v/Banned.CodeDiff.Avalonia.svg)](https://www.nuget.org/packages/Banned.CodeDiff.Avalonia) [![Downloads](https://img.shields.io/nuget/dt/Banned.CodeDiff.Avalonia.svg)](https://www.nuget.org/packages/Banned.CodeDiff.Avalonia) [![License](https://img.shields.io/badge/license-Apache_2.0-green)](https://github.com/banned2054/Banned.CodeDiff/blob/master/LICENSE)

Avalonia 代码 diff 渲染控件库：GitHub 风格的 split / unified 视图，支持行级与词级高亮、
hunk 展开、语法高亮、拖拽行选择、剪贴板复制与长行换行。

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
| M5 | 语法高亮（TextMateSharp） | 已完成 — 与 shiki 引擎黄金基准对照 |
| M6 | 深浅主题、wrap、行选择、复制 | 已完成 |

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
- **语法高亮** — 内置 TextMate 引擎（TextMateSharp + shiki 同源的 TextMate 语法与
  GitHub 明暗主题）对完整新旧文件做词法着色，按行提供着色区间
  （`DiffFile.InitSyntax` / `GetOldSyntaxLine` / `GetNewSyntaxLine`）；
  通过 `IDiffHighlighter` 可替换引擎。
- **行选择数据层** — 面向已构建 `DiffFile` 的行区间选择查询（`MultiSelectData`）：
  1-based 行索引、随 hunk 展开/收起存续的隐藏行标志、区间归一化，
  语义与上游 multiSelect 一致。
- 核心库**零 UI 依赖**。
- **`DiffView` Avalonia 控件** — 只读 GitHub 风格 diff 视图，行级增删背景色、
  变更行内的词级高亮块、带可点击展开入口的收起 hunk 占位行（up / down / all，
  按钮摆放规则与上游/GitHub 一致）、面向大 diff 的行虚拟化、明暗两套配色；
  支持 split（双栏）与 unified（单栏、双行号、删除行在新增行上方）两种视图模式。
- **行选择** — 可选开启的 GitHub 风格行号列拖拽选择（`IsSelectionEnabled`），
  含 `SelectionChanged` / `SelectionCompleted` 事件、预选行 API 与控件侧选区查询。
- **复制** — `CopySelectionCommand` / `CopyOldFileCommand` / `CopyNewFileCommand`
  （及同名异步方法）把选中行或完整新旧文件复制到剪贴板。
- **wrap** — 可选开启的长行换行（`Wrap`），在视图宽度处折行且不破坏虚拟化；
  行号列保持定宽，split 行左右等高（取较高者）。

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
（单栏统一视图）之间切换；`SyntaxHighlight`（默认开）会执行 `InitSyntax`，
行内文字按语法着色并随明暗主题取色，`Highlighter` 可注入自定义 `IDiffHighlighter` 引擎。
`Wrap`（默认关）在视图宽度处换行长行且不破坏虚拟化——行号列保持定宽，
split 行左右等高（取较高者）。`IsSelectionEnabled`（默认关）开启 GitHub 风格的
拖拽行选择（见下文「行选择」一节）。控件默认等宽字体、行号列宽自适应、
渲染收起 hunk 占位行，并随 `ActualThemeVariant` 切换明暗配色。

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

Demo 工具栏同时提供 M6 功能的开关——「启用行选择」「自动换行」与三个复制按钮
（复制选中行 / 旧文件 / 新文件），状态栏会反馈选区（「已选 N 行(old 12-34)」）
与复制结果。Ctrl+C 在窗口层绑定，作为宿主侧快捷键接线的示例（控件本身不内置快捷键）。

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

## 语法高亮

核心库内置语法引擎：TextMateSharp 词法分析，配 upstream shiki 引擎同款打包的
TextMate 语法与 GitHub 明暗主题。`InitSyntax` 对**完整**新旧文件全文着色
（规则状态跨行延续，块注释、模板字符串跨折叠 hunk 依然正确），未注册语言或超过
2000 行的文件自动回退纯文本：

```csharp
var file = new DiffFile("a/Program.cs", oldText, "b/Program.cs", newText, [diffText]);
file.Init();          // InitRaw + InitSyntax（语法一并就绪）

var syntaxLine = file.GetNewSyntaxLine(12); // 未高亮时为 null
// syntaxLine.Value — 原始行文本；syntaxLine.NodeList — 有序着色区间：
//   { Node: { StartIndex, EndIndex（含端） }, Wrapper: { Properties: { Style } } }
// Style 字符串同时携带两个主题的颜色："--diff-view-dark:#F97583;--diff-view-light:#D73A49"。
```

语言按文件名探测（`DiffTool.GetLang`）；已注册语言覆盖 C#、TypeScript/TSX、
JavaScript/JSX、JSON、HTML、CSS、Markdown、Python、Java、Go、Rust、C、C++、
shell、YAML、XML、Vue 与 diff 输出。用 `DiffFile.InitSyntax(myHighlighter)`
（`IDiffHighlighter`）可替换引擎——AST 契约与上游 `DiffHighlighter` 接口对应，
内置与注入引擎的区间都走同一套渲染。

## 行选择

`DiffView.IsSelectionEnabled`（默认 `false`，可选开启）启用上游 multiSelect 的移植：
在行号列上拖拽即按 GitHub 风格选择行区间。split 视图拖拽锁定起始侧，悬停行内容
也会延伸选区；unified 视图只有悬停行号区才延伸。context 行双侧高亮；完成后的选区
保持高亮直到下一次交互，收起 hunk 隐藏的行保留在选区里、展开后自动补齐高亮。

```csharp
view.SelectionCompleted += (_, e) =>
{
    // e.Result — MultiSelectResult?（未形成区间即释放时为 null）：
    //   Range：{ Side（Old/New）、StartLineNumber、EndLineNumber }
    //   Lines：SelectedLine 记录 — 1-based Index、LineNumber、Value、
    //          IsHide、IsAdd、IsDelete、IsContext
};

view.SetPreselectedLines(oldLines: [12, 34]); // 从已有标注预选
view.GetSelectionResult();  // 拖拽中为实时区间；释放后为最近完成的区间
view.GetSelectionState();   // MultiSelectState — IsSelecting、StartInfo、CurrentRange
view.ClearSelection();
```

`SetPreselectedLines` 会把每一侧的列表合并成一个 min/max 大区间（上游已知语义——
离散列表会把 min 与 max 之间的所有行点亮）。该功能的数据层位于核心库
（`MultiSelectData`：区间归一化、split/unified 选中行查询、选区转文本），
无需 UI 即可计算与断言选区结果。

## 复制到剪贴板

`DiffView` 提供三个复制操作，同时以命令和异步方法暴露：

```csharp
view.CopySelectionCommand.Execute(null); // 或：await view.CopySelectionAsync();
view.CopyOldFileCommand.Execute(null);   // 或：await view.CopyOldFileAsync();
view.CopyNewFileCommand.Execute(null);   // 或：await view.CopyNewFileAsync();
```

选区复制写出的就是界面所见——每个选中行一行输出、跳过隐藏行、去掉尾部换行；
整文件复制把新旧文件内容原样传出。没有可用选区时 `CopySelectionCommand` 自动
禁用（`CanExecuteChanged` 跟踪选区与模型变化）。控件**不内置键盘快捷键**——
由宿主自行绑定到 `CopySelectionCommand`（Demo 在窗口层绑定 Ctrl+C），
避免与宿主既有绑定冲突。

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

614 个核心用例（NUnit，含与 JS 原版 `@git-diff-view/core` + `fast-diff@1.3.0`
逐字段对比的黄金基准，用真实 shiki 引擎按 `@git-diff-view/shiki` 同款
codeToHast 参数回放的语法黄金基准，以及 multiSelect 数据层与选区文本生成的单测）
+ 67 个 headless Avalonia UI 测试（NUnit + Avalonia.Headless.NUnit，覆盖主题加载、模板实例化、
两种视图行构建、模式切换、词级高亮区间与矩形计算、三方向 hunk 展开与按钮摆放规则、
命令接线、万行级模型的行虚拟化，语法着色接线与明暗主题取色，行选择拖拽语义、
事件与预选，选区与整文件的剪贴板复制，以及 wrap 布局与换行下的滚动、虚拟化保持）。

## 📜 更新日志

[🧾 查看 CHANGELOG](https://github.com/banned2054/Banned.CodeDiff/blob/master/Docs/CHANGELOG.md)

## ⚖️ 许可证

Copyright (c) 2026 banned.

本项目基于 Apache License 2.0 授权，详见
[LICENSE](https://github.com/banned2054/Banned.CodeDiff/blob/master/LICENSE)。
上游署名与第三方声明见 [NOTICE](https://github.com/banned2054/Banned.CodeDiff/blob/master/NOTICE)。
