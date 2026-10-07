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
| M7 | 行范围评论与主题定制入口 | ✅ 已完成（2026-10-06） |
| M8 | Diff/语法配色分层、主题预设与局部覆盖 | ✅ 已完成（2026-10-06） |

每个里程碑保持可构建、可测试、可演示；完成一个更新一次本文件状态与 `Docs/CHANGELOG.md`。

## 3. 当前状态

- `Banned.CodeDiff` 核心库完成：解析（`DiffParser`）、split/unified 行模型（`DiffFile`）、
  词级区间（`ChangeRange` / `FastDiff`）、展开状态机、`TemplateOptions` 全局开关。
- 目录已按 `Models` / `Services` / `Utils` 分类。
- 当前回归基线为 624 个核心测试 + 90 个 Avalonia headless 测试全绿（Debug/Release 构建 0 警告 0 错误），
  含与 JS 原版逐字段对比的黄金基准，以及与真实 shiki 引擎逐字段对比的语法黄金基准。
- `Banned.CodeDiff.Avalonia` 已就位：`DiffView` 控件（split/unified 视图、词级高亮、
  语法高亮、hunk 展开/收起 + 虚拟化、明暗主题、行选择、复制、长行 wrap）与配套 Demo，
  详见第 4~8 节执行结果。
- M7 已完成（2026-10-06）：行范围评论（锚点事件 + 评论卡片 + 持久高亮）与初版语义配色
  入口 `DiffView.Palette`，见第 11 节执行结果。
- M8 已完成（2026-10-06）：Diff/界面与语法两套调色板分离、GitHub/Codex/Monokai/Visual Studio
  预设、逐槽位覆盖优先级（宿主覆盖 → 独立预设 → 组合预设 → GitHub 基线）、TextMate token
  scope 保留与逐 scope 语法覆盖，见第 12 节执行结果。任意字符/子串选择
  （`DiffSegmentText` 自绘文本选择）仍未实现，颜色槽位已在 M8 预留。

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
  vue-directives 等）。提取脚本对 TextMateSharp 做两处规整：capture 字符串简写包
  `{name}`；begin-only 规则（缺 end/while）补永不匹配的 end。begin/while 规则**原样保留**
  ——TextMateSharp 2.x 原生支持 while（初版脚本误判不支持而丢弃/转换，曾导致 C# `//`、
  `///` 注释整族规则失效，2026-10-07 复审修复，见 M8 复审修复记录）；TextMateResources
  动态枚举嵌入 grammar 建语言表（name+alias → scope）。
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

## 11. M7 行范围评论与主题定制入口（已完成，2026-10-06）

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

- [x] 单行点击与连续多行拖选均能产生正确的旧/新侧锚点；选区可见高亮，评论入口由用户显式触发。
- [x] 评论卡片能显示在锚定范围附近；新增、删除、上下文行均可评论，split/unified 切换后锚点不漂移。
- [x] 已创建评论的锚点高亮不受后续普通选区替换影响；折叠/展开 hunk 后按行号恢复可见位置。
- [x] 宿主可以通过统一配色入口覆盖主要语义颜色；默认主题外观保持现有值。
- [x] Demo 展示完整的客户端评论接入；核心库 diff 解析与原始行号语义保持不变。

**执行结果（2026-10-06）**：

- **评论锚点与显式入口**：新增 `Models/DiffCommentAnchor`（文件身份 + `SplitSide` + 1 基含端点
  起止行号，`Normalize()` 容错反向区间）、`Models/DiffComment`（锚点 + 作者 + 正文）与
  `Models/DiffCommentRequestedEventArgs`。`DiffView` 新增 `FilePath`（宿主提供的文件身份，
  控件自身不解析文件名）、`GetCommentAnchor()`（从当前选区推导锚点——选区归一化区间的侧别
  与行号，统一视图侧别沿用上游“新号优先”规则）与显式入口 `BeginCommentCommand`
  （CanExecute 跟随选区，与复制命令同一失效通道；Execute 推导锚点并引发 `CommentRequested`）。
  普通选区完成不会自动触发评论流；指针管线零改动，与复制/选区操作天然无冲突。
