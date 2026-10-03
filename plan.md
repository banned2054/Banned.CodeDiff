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
| M5 | 语法高亮 | ⬜ 下一步 |
| M6 | 打磨 | ⬜ |

每个里程碑保持可构建、可测试、可演示；完成一个更新一次本文件状态与 `Docs/CHANGELOG.md`。

## 3. 当前状态

- `Banned.CodeDiff` 核心库完成：解析（`DiffParser`）、split/unified 行模型（`DiffFile`）、
  词级区间（`ChangeRange` / `FastDiff`）、展开状态机、`TemplateOptions` 全局开关。
- 目录已按 `Models` / `Services` / `Utils` 分类。
- 577 个核心测试 + 22 个 Avalonia headless 测试全绿（构建 0 警告 0 错误），
  含与 JS 原版逐字段对比的黄金基准。
- `Banned.CodeDiff.Avalonia`（`DiffView` split 控件 + Demo）已就位，详见第 4 节执行结果。

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

- [ ] 常见语言（C#/JS/TS/JSON 等）Demo 正确着色。
- [ ] 语法高亮与词级/行级高亮正确叠加，不破坏 M4 性能基线。
- [ ] NOTICE 补充 TextMateSharp 条目。

## 8. M6 打磨

**目标**：可用性收尾。

**内容**：

- `ThemeVariant` 深浅主题（颜色体系对齐上游 theme）。
- wrap 模式（长行换行渲染，注意与虚拟化行高的配合）。
- 复制功能（选中行/文件内容复制）。
- `multiSelect` / widget 视需求决定是否移植（上游 `packages/core/src/multiSelect/*`）。

**验收**：

- [ ] 深浅主题切换正确，颜色符合设计。
- [ ] wrap 开关生效且不破坏滚动与虚拟化。
- [ ] 复制功能可用。
- [ ] 决定 multiSelect/widget 的去留并记录结论。

## 9. 通用验收（每个里程碑）

- `dotnet build Banned.CodeDiff.slnx` 与 `-c Release` 均 0 警告 0 错误。
- 全量测试绿，无回归。
- Demo 可演示该里程碑功能。
- `README.md` / `Docs/README.zh-CN.md` / `Docs/CHANGELOG.md` 同步更新。
- 新增上游衍生代码时同步更新 `NOTICE`。

## 10. 未决事项

- `Banned.CodeDiff` 是否单独发布 NuGet 包（`Banned.CodeDiff.Avalonia` 确定发布）。
- 仓库尚未初始化 git；README/文档中的 GitHub 链接按 `banned2054/Banned.CodeDiff` 预填，待确认。
- 版本号与 CHANGELOG 策略随首次发布确定。

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
