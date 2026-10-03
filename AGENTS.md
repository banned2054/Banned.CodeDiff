# Banned.CodeDiff 项目约定

## 项目定位

- 本仓库是 [git-diff-view](https://github.com/MrWangJustToDo/git-diff-view)（MIT）的 C#/Avalonia 移植，
  最终交付 Avalonia diff viewer 控件库：传入统一 diff 文本 → 渲染 GitHub 风格 split/unified 视图。
- JS 源仓库的本地克隆在 `C:\Code\JavaScript\git-diff-view`，是移植实现与黄金测试的对照基准。
  测试 harness 支持用环境变量 `GDV_REPO` 覆盖该路径。
- 仓库结构：`Banned.CodeDiff`（核心逻辑库，M1 已完成）、`tests/Banned.CodeDiff.Tests` +
  `tests/js-harness`（xUnit 与黄金测试）、规划中的 `Banned.CodeDiff.Avalonia`（控件库）与配套 Demo。
- 发布计划：至少 `Banned.CodeDiff.Avalonia` 会发布到 NuGet；`Banned.CodeDiff` 是否单独发包**未决定**，
  未经用户明确要求不要为它添加打包/发布配置。
- 里程碑计划与当前状态见 `plan.md`，完成一个里程碑后同步更新。

## C# 代码风格

- 优先使用 guard clause 和 early return 降低嵌套层级。
- 当条件分支已经 `return`、`throw`、`continue` 或 `break` 时，不再保留不必要的 `else`。
- 对“条件不满足即可退出”的逻辑，优先先处理否定条件，再在主路径继续执行。
- 不为追求早返回而机械翻转复杂条件；条件可读性和行为等价性优先。
- 逻辑运算符 `&&` / `||` 需要换行时，运算符放在**行末**，不放行首。
- 函数调用的参数紧跟左括号之后，**不在左括号后换行**；参数过长时在参数之间换行缩进。
- 仓库根 `.editorconfig` 强制 UTF-8（无 BOM）、LF 行尾、去除行尾空格、文件末尾换行；
  遵循编辑器/格式化工具的结果，不引入相异的手工排版。
- 遵循项目现有格式化结果，不在功能修改中混入无关的全项目格式化。

## 移植代码约定

- 移植代码优先与 JS/TS 原版**逐行、逐结构对应**，文件头注释标明来源 ts 文件
  （现有代码均已如此，新文件保持一致）。
- 不“顺手修正”原版行为。任何与 JS 原版的行为差异都必须有黄金测试证据并单独说明；
  行为对齐由 golden 测试保证，不靠目测。
- JS 语义怪癖必须在 C# 里显式复刻，已知清单：
  - JS 用字符串枚举（如 `DiffHunkExpansionType`），沿用其语义而不是改造成位标志。
  - JS 除法是浮点：fast-diff 的 `length / 2` 在 C# 必须写成 `/ 2.0`。
  - JS `substring` / `slice` 对越界参数会钳制或交换，C# 需显式复刻该行为。
  - `equalities` 稀疏数组允许负索引，用 `Dictionary` 复刻。
  - fast-diff 对个别输入无限递归，C# 用 depth 1000 护栏抛 `InvalidOperationException`。
  - `Number(undefined) = NaN` 这类转换用可空值类型表达（见 `Models/DiffFileModels.cs` 注释）。
- `DiffParser.Shared` 与 `TemplateOptions` 是全局有状态的（与 JS 原版一致），禁止并发使用。
- 明确不移植的内容：`parse/template.ts` 的 HTML 模板构建（Avalonia 不消费 HTML 字符串，
  但其全局开关 `TemplateOptions` 已保留）、`cache.ts`（web 专用跨实例缓存）、
  `multiSelect/*`（视 M6 需求再定）。

## 移植对照表

| C# | JS 来源 |
|---|---|
| `Models/DiffLine.cs` | `packages/core/src/parse/diff-line.ts` + `raw-diff.ts` |
| `Models/DiffFileModels.cs` | `packages/core/src/diff-file.ts`（类型） |
| `Models/ChangeRangeModels.cs` | `packages/core/src/parse/change-range.ts`（类型） |
| `Models/DiffFileItemModels.cs` | `packages/core/src/diff-file-utils.ts`（类型） |
| `Models/SourceFile.cs` | `packages/core/src/file.ts`（raw 部分） |
| `Models/SyntaxAst.cs` | `packages/utils/src/highlightAST.ts`（仅类型） |
| `Services/DiffParser.cs` | `packages/core/src/parse/diff-parse.ts` |
| `Services/Transform.cs` | `packages/core/src/parse/transform.ts` |
| `Services/ChangeRange.cs` | `packages/core/src/parse/change-range.ts` |
| `Services/DiffTool.cs` | `packages/core/src/parse/diff-tool.ts` + `template.ts` 的全局开关（`TemplateOptions`） |
| `Services/DiffFile.cs` | `packages/core/src/diff-file.ts` |
| `Services/FastDiff.cs` | npm 包 `fast-diff@1.3.0` |
| `Utils/DiffFileUtils.cs` | `packages/core/src/diff-file-utils.ts` |
| `Utils/EscapeHtml.cs` | `packages/core/src/escape-html.ts` |
| `Utils/HighlightColors.cs` | `packages/utils/src/color.ts` |
| `Utils/Symbol.cs` | `packages/utils/src/symbol.ts` |

## 目录与职责

- 核心库 `Banned.CodeDiff` 按类型组织为 `Models` / `Services` / `Utils`：
  - `Models`：数据模型、状态、枚举、配置等纯数据类型，不依赖具体渲染。
  - `Services`：需要实例化、持有状态或生命周期的组件（`DiffFile`、`DiffParser` 等）。
  - `Utils`：无状态、以 static 提供的解析、转换和辅助逻辑。
- `Banned.CodeDiff.Avalonia`（规划）按 MVVM 组织：`Views`（控件与 `.axaml`）、
  `ViewModels`（可绑定状态与命令）、`Services`、`Models`；不为单个新增功能创建新的一级目录。
- 文件移动与公开 namespace 修改分开评估；单纯整理目录默认保持公开 namespace，
  调整公开 API 必须由用户明确授权。
- 每个 `public` 顶级类型放独立文件；只服务于单个实现的私有类型保留在所属类中。

## Avalonia 控件库约定

- **Avalonia 不会自动加载控件库的 `Themes/Generic.axaml`**（与 WPF 不同，没有 `ThemeInfo`
  魔法发现）。库的全部 ControlTheme 集中在 `Themes/Generic.axaml` 一个入口，由宿主在
  `Application.Styles` 显式 `StyleInclude`（README 双语使用示例必须包含这一行）。
- **该文件根元素必须是 `Styles`**，ControlTheme 放在 `Styles.Resources` 的
  `ResourceDictionary` 内并带 `x:Key="{x:Type ...}"`（Fluent 主题同款结构）。根写成
  `ResourceDictionary` 会让宿主构建期报 AVLN2000（expected IStyle）；`Styles` 直接子元素
  不能带 x:Key。
- 新增控件的主题一律加入该字典，不另开第二入口；`TextBox.Watermark` 在 Avalonia 12 已改名
  `PlaceholderText`。
- **运行时按 URI 加载编译 XAML 会报 "No precompiled XAML found"**（Demo 能工作是因为
  编译期 include）——测试/宿主要引入库主题必须走**编译期** include：带 `x:Class` 的
  `Styles` 子类 `AvaloniaXamlLoader.Load(this)`，不要用 C# 运行时构造 `StyleInclude`。
- Avalonia 12 的 `FormattedText` 已无 `Text`/`Bounds`/`HitTestTextPosition`；自绘文本用
  `TextLayout`（`HitTestTextPosition(int)→Rect`、`Draw(context, origin)`、
  `TextLines[i].WidthIncludingTrailingWhitespace/Height`），`FillRectangle` 圆角参数是 float。
- `DiffFile` 等核心类型位于 `Banned.CodeDiff.Services` 命名空间（不是 Models）。
- UI 回归测试在 `tests/Banned.CodeDiff.Avalonia.Tests`（xunit.v3 + Avalonia.Headless.XUnit；
  该包依赖 **xunit.v3** 而非 xunit 2.x，注册方式为
  `[assembly: AvaloniaTestApplication(typeof(Builder))]` + 静态 `BuildAvaloniaApp()`，
  测试方法用 `[AvaloniaFact]`）。TestApp 以消费方同款 StyleInclude 加载主题，
  控件模板/渲染改动的验证以这些测试为准，不依赖截图。

## 验证纪律（血泪教训）

- 构建输出**不允许 tail/grep 截断到看不见错误**；判断成败必须看退出码或显式 grep error，
  防止"构建失败但跑着旧二进制"的假绿。
- UI 截图验证必须用**中立提问**的图像分析（不得把预期写进问题），且"通过"结论要有可交叉
  验证的程序化证据（如界面统计文本与独立控制台输出一致）；视觉模型可能部分真实部分编造，
  每项结论独立核实。

## 构建与验证

- 本地验证使用 `dotnet build Banned.CodeDiff.slnx`，需要时追加 `-c Release`。
- 测试框架为 **NUnit 5.0.0**（约束模型断言 `Assert.That`，NUnit 5 无 ClassicAssert）+
  `NUnit3TestAdapter` 6.3.0（VSTest 模式）：`dotnet test tests/Banned.CodeDiff.Tests/...csproj`
  与 `dotnet test tests/Banned.CodeDiff.Avalonia.Tests/...csproj`。
- **NUnit 5.0.0 已移除 `EnableNUnitRunner`/MTP 集成**（props 为空、零依赖），不要重新引入
  `global.json` 的 MTP runner 配置（会用过然后已删除，VSTest 适配器路线才可用）。
- UI 测试用 `Avalonia.Headless.NUnit`（其编译目标是 NUnit 4.5.1，在 NUnit 5.0.0 运行时
  实测兼容），测试方法标 `[AvaloniaTest]`，应用注册仍是
  `[assembly: AvaloniaTestApplication(typeof(Builder))]` + 静态 `BuildAvaloniaApp()`。
- 两个测试项目都声明了 `[assembly: Parallelizable(ParallelScope.None)]`——
  `DiffParser.Shared`/`TemplateOptions` 全局有状态，禁止并行，不要移除。
- 改包引用后警惕**过期 restore 缓存的假绿**：csproj 删包后旧 assets 仍能让构建/测试通过，
  必须实际触发还原再验证。
- 测试已通过 `xunit.runner.json` 关闭并行（`DiffParser.Shared` 全局有状态所致），**不要移除该配置**。
- 重新生成黄金数据：`cd tests/js-harness && node build.mjs`（需要 esbuild 与 `fast-diff@1.3.0`，
  源仓库路径可用 `GDV_REPO` 覆盖）。
- 重新生成 Demo 数据：`cd tests/js-harness && node gen-demodata.mjs`
  （来自 JS 仓库 `ui/vue-example/src/data.ts`）。

## 文档与署名

- `README.md`（英文）与 `Docs/README.zh-CN.md`（中文）是同一内容的双语版本，修改时**必须同步**。
- 行为变更记录到 `Docs/CHANGELOG.md` 的 Unreleased 段，不擅自修改版本号。
- `NOTICE` 记录上游衍生关系：git-diff-view（MIT）、GitHub Desktop（MIT）、
  Vue escape-html（MIT）、fast-diff（Apache-2.0）。**新增任何上游衍生代码必须同步更新 NOTICE**；
  后续接入 TextMateSharp 等第三方库时同样如此。

## Git 提交风格

- 提交标题使用 conventional 前缀 + 中文描述，冒号后不加空格：`feat:中文描述`、`fix:中文描述`、
  `test:中文描述`、`docs:中文描述`、`chore:中文描述` 等。
- 标题不以句号结尾；需要补充细节时写在正文（可用列表），标题保持一行简述。
- 不做 push，发布相关操作见下节。

## NuGet 打包与发布

- 除非用户在当前任务中明确要求，否则禁止在本地执行 `dotnet pack`、`nuget pack`、
  `dotnet nuget push`、`nuget push` 或任何手动发布命令。
- 不为了“验证包版本”而本地打包；应通过项目文件中的 `<Version>` 和 Release 构建结果验证。
- 不主动创建 Release、上传 `.nupkg` 或推送 NuGet。版本发布必须由用户决定并通过 GitHub 流程完成。