- **评论卡片行**：新增 `Models/DiffCommentRow`（抽象，携锚点、评论列表、卡片画刷）及
  `DiffSplitCommentRow`（internal `CardColumn` 1/4 供主题 `Grid.Column` 绑定，卡片落在锚点侧
  内容列，另一侧留空——两个空星号列仍平分剩余空间，卡片与上方内容列对齐）与
  `DiffUnifiedCommentRow`（卡片横跨内容列）。`RebuildRows` 末尾经 `InsertCommentRows` 把每个
  锚点的卡片插到其范围内最后一个可见行之后（同一锚点多条评论聚合一卡；全部行被折叠时不出卡，
  展开揭示后随重建自动重现）。锚点按行号稳定定位：视图模式切换与 hunk 展开/收起均由重建
  重推位置，无需宿主干预。
- **持久高亮**：`DiffSplitCellModel`/`DiffUnifiedContentRow` 增加 `IsCommented`（INPC）与
  `CommentOverlay` 画刷，模板在选区覆盖层之下叠加评论覆盖层。`ApplyCommentVisual` 与选区
  视觉同构但走独立标志位与独立追踪列表——新拖选替换选区不清除评论标记；评论语义只标锚点
  侧（split 配对行的另一侧不标，与选区“上下文双侧高亮”不同）。
- **统一配色入口**：评估结论——选 `DiffPalette` 属性而非 XAML 资源键：行画刷在构建期烘进
  行模型，palette 变更直接走既有重建管线（每控件实例生效），变体处理显式（资源键方案需在静态
  `DiffBrushes` 里做运行时资源探测，且资源变更不触发重建，语义含糊）。`Models/DiffPalette`
  （`Light`/`Dark` 两套 `DiffPaletteColors`）8 个槽位：AddLineBackground/DeleteLineBackground/
  ContextBackground（设置时同时覆盖该类行的行号格与内容格）、SelectionHighlight/SelectionEdge、
  CommentLineHighlight/CommentCardBackground/CommentCardBorder；`null` 槽位保留内置上游值，
  词级高亮/hunk/展开行等衍生色不纳入。`DiffBrushSet` 增三个评论画刷位（内置值：浅色
  #fff8c5@55% 覆盖层 + #f6f8fa/#d1d9e0 卡片，深色 #d29922@28% + #151b23/#3d444d），
  `DiffBrushes.Get(variant, palette)` 以 record `with` 覆盖。
- **Demo**：工具栏「添加评论」按钮直绑 `#DiffView.BeginCommentCommand`；code-behind 桥接
  `CommentRequested` → VM 打开编辑面板（标题显示侧别与行号区间）；提交把 `DiffComment` 加入
  宿主持有的列表并重新赋值 `Comments`（集合原地变更不刷新——视图按属性变更重建，README 已
  注明该契约）；另有取消与「清除全部评论」。VM 暴露 `CurrentFilePath` 绑定 `DiffView.FilePath`。
- **测试**：新增 `DiffCommentTests`（13 个：锚点推导/命令与事件/卡片位置与侧别列/持久高亮
  存活于新选区/模式切换重定位/折叠不显卡展开恢复/集合变更/模板实化）与 `DiffPaletteTests`
  （4 个：默认值不变/增删上下文覆盖/选区与评论槽位/深色变体）。测试数据延续语义反转防护：
  断言新侧锚点不标配对行的删除格（选区式双侧高亮会在此翻车）。基线：624 核心 + 90 headless
  全绿，Debug/Release 0 警告 0 错误。
- **文件身份隔离(用户评审反馈修复)**:评论渲染按 `Anchor.FilePath == DiffView.FilePath`
  过滤——`InsertCommentRows` 与 `ApplyCommentVisual` 同口径,`FilePath` 属性变更亦触发重建,
  切换文件不会把评论串到其他文件的同号行上(`Comments_FilePathMismatch_DoNotRender` 钉住:
  同侧同号不同文件的两条评论只有当前文件的一条渲染,切换身份后另一条接管)。
