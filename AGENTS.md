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

## 构建与验证

- 本地验证使用 `dotnet build Banned.CodeDiff.slnx`，需要时追加 `-c Release`。
- 测试使用 `dotnet test tests/Banned.CodeDiff.Tests/Banned.CodeDiff.Tests.csproj`。
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

## NuGet 打包与发布

- 除非用户在当前任务中明确要求，否则禁止在本地执行 `dotnet pack`、`nuget pack`、
  `dotnet nuget push`、`nuget push` 或任何手动发布命令。
- 不为了“验证包版本”而本地打包；应通过项目文件中的 `<Version>` 和 Release 构建结果验证。
- 不主动创建 Release、上传 `.nupkg` 或推送 NuGet。版本发布必须由用户决定并通过 GitHub 流程完成。
