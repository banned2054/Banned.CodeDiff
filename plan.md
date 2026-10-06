# Banned.CodeDiff 里程碑计划

## 1. 项目目标

把 [git-diff-view](https://github.com/MrWangJustToDo/git-diff-view)（JS/TS，MIT）的 diff 展示能力
移植为 C#/.NET 实现，最终交付 Avalonia 控件库 `Banned.CodeDiff.Avalonia`：宿主传入统一 diff
文本，即得到 GitHub 风格的 split / unified 视图，含行级与词级高亮、hunk 展开/收起、
语法高亮与深浅主题。

核心逻辑（`Banned.CodeDiff`）零 UI 依赖、先行完成，Avalonia 层只做渲染与交互。

## 2. 总体节奏

| 里程碑 | 主题 | 状态 |
|---|---|---|
| M1 | 纯逻辑库（解析 + 行模型 + 词级区间 + 展开状态机） | ✅ 已完成 |
| M2 | 最小可看：split 视图渲染 | ✅ 已完成 |
| M3 | 词级高亮渲染 | ✅ 已完成 |
| M4 | hunk 展开/收起 UI + 虚拟化 | ✅ 已完成 |
| M5 | 语法高亮 | ✅ 已完成 |
| M6 | 打磨 | ✅ 已完成 |
| M7 | 行范围评论与主题定制入口 | ⏭ 下一优先级（规划中） |

每个里程碑保持可构建、可测试、可演示；完成一个更新一次本文件状态与 `Docs/CHANGELOG.md`。

## 3. 当前状态

- `Banned.CodeDiff` 核心库完成：解析（`DiffParser`）、split/unified 行模型（`DiffFile`）、
  词级区间（`ChangeRange` / `FastDiff`）、展开状态机、`TemplateOptions` 全局开关。
- 目录已按 `Models` / `Services` / `Utils` 分类。
- 614 个核心测试 + 67 个 Avalonia headless 测试全绿（构建 0 警告 0 错误），
  含与 JS 原版逐字段对比的黄金基准，以及与真实 shiki 引擎逐字段对比的语法黄金基准。
- `Banned.CodeDiff.Avalonia` 已就位：`DiffView` 控件（split/unified 视图、词级高亮、
  语法高亮、hunk 展开/收起 + 虚拟化、明暗主题、行选择、复制、长行 wrap）与配套 Demo，
  详见第 4~8 节执行结果。
- 下一优先级为 M7：复用现有行范围选择，补齐宿主可接入的行内评论呈现，并提供简洁的主题配色覆盖入口；
  任意字符范围选择暂不纳入本阶段。

## 4. M2 最小可看

**目标**：新建 `Banned.CodeDiff.Avalonia` 控件库与 Demo，Avalonia 控件渲染 split 视图，
行级增删背景色，等宽字体，只读。跑通「粘贴 diff 文本 → 出界面」。

**内容**：

- 新建 `Banned.CodeDiff.Avalonia` 项目（net10.0，与核心库一致）与配套 Demo 桌面工程，
  加入 `Banned.CodeDiff.slnx`。
- 提供 diff 视图控件（split 双栏 + 行号列），数据源为核心库 `DiffFile` 的 split 行模型。
- 行级背景色：新增行 / 删除行 / 普通行区分；等宽字体；只读。
- Demo：文本框粘贴 diff 文本（可用 `tests/js-harness` 生成的 DemoData）→ 渲染出界面。
- 可选收尾：unified 单栏视图（核心模型已就绪，仅是渲染分支，放 M2 末尾或 M3 顺带）。

**验收**：

- [x] Demo 中粘贴 diff 文本即可看到 split 双栏视图。
- [x] 增/删行有正确背景色，行号与内容对齐，等宽字体，无编辑行为。
- [x] `dotnet build Banned.CodeDiff.slnx` Debug/Release 0 警告 0 错误，577 测试不回归。

**执行结果（2026-10-03）**：

- 新增 `Banned.CodeDiff.Avalonia` 控件库（`Views/DiffView`、`Models/DiffSplitRow` 行模型、
  `Models/DiffBrushes` 明暗两套画刷（对齐上游 `_base.css` 变量值）、
  `Utils/DiffSplitRowBuilder` 行构建、`Themes/Generic.axaml` ControlTheme）
  与 `Banned.CodeDiff.Avalonia.Demo`（粘贴 diff → 渲染、载入示例、深浅主题切换、统计栏）。
- `DiffView`：`DiffFile` 属性赋值即渲染（内部幂等调用 `Init`/`BuildSplitDiffLines`），
  订阅模型 `Updated` 自动刷新；行号列宽按最大行号位数自适应；`ActualThemeVariant`
  变化时换画刷重建行；默认等宽字体 `Menlo, Consolas, monospace`、字号 14。
- hunk 占位行（`@@` 头）按 `GetSplitHunkLine` 渲染，与 JS 槽位循环一致；
  键等于 `SplitLineLength` 的合成尾 hunk 不渲染（展开行属 M4）。
- 冒烟验证（含两次返工）：首轮"通过"为误判——引导式截图分析复述了预期；实际 DiffView
  空白、状态栏为空。根因一：**Avalonia 不自动加载控件库 `Themes/Generic.axaml`**，
  ControlTheme 必须由宿主显式 `StyleInclude`；根因二（用户实测反馈）：**StyleInclude 的
  目标必须是 `Styles` 根**——`ResourceDictionary` 根会触发构建期 AVLN2000
  （"expected Avalonia.Styling.IStyle"），而当时 `tail -2` 截断构建输出掩盖了该错误，
  "复验"实际跑的是旧二进制。最终结构：`Styles` 根 + `Styles.Resources` 内
  `ResourceDictionary` 包裹带 `x:Key="{x:Type ...}"` 的 ControlTheme（Fluent 主题同款）。
- 程序化回归门：新增 `tests/Banned.CodeDiff.Avalonia.Tests`（xunit.v3 +
  Avalonia.Headless.XUnit 12.1.0），TestApp 以消费方同款 StyleInclude 加载主题，
  断言模板实例化（ScrollViewer/ItemsControl + 13 项）、行模型结构与上游配色值
  （#dafbe1/#ffebe9）。该包依赖 xunit.v3 而非 xunit 2.x，注册用
  `[assembly: AvaloniaTestApplication(typeof(Builder))]` + 静态 `BuildAvaloniaApp()`。
- 最终复验（三证据链）：控制台探针（4/2/11）、构建零错误（输出全量检查）、中立提问
  截图分析（13 行双栏、@@ 行位于第 1/8 行、统计 `+4 -2 / split 11 行` 与探针一致）。
- unified 单栏视图已补齐（2026-10-03，应用户需求提前完成）：`DiffView.ViewMode`
  （Split/Unified）切换，unified 为单栏 + 双行号列、删除行在新增行上方（GitHub 布局）；
  新增 `Models/DiffUnifiedRow.cs` 与 `Utils/DiffUnifiedRowBuilder.cs`，行构建懒按当前模式。
  验证：unified 探针（13 内容行 + hunk 于 index 0/8）+ headless 断言（15 行、2 hunk、
  Delete=2/Add=4、模式切换重建）+ 中立截图分析（15 行、浅蓝行位于第 1/10 行、
  粉行行号 `4|空` 绿行 `空|4`，与探针逐位吻合）。Demo 加「Unified 视图」开关，
  统计栏同时显示 split/unified 行数（VM 构建双模型，控件侧幂等）。
  视觉模型曾把粉行数成 3（实际 2），已用程序化断言钉死——再次印证验证纪律的必要性。

## 5. M3 词级高亮

**目标**：配对的增/删行显示词级（字符区间）高亮，数据来自 `DiffLine.DiffChanges` /
`DiffLine.Changes`。

**内容**：

- **先 spike**：验证 Avalonia per-run 背景（`TextRun` 级背景或 `FormattedText` 按区间绘制）
  的可行性与性能，用万行级样本评估。
- spike 通过 → 实现 Segment 自绘渲染（按字符区间绘制背景，精确对齐等宽布局）。
- spike 不通过 → 记录证据与失败原因，评估备选方案（整行背景降级、GlyphRun 自绘等）
  后再定实现路径，不盲目堆补丁。

**验收**：

- [x] 配对增/删行的变更部分有词级背景高亮，与行级背景正确叠加。
- [x] 样例渲染正确（中立截图分析：`return 1/2` 行内 `1`/`2` 字符有嵌套深色块，
      `WriteLine` 行没有——与上游配对规则一致）。
- [x] Spike 结论：Avalonia 文本 Run 无背景属性（仅 Foreground），per-run 背景不可行；
      采用 `TextLayout` 自绘（`HitTestTextPosition(int)→Rect` 解析区间边界 x 坐标，
      `Draw(context, origin)` 绘制文本），矩形计算有独立单测。
- [x] 万行级性能基线 → 已随 M4 完成并记录（见第 6 节）。

**执行结果（2026-10-03）**：

- 新增 `Views/DiffSegmentText`（TextLayout 自绘：文本 + 词级高亮矩形，圆角 2px，
  缓存 layout、文本/字体/前景变化失效）、`Models/DiffHighlight`（区间 record）、
  `Utils/DiffHighlights`（区间提取：fast-diff 多段优先，`Changes` 单区间回退；
  增行取 Insert 段、删行取 Delete 段）、`DiffBrushes` 增补 `*-content-highlight` 色
  （浅 #aceebb/#ffcecb，深 #2f5732/#713431，对齐上游变量）。
- 关键移植事实：`DiffItem` 区间基于**含行尾换行的原始行文本**，`EndIndex` 为闭区间——
  高亮统一按 `[StartIndex, StartIndex+Length)` 计算并对显示文本钳制；
  `GetDiffRange` 仅在 hunk 内增删行数相等时整批配对（与 JS 上游一致）。
- Demo 加「词级高亮」开关（fast-diff 多段 ↔ 相对单区间，切换重建模型）。
- headless 测试扩至 11 个（区间断言、矩形计算、独立控件测量渲染、明暗画刷值）；
  顺带修复：核心测试项目 csproj 缺 xunit 框架包（此前跑在过期 restore 缓存上）、
  UI 测试改用 `xunit.v3.mtp-v2` 3.2.2 + `global.json` MTP runner 配置。
  （后续按用户要求整体切换 **NUnit 5.0.0** + `NUnit3TestAdapter`：NUnit 5 无 MTP 集成，
  移除了 `global.json`；`Avalonia.Headless.NUnit` 编译目标 4.5.1 但在 5.0.0 运行时实测兼容；
  两项目以 `[assembly: Parallelizable(ParallelScope.None)]` 保证串行。约定详见 AGENTS.md。）

## 6. M4 hunk 展开/收起 + 虚拟化

**目标**：收起 hunk 显示占位行，可展开（up / down / all，每步 40 行），大 diff（万行级）流畅。

**内容**：

- 展开状态机核心（up/down/all、步长 40、`Updated` 事件）M1 已移植完成，本里程碑做 UI 接线：
  占位行可点击，提供向上/向下/全部展开入口，`Updated` 后刷新可见行。
- 接 `VirtualizingStackPanel`（或 ItemsControl 虚拟化）渲染行模型，split 左右列同步滚动。
- 用万行级 diff 做性能测试（内存、滚动流畅度），记录基线数据供后续回归对比。

**验收**：

- [x] 占位行三种方向展开均生效，展开后行号与内容正确。
- [x] 万行级 diff 行虚拟化（仅实化可见行容器；滚动不触发整表重建——行列表只在
      展开/收起/模式切换时重建）。滚动流畅度的主观体验交由 Demo 人工验证。
- [x] 性能基线已记录（见执行结果）。

**执行结果（2026-10-03）**：

- **hunk 占位行展开接线**：`DiffSplitHunkRow` / `DiffUnifiedHunkRow` 增加 `HunkIndex`
  （传给 `On*HunkExpand` 的模型键）与按钮可见性标志；按钮摆放规则逐条移植上游
  `DiffSplitHunkLine*.tsx` 的 ternary 链（首个 hunk 单个 Expand Up、末尾合成折叠条单个
  Expand Down、剩余隐藏行 < 40 显示单个 Expand All、否则显示上下成对的 Down+Up）。
  `DiffView` 暴露 `ExpandHunkUpCommand` / `ExpandHunkDownCommand` / `ExpandHunkAllCommand`
  （`ICommand`，参数为 hunk 行），模板按当前模式转发到 `OnSplitHunkExpand` /
  `OnUnifiedHunkExpand`。展开按钮图标为上游 `DiffExpand.tsx` 的 SVG path 数据原样移植
  （已验证 Avalonia 12 `Geometry.Parse` 支持 SVG 弧线与隐式 lineto）。
- **上游可见性对齐（行为修正）**：hunk 行只在隐藏区间非空时渲染
  （`startHiddenIndex < endHiddenIndex`，纯 diff 模式下无 info 的 `@@` 头照常渲染）；
  由此首个 hunk 从第 1 行开始时其 `@@` 头不再显示——与 GitHub/上游一致，M2 遗留差异。
  尾部合成 hunk（键 = `SplitLineLength`/`UnifiedLineLength`）此前不渲染，现已作为
  底部折叠条渲染（空文本 + 单个向下展开按钮）。
- **展开后的原始行渲染**：`Diff == null` 的行此前一律按占位空单元格处理；展开显出的
  原始 gap 行（有行号有文本、无 DiffLine）现在渲染为普通 context 行（split/unified 两处）。
- **虚拟化**：模板 ItemsPanel 换 `VirtualizingStackPanel`；split 左右列本就是同一行
  Grid 的两半，同步滚动天然成立。
- **Demo**：新增「载入可展开示例」（`ExpandableSample` 生成带真实文件内容的合成样例，
  各 gap 大小覆盖全部按钮形态）与「全部展开 / 全部收起」工具栏按钮（`CanExpandHunks`
  随 `Updated` 刷新）。
- **测试**：UI 测试扩至 22 个（新增 `DiffExpandTests`：按钮摆放规则、三方向展开的行数/
  行号/占位行位移断言、首 hunk 向上全展、尾部折叠条、unified 展开、全部展开/收起、
  模板命令接线、纯 diff 无按钮、万行虚拟化与性能基线）。既有测试按新可见性规则更新
  预期。TestApp 补加载 FluentTheme（此前只有库主题，`ItemsControl`/`Button` 无
  ControlTheme，容器从不实例化——M2/M3 测试未覆盖到所以没暴露）。
- **性能基线**（headless、Debug、10,312 行模型 / 98 个折叠 hunk / 785 可见行）：
  模型 Init+Build 50 ms；行构建 36 ms；模板+首布局 1195 ms（首布局含主题/XAML 一次性
  JIT 开销）；**实化容器 25 / 785**（视口 800×600）。
- 顺手修复：展开会改变行数导致 ScrollViewer 视口跳变，展开锚定（点击行保持在视口内）
  留到 M6 打磨。

## 7. M5 语法高亮

**目标**：接入 TextMateSharp，按文件语言做语法着色。

**内容**：

- 语言探测沿用核心库 `DiffTool.GetLang`，映射到 TextMate 语法。
- 跨行语法状态处理：hunk 边界处的上下文延续策略（对齐 JS 原版行为）。
- token 颜色对接 `Utils/HighlightColors`（`color.ts` 移植）与主题。

**验收**：

- [x] 常见语言（C#/TS/JSON 等）Demo 正确着色。
- [x] 语法高亮与词级/行级高亮正确叠加，不破坏 M4 性能基线。
- [x] NOTICE 补充 TextMateSharp 条目。

**执行结果（2026-10-03）**：

- **核心库语法链路**：`Models/IDiffHighlighter`（对应上游 `DiffHighlighter` 接口形状）、
  `Utils/HighlightAst`（`processAST` 逐行移植：多行 text 节点拆行、含端区间、空段语义）、
  `SourceFile.DoSyntax`（2000 行熔断 + 幂等 + 引擎回退）、`DiffFile.InitSyntax/InitTheme/
  GetOldSyntaxLine/GetNewSyntaxLine/GetHighlighterName`，`Init()` 补齐 initRaw+initSyntax。
- **内置 TextMate 引擎**（`Services/TextMate/`）：TextMateSharp **2.0.4**（1.x 的 OnigSharp
  不支持 csharp 语法的 lookbehind 正则，2.x 换 Onigwrap 后解决）；`ScopeThemeMatcher` 为
  vscode-textmate 主题匹配的直接移植——TextMateSharp 自带 `Theme.Match` 对 scope 栈的
  后代选择器有错配 bug（`source.cs` 命中 `string … embedded source`），不可用；着色语义
  为「栈内自顶向下首个命中层，未命中层继承外层」（vscode metadata 通道），默认前景取
  主题 `colors["editor.foreground"]`；相邻 token 以「颜色 + standardTokenType 相等」合并，
  对齐 vscode 二进制 tokenizer 的合并行为；输出 style 双主题变量（dark 在前、大写 hex），
  与 shiki `codeToHast` 输出逐字符一致。
- **嵌入资源**：`Resources/TextMate/`（29 个 grammar + 2 个主题，提取自
  @shikijs/langs/@shikijs/themes——主语言 + 依赖 grammar，vue 依赖 html-derivative/
  vue-directives 等）。提取脚本对 TextMateSharp 做了两处规整：capture 字符串简写包
  `{name}`、负向 `while:"^(?!X)"` 转 `end:"(?=X)"`（正向 while 如 `///` 续行块无等价
  end，规则删除，样例不触发）；TextMateResources 动态枚举嵌入 grammar 建语言表
  （name+alias → scope）。
- **黄金基准**：`build-syntax.mjs` + `gen-syntax.ts`（esbuild external shiki，wasm 在 node
  运行时解析）用真实 shiki + 上游 `processAST` 生成 `syntax-golden.json`（C#/TS/JSON/Vue
  四用例，含块注释、插值模板、正则、转义字符串）。C# `SyntaxGoldenTests` 逐字段对比：
  **447 个 span 中仅 3 行存在 TextMateSharp↔vscode-oniguruma 引擎级差异**
  （C# 插值串 capture 边界、vue `<style>` 内 CSS 类选择器被误判 invalid），以显式豁免
  清单钉住（计数断言防新增退化），其余全绿。
- **Avalonia 渲染**：行模型携带 `SyntaxRuns`（`DiffSyntaxRuns.Extract`：解析双主题变量、
  按当前 ThemeVariant 取色、钳制到去换行显示文本、相邻同色合并、>150 span 降级）；
  `DiffSegmentText` 每语法段独立 TextLayout，x 坐标取整行 layout 的
  `HitTestTextPosition`（词级高亮矩形计算不变，叠放于语法色之下）；`DiffView`
  增 `SyntaxHighlight`（默认开）与 `Highlighter`（注入引擎）。
- **Demo**：语法开关、粘贴模式从 diff 头提取文件名（核心库不解析 diff 头，与 JS 一致，
  由调用方提供；Demo 层便利）、C#/TS/JSON 三语言样例循环（真实新旧文件内容 + git diff
  生成）。
- **验证**：589 核心 + 28 headless 全绿；Demo 实机截图中立提问视觉验证（C#/TS/JSON/
  深色 5 张：多色着色与代码结构吻合、深色可读、词级高亮叠加正常；视觉模型个别行号
  描述有误，以程序化断言为准）。known-divergence 与 JS 行为差异（无 lowlight 回退：
  不支持语言保持纯文本）记录于 CHANGELOG。

## 8. M6 打磨

**目标**：可用性收尾。

**内容**：

- `ThemeVariant` 深浅主题（颜色体系对齐上游 theme）。
- wrap 模式（长行换行渲染，注意与虚拟化行高的配合）。
- 复制功能（选中行/文件内容复制）。
- `multiSelect` / widget 视需求决定是否移植（上游 `packages/core/src/multiSelect/*`）。

**验收**：

- [x] 深浅主题切换正确，颜色符合设计（light/dark 画笔逐项核对上游 `_com.css` 的
      `--diff-*--` token，既有值全部一致；唯一修正是展开揭示行改用
      `--diff-expand-content--`，批次 1a）。
- [x] wrap 开关生效且不破坏滚动与虚拟化（headless 实证：extent 随实现行高度精化，
      滚到底部/中部目标行可见、不丢行，仍只实化可见切片）。
- [x] 复制功能可用（选区行/新旧整文件复制到剪贴板，Demo 含 Ctrl+C 宿主接入示例）。
- [x] 决定 multiSelect/widget 的去留并记录结论（见下方执行结果）。

**执行结果（2026-10-04）**：

- **主题颜色对齐上游（批次 1a）**：`DiffBrushes` 的 light/dark 两套画笔逐项核对上游
  `_com.css` 的 `--diff-*--` token（react/vue/solid 一致），既有值全部一致。唯一修正：
  展开 hunk 揭示的原始行（无 `DiffLine`）此前沿用 plain context 背景，现改用上游
  `--diff-expand-content--`（light `#fafafa` / dark `#161b22`）。
- **展开后视口锚定（批次 1b，M4 遗留）**：展开命令先记录锚点（点击的占位行索引、滚动
  偏移、占位行与相邻内容行的实化高度），`DiffFile.Updated` 重建行后按各方向的实际插行
  几何补偿 `ScrollViewer.Offset`（Up 存活时偏移不动；Up 全揭示/All 时偏移 += 揭示高度 −
  占位行高；Down 时偏移 += 插入高度），偏移设置延迟到 extent 更新后应用以避开
  coerce 钳制。行为对标上游 web 端依赖的浏览器 scroll anchoring，消除展开后视口跳变。
- **行选择（批次 2，上游 multiSelect 移植）**：`DiffView.IsSelectionEnabled`（默认
  false，opt-in）开启后，从行号列拖拽即按 GitHub 风格选择行区间（split 锁定起始侧、
  拖拽悬停内容行也延伸；unified 悬停行号区才延伸；context 行双侧高亮）。公开 API：
  `SelectionChanged` / `SelectionCompleted` 事件、`GetSelectionResult()` /
  `GetSelectionState()` / `ClearSelection()` / `SetPreselectedLines(oldLines, newLines)`。
  隐藏行保留在选区数据里但不高亮，展开后自动补齐；新拖拽清空上次选区。
- **复制（批次 3，原生新功能，上游无对应实现）**：`DiffView` 新增
  `CopySelectionCommand` / `CopyOldFileCommand` / `CopyNewFileCommand` 与
  `CopySelectionAsync()` / `CopyOldFileAsync()` / `CopyNewFileAsync()`。选区复制 =
  用户所见（跳过隐藏行、去尾换行、`\n` 连接，核心库
  `MultiSelectData.GetSelectedTextFromResult`）；整文件复制原样传出新旧文件内容。
  不内置键盘快捷键（避免与宿主绑定冲突），由宿主/Demo 自行绑定（Demo 绑 Ctrl+C）。
- **wrap（批次 4，上游 diffViewWrap 移植）**：`DiffView.Wrap`（默认 false，opt-in——
  上游包装层默认开启，端口不沿用）开启后长行在视图宽度处换行，不破坏虚拟化（行高随
  内容增长）；行号列定宽不参与换行；split 同一行左右 cell 等高（取较高者）。换行策略
  用 Avalonia `TextWrapping.Wrap`（词级断行），对应浏览器 `pre-wrap` 的可见效果。
  跨行的词级高亮矩形按行分段（对齐浏览器 inline box 背景行为）。
- **Demo**：新增「启用行选择」「自动换行」开关、三个复制按钮（选中行随选区状态可用）
  与选区状态栏反馈（「已选 N 行(old 12-34)」/「已复制 N 行」）。
- **multiSelect / widget 去留结论**（验收硬性要求）：
  - **已移植——选择语义**：数据层逐结构对应进核心库（`Models/MultiSelectModels.cs` ↔
    `multiSelect/types.ts`，`Utils/MultiSelectData.cs` ↔ `multiSelect/data.ts`，含
    `dom.ts` 的 `normalizeRange`、`visual.ts` 的 `changePreselectedLinesToLineRange`
    两个纯函数）；状态机（`manager.ts`）以 Avalonia 指针事件重实现
    （`Services/DiffSelection.cs`，`DiffSelectionDom.cs` 为 dom.ts DOM 契约的视觉树
    等价物）；视觉（`visual.ts` + `_com.css` 选区配色）用行模型标志位 + 画刷实现。
  - **不移植——DOM 契约与宿主业务**：dom.ts/visual.ts 的 DOM 形态分支（wrap/nowrap
    双 tr 等，以模型行 + side 为键替代）、`extendData` 评论流适配
    （`extendDataToPreselectedLines`）、`scopeToHunk` 钩子（上游默认恒等函数）、
    四套框架的包装组件（react/vue/solid/svelte 的 `DiffViewWithMultiSelect`——其职责
    在端口里由 `DiffView` 的指针路由与事件承担）。
  - **「widget」结论**：实指上游「+」评论按钮（`diff-add-widget`）与 `renderExtendLine`
    评论流，属宿主业务而非 diff 渲染，**不移植**；宿主可经 `SelectionCompleted` 事件
    自行扩展等价功能。
- **基线**：614 核心 + 67 headless 测试全绿，Debug/Release 构建 0 警告 0 错误。

## 9. 通用验收（每个里程碑）

- `dotnet build Banned.CodeDiff.slnx` 与 `-c Release` 均 0 警告 0 错误。
- 全量测试绿，无回归。
- Demo 可演示该里程碑功能。
- `README.md` / `Docs/README.zh-CN.md` / `Docs/CHANGELOG.md` 同步更新。
- 新增上游衍生代码时同步更新 `NOTICE`。

## 10. 首发支持范围与待决事项

- **NuGet 包**：首发同时发布 `Banned.CodeDiff` 与 `Banned.CodeDiff.Avalonia`，版本均为 `0.1.0`；
  Avalonia 包是控件用户的直接入口，并依赖核心包，核心包也可单独用于 diff 解析与数据处理。
- **.NET 支持范围**：两个包首发均以 `net10.0` 为目标框架。
- **diff 与视觉范围**：核心库处理 unified diff 文本及 Git diff 常见扩展；Avalonia 控件首发提供
  当前 GitHub 风格的布局约定和明暗配色。产品定位不限定于 GitHub 视觉风格，其他预设样式可在
  后续版本扩展，首发不承诺额外样式。
- **版本记录**：首发版本为 `0.1.0`；发布时从 `Unreleased` 整理首发用户可见的变更记录。
- README/文档中的 GitHub 链接按 `banned2054/Banned.CodeDiff` 预填，待确认。

## 11. M7 行范围评论与主题定制入口（下一优先级，规划中）

**目标**：让宿主应用能对任意可见 diff 行或连续行范围发起评论，并在 diff 中显示行内评论内容；
同时提供少量、稳定、便于 XAML 使用的配色定制入口。

**设计基线**：

- 以现有行号拖选为基础：点行号形成单行选区，拖动形成连续行范围；新增、删除、上下文行都可作为锚点。
- 保留现有选区高亮作为选择反馈；选区完成后由显式的“添加评论”操作进入评论流程，普通选区完成本身不自动弹出编辑器。
- 评论锚点包含文件身份、旧/新侧别与起止行号；删除行锚定旧侧，新增行锚定新侧，上下文行保留所选侧信息。
- `DiffView` 提供宿主可订阅的评论请求/锚点数据，并提供行内评论内容的呈现扩展点。评论草稿、提交、回复、删除与持久化由宿主负责。
- 评论锚点的持久高亮与当前多选状态分开保存，避免下一次拖选清除已有评论标记；视图模式切换及 hunk 展开后仍按稳定锚点重新定位。
- 公开一个统一的语义配色入口（优先评估 `DiffPalette` 属性与可覆盖的 XAML 资源键），覆盖增删行、上下文行、选区和评论卡片的主要颜色，不为每个内部画刷单独增加控件属性。
- 本阶段不做任意字符/子串选择；`DiffSegmentText` 的自绘文本选择另列后续评估项。

**实现顺序**：

1. 明确单行/范围锚点事件与“添加评论”入口，复用 `SelectionCompleted` 数据，避免与复制/普通选区操作冲突。
2. 扩展 `DiffRow` 呈现评论卡片，覆盖 split 与 unified 两种布局，并支持宿主提供评论内容。
3. 添加独立于多选状态的评论锚点与持久高亮，处理隐藏行、hunk 展开和视图模式切换。
4. 暴露统一配色定制入口，并在 Demo 中演示宿主接入。

**验收**：

- 单行点击与连续多行拖选均能产生正确的旧/新侧锚点；选区可见高亮，评论入口由用户显式触发。
- 评论卡片能显示在锚定范围附近；新增、删除、上下文行均可评论，split/unified 切换后锚点不漂移。
- 已创建评论的锚点高亮不受后续普通选区替换影响；折叠/展开 hunk 后按行号恢复可见位置。
- 宿主可以通过统一配色入口覆盖主要语义颜色；默认主题外观保持现有值。
- Demo 展示完整的客户端评论接入；核心库 diff 解析与原始行号语义保持不变。

---

# 历史计划：M1 纯逻辑库（已完成，2026-10）

## 目标

不依赖 UI，把 git-diff-view core 的解析与行模型逻辑逐行移植到 C#，行为由黄金测试保证。

## 执行结果

- [x] 移植 `parse/`（diff-parse / diff-line / raw-diff / transform / change-range / diff-tool）
      与 `diff-file.ts` / `diff-file-utils.ts` / `escape-html.ts` / `file.ts`。
- [x] 移植 `packages/utils` 的 `symbol.ts` / `color.ts` / `highlightAST.ts`（类型）。
- [x] 移植 `fast-diff@1.3.0` 为 `Services/FastDiff.cs`（含递归护栏）。
- [x] `parse/template.ts` 的 HTML 模板不移植，仅保留其全局开关 `TemplateOptions`。
- [x] split / unified 行模型 + hunk 展开/收起状态机。
- [x] 目录重组为 `Models` / `Services` / `Utils`，命名空间统一。
- [x] 577 个测试全绿；`tests/js-harness` 用 esbuild 打包 JS 原版源码 + 真实 fast-diff
      生成 golden.json 做逐字段对比。