- **Demo 命令状态修复(用户评审反馈)**:`OnCommentRequested` 打开编辑器后未引发
  `RaiseCanExecuteChanged`,首次打开时提交/取消按钮停留在禁用态;取消/提交后也同源地缺通知
  (提交后编辑面板不关闭)。统一收敛为 `ClosePendingComment` /
  `RaiseCommentCommandsCanExecuteChanged`,并在 `DiffFile` 切换时作废过期的评论请求。
- **排坑记录**：`GetLayoutManager` 扩展位于 `Avalonia.VisualTree` 命名空间（Avalonia 12），
  测试文件缺 `using Avalonia.VisualTree;` 时报 CS1061；且在 `Banned.CodeDiff.Avalonia.*`
  命名空间内，代码中的裸标识符 `Avalonia` 优先解析为 `Banned.CodeDiff.Avalonia`（库命名
  空间遮蔽框架命名空间），全限定 `Avalonia.Styling.X` 会翻车，`using Avalonia.Styling;` 则
  正常——两类场景行为不一致，新测试文件建议照抄既有测试的 using 集合。

## 12. M8 Diff/语法配色分层、主题预设与局部覆盖（已完成，2026-10-06）

**目标**：在保留 GitHub 当前外观的基础上，提供可组合的 Diff/界面配色与语法高亮配色，
内置 GitHub、Codex、Monokai、Visual Studio 等预设，并允许宿主通过 XAML Style 只覆盖少数颜色。
配色体系先行；本阶段不实现字符/子串选择交互。

**设计基线**：

- **两套独立调色板**：
  - Diff/界面调色板负责画布背景、增删与上下文行、行号区、行内增删强调、分隔线、hunk、评论、
    行选择与文字选择等控件视觉状态。浅/深变体分别定义。
  - 语法调色板只负责普通文本与语法 token 的前景色（如注释、关键字、字符串、数字、类型、函数）；
    不得隐式覆盖画布或 diff 行背景。
  - 一个组合主题预设可以同时指定两套调色板，供宿主一键使用；两套预设仍可独立选择和覆盖，
    例如 Monokai 语法色搭配 GitHub Diff 行背景。
- **Diff 语义槽位**：至少区分新增/删除/上下文行背景及行号背景、行内新增/删除片段强调、
  选中新增/删除/上下文行背景、行号前景、画布/分隔线/hunk/展开行与评论颜色。选中行使用明确的
  按行类别配色，不依赖通用半透明覆盖层与红/绿底色混合出的不确定结果。
- **字符选择槽位**：预留文字选择背景与可选前景色。文字选择前景默认保留语法色；只有宿主显式设置
  覆盖色时，才统一改写所选字符的前景。M8 只定义颜色语义与优先级，不实现字符命中、拖选、键盘选择
  或复制行为。
- **语法 scope 保真**：当前 TextMate 分词已取得 token scope，但输出 AST 前会按 GitHub 最终颜色合并，
  scope 随之丢失。调整为保留原始文本区间与 scope，按当前控件的语法预设匹配主题颜色、应用宿主覆盖，
  最后再合并同色相邻区间绘制。复用现有 `ScopeThemeMatcher`；不可用已合并的最终颜色反推 token 类别，
  也不可通过修改共享高亮器全局主题实现每控件配色。
- **预设与覆盖层级**：项目默认值 → 组合预设 → 独立 Diff/语法预设 → 宿主局部覆盖，逐语义槽位解析。
  切换预设保留宿主已设覆盖；清除某项覆盖后回退到当前预设值。共享预设对象不可被宿主修改，主题状态按
  `DiffView` 实例隔离。
- **XAML Style 局部覆盖**：公开独立预设与覆盖入口，使 Style 可以选择组合预设并只提供少量非空覆盖项，
  未覆盖槽位继续继承预设值。普通嵌套对象赋值不会自动逐属性合并；设计时应采用逐槽位可设属性或可合并的
  专用 override 集合，不要求宿主复制整套调色板。现有 `DiffView.Palette` 的 M7 用法作为 Diff 覆盖入口
  保留或提供清晰迁移方式；复杂语法 scope 规则走独立语法覆盖集合。
- **候选预设**：GitHub 预设作为当前视觉与黄金回归基线；另评估 Codex、Monokai、Visual Studio 风格。
  Codex 颜色需依据用户认可的实际截图/参考确定；Visual Studio 需注明所参考的主题版本。预设明确支持的
  明暗变体；没有浅色版本的主题需明确回退行为，不得简单反转颜色伪造。

**实现顺序**：

1. 冻结语义槽位、组合/独立预设关系、明暗变体和局部覆盖优先级；确定新增公开 API 与现有 `Palette` 的衔接。
2. 扩展 Diff 画刷解析与行模型，覆盖增删/上下文、行内强调、行类别选中状态、行号与评论等槽位；确保
   选中行背景、评论标记和词级差异强调有稳定绘制顺序，且不会意外改写语法前景。
3. 调整 TextMate 高亮数据通路以保留 scope；在 `DiffView` 实例范围内选择语法主题并应用局部 scope 覆盖，
   保持核心解析与既有黄金输出语义不变。
4. 添加 GitHub、Codex、Monokai、Visual Studio 预设及按槽位合并逻辑；提供 XAML Style 选预设和局部覆盖的
   消费示例。
5. 在 Demo 中分别切换 Diff 与语法预设，并演示组合预设后只覆盖一个/少量颜色；同步双语 README 与
   CHANGELOG。

**验收**：

- [x] Diff/界面预设与语法预设可独立组合；组合预设提供便捷入口但不耦合两套配置。
- [x] Diff 各行类别、行内增删强调、选中行状态、浅/深背景、评论等均可按语义槽位定制；GitHub 默认外观
      保持现有值。
- [x] 语法配色保留 scope 粒度；至少验证注释、关键字、字符串、数字、类型/函数等类别可由预设着色，宿主
      可以覆盖单个 scope 而不重写整套主题。
- [x] 覆盖优先级稳定：局部覆盖胜过独立预设，独立预设胜过组合预设，组合预设胜过项目默认；清除覆盖恢复
      当前预设值；不同 `DiffView` 实例互不影响。
- [x] 字符选择的颜色槽位与行选择分别定义；M8 不宣称已支持字符选择交互。
- [x] Demo 演示 GitHub/Codex/Monokai/Visual Studio 组合与独立搭配，并能通过宿主 Style 覆盖少数槽位。
- [x] TextMate/核心黄金测试保持原有行为；新增 Avalonia 测试覆盖各状态背景、语法 scope 覆盖和层级合并。

**执行结果（2026-10-06）**：

- **两套独立调色板与解析链**：`DiffThemeContext`（internal record，行构建期只读）承载变体 +
  宿主 `DiffPalette` 覆盖 + 组合/独立预设 + 语法覆盖。`DiffBrushes.Get(variant, palette,
  themePreset, diffPreset)` 逐槽位解析：宿主细粒度槽位 → 宿主 M7 粗槽位（仅宿主层）→ 独立
  Diff 预设 → 组合预设 → GitHub 基线（黄金锁定的上游值,全值兜底）。`DiffThemePresets`
  （internal static）按预设 × 变体持全值 `DiffPaletteColors`;预设实例进程级共享只读,宿主
  永不触碰,主题状态按 `DiffView` 实例隔离。
- **`DiffPaletteColors` 扩槽位**：画布背景（经 `DiffView` 应用到控件 Background,GitHub 基线
  为 null 不触碰宿主背景,清除预设后回收自己设置的画布）、行号前景、增删/上下文行的行号格与
  内容格分离背景、展开行、hunk 三槽、分隔线、词级增删强调、`SelectedAdd/Delete/Context
  Background`（模板选中覆盖层改绑按行类别解析的 `SelectedBackground`;GitHub 基线回落到 M7
  通用覆盖层——同一画刷实例,M7 外观与 API 语义不变）、预留 `TextSelectionBackground/
  Foreground`（M8 仅定义槽位）。
- **语法 scope 保留**：`TextMateHighlighter.Tokenize` 在同色合并时保留每个原始 token
  （`SyntaxNodeProperties.Tokens`:wrapper 内区间 + scope 栈数组拷贝,防 TextMateSharp 复用
  存储）——GitHub 内置主题下的合并行为与黄金输出零变化（624 核心测试含 SyntaxGolden 全绿）。
  渲染端 `DiffSyntaxRuns.Extract` 增加可选 `DiffSyntaxColors` 解析器:非空时逐 token 经
  `ScopeThemeMatcher` 重新匹配前景,否则直接用 wrapper style 最终颜色（零开销）。默认
  (GitHub 且无覆盖)时 `DiffSyntaxColors.Create` 返回 null,完整走原通路。
- **`DiffSyntaxColors`（per-view 解析器,Models/）**：按 (语法预设 ?? 组合预设语法侧, 变体)
  取内置主题 matcher(共享只读,经公开门面 `DiffSyntaxThemes.GetMatcher`);宿主 scope 规则
  合成为 vscode-textmate 主题 JSON 走同一条 `ScopeThemeMatcher.FromThemeJson` 流水线（不另起
  匹配实现）;`MatchForeground` 优先级:覆盖规则 → 预设规则 → 宿主默认前景覆盖 → 预设默认
  前景回退。无效颜色值（非 #RGB/#RGBA/#RRGGBB/#RRGGBBAA）被忽略,全部无效视为未定制。
- **预设值**：GitHub=黄金基线(上游 `--diff-*--` + github-light/dark);Monokai=VS Code 内置
  Monokai 语法色 + 基于 #272822 的派生 diff 行背景,仅深色;Visual Studio=Dark+/Light+ 语法色
  + VS Code git diff 行色派生背景,明暗都有;Codex=初版占位（plan.md 候选预设事项:待用户认可
  的实际截图/参考校准）,仅深色。无浅色变体的预设回退 GitHub 对应变体,不做颜色反转。
- **`DiffView` 新属性**：`ThemePreset` / `DiffPreset` / `SyntaxPreset` /
  `SyntaxOverrides`（全部可空,赋值重建行,与 Comments/Palette 同一失效通道）;`Palette`
  语义更新为解析链顶层宿主覆盖（M7 用法不变）。模板选中覆盖层改绑 `SelectedBackground`。
- **`ScopeThemeMatcher` 公开化**：类转 public 并新增 `MatchRuleForeground`（不回退主题默认
  前景,供覆盖链区分「规则命中」与「主题默认」）;核心库新增公开门面 `DiffSyntaxThemes`
  （常量主题名 + `GetMatcher`）,控件库不直接触碰 internal 的 `TextMateResources`。NOTICE
  无新增上游(monokai/vs 主题 JSON 为本项目按 VS Code 内置主题手写的等价 tokenColors 子集)。
- **Demo**：新增预设工具栏行——组合主题 ComboBox(GitHub/Codex/Monokai/Visual Studio)、
  Diff/语法独立 ComboBox(跟随组合/GitHub/Codex/Monokai/Visual Studio)、
  「覆盖新增行背景(单槽位)」与「覆盖注释色(单 scope)」开关,状态栏反馈当前预设与覆盖组合。
- **测试**：新增 `DiffThemePresetTests`（15 个:GitHub 基线同一性、独立预设压组合、宿主覆盖
  压预设且其余槽位保持预设值、清除覆盖回落当前预设、细槽位压 M7 粗槽位、Monokai/Codex 浅色
  回退、选中三槽基线回落覆盖层、画布/文字选择槽位、语法 resolver 语义(未定制 null、覆盖规则
  压预设规则、默认前景不重写规则命中、覆盖跨预设存活、无效值忽略、Monokai 浅色回退 github-
  light)与块注释行逐主题重着色的 builder 级集成）与 `DiffPresetViewTests`（6 个:预设切换重建
  行、深色变体值、画布应用/回收、独立 Diff 预设保持 GitHub 语法侧+Monokai 行背景、语法覆盖
  重建且只改目标 scope、选中背景进模板实化矩形、双实例隔离）。基线：624 核心 + 112 headless
  全绿，Debug/Release 0 警告 0 错误。Demo 实机验证：切换组合预设到 Visual Studio 后关键字
  变紫、hunk 头换 VS 底色、状态栏反馈(截图目视 + 程序化 headless 断言双证据)。
- **已知引擎差异（非本次引入）**：TextMateSharp 对 C# 行注释（`//`）的分词与 vscode-oniguruma
  不同（`//` 被分为 keyword+text 而非 comment,块注释 `/* */` 正确）——M5 黄金的 3 行豁免
  之外的又一种引擎级差异,渲染层无法修复;预设/覆盖管线按 TextMateSharp 实际产出着色。后续可
  评估升级 TextMateSharp 或对行注释加 port 侧修正。
  （2026-10-07 更正：该差异并不存在——`//` 被误分词是提取脚本丢弃 begin/while 规则所致,
  TextMateSharp 2.x 经 `BeginWhileRule` 原生支持 while;语法包恢复原规则后行注释正确,见下方
  2026-10-07 复审修复记录。）

**复审修复记录（2026-10-07,上一轮 11 个复现场景通过后的 4 项 P2 + M5 注释）**：

- **P2 选中色解析链（`DiffBrushes.Selected`）**：按行类别的选中背景此前只查宿主细槽位、
  之后直接进预设层——违反「宿主覆盖优先」,也放任组合预设的细槽位穿透独立 GitHub 预设。
  现在的顺序：宿主细槽位 → 宿主粗槽位 `SelectionHighlight` → 独立预设细槽位 → 其通用
  `SelectionHighlight` → 组合预设细槽位 → 其通用值 → GitHub 基线粗覆盖层（同一画刷实例,
  M7 外观不变）。修复宿主 HotPink 被 VS `#add6ff` 遮蔽、GitHub 独立预设放行 VS 蓝两个场景。
- **P2 画布背景保护补全（`DiffView.ApplyCanvasBackground`）**：只覆盖「修改后立即清除」,
  重建覆盖宿主背景时不更新保存值,清除时恢复旧值。现在当前背景不是控件自己的画布画刷
  即重新保存宿主值,清除恢复最新宿主背景。
- **P2 展开锚点探测（`DiffView.OnAnchorTuneLayout`）**：评论卡片高于视口时,估算偏移使锚点
  落在虚拟化实化窗口之外,校正找不到容器、三趟后放弃。新增 `ProbeAnchorIntoView`:方向由
  最近已实化行判定（实化区连续,锚点必在外侧）,按视口高度逐步探测（上限 24 次）,实化后
  走原有精确校正;分栏/统一/Expand All 共用该路径。
- **M5 注释分词（extract-textmate.mjs）**：脚本此前断言 TextMateSharp 不支持 begin/while
  ——对 2.x 不成立（DLL 含 `CompileWhile`/`BeginWhileRule`）。删除 while 丢弃与负向
  `^(?!X)`→end 转换,原样保留规则（csharp `//`、`///` 与 markdown 72 条规则恢复）;上游
  「只有 begin、缺 end/while」的畸形规则（xml 的 `<%--`/`--(?!>)`,vscode-textmate 实测
  让规则永久驻留）补永不匹配的 `end:"(?!)"`,避免 TextMateSharp 以 null end 编译崩溃。
  重新提取后语法黄金 447 span 仍全绿（3 处豁免不变）,行/文档注释分词回归测试落地
  （`SyntaxHighlightTests` 2 个）。repro 8 注释样例随包语法 0/8 → 修复后通过。
- **测试**：新增 `Resolution_HostCoarseSelection_BeatsPresetFineSlots`、
  `Resolution_IndependentGitHubPreset_PinsSelectionToItsGenericColor`、
  `ThemePreset_RebuildAfterAHostAssignment_RestoresTheLatestHostBackground`、
  `HunkExpansion_WithCardTallerThanTheViewport_KeepsTheViewportAnchor`（220px 视口 + 20 行
  评论 ≈334px 卡片）与 `GetAst_LineComment_UsesTheCommentColor` /
  `GetAst_DocComment_ContinuesAcrossLines`。基线：627 核心 + 128 headless 全绿,
  Debug/Release 0 警告 0 错误。

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
