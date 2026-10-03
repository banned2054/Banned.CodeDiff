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
| M2 | 最小可看：split 视图渲染 | ⬜ 下一步 |
| M3 | 词级高亮渲染 | ⬜ |
| M4 | hunk 展开/收起 UI + 虚拟化 | ⬜ |
| M5 | 语法高亮 | ⬜ |
| M6 | 打磨 | ⬜ |

每个里程碑保持可构建、可测试、可演示；完成一个更新一次本文件状态与 `Docs/CHANGELOG.md`。

## 3. 当前状态

- `Banned.CodeDiff` 核心库完成：解析（`DiffParser`）、split/unified 行模型（`DiffFile`）、
  词级区间（`ChangeRange` / `FastDiff`）、展开状态机、`TemplateOptions` 全局开关。
- 目录已按 `Models` / `Services` / `Utils` 分类。
- 577 个测试全绿（构建 0 警告 0 错误），含与 JS 原版逐字段对比的黄金基准。
- `Banned.CodeDiff.Avalonia` 项目尚未创建。

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

- [ ] Demo 中粘贴 diff 文本即可看到 split 双栏视图。
- [ ] 增/删行有正确背景色，行号与内容对齐，等宽字体，无编辑行为。
- [ ] `dotnet build Banned.CodeDiff.slnx` Debug/Release 0 警告 0 错误，577 测试不回归。

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

- [ ] 配对增/删行的变更部分有词级背景高亮，与行级背景正确叠加。
- [ ] DemoData 与手造样例渲染正确（与 JS 原版截图对照）。
- [ ] 性能在 spike 结论允许范围内。

## 6. M4 hunk 展开/收起 + 虚拟化

**目标**：收起 hunk 显示占位行，可展开（up / down / all，每步 40 行），大 diff（万行级）流畅。

**内容**：

- 展开状态机核心（up/down/all、步长 40、`Updated` 事件）M1 已移植完成，本里程碑做 UI 接线：
  占位行可点击，提供向上/向下/全部展开入口，`Updated` 后刷新可见行。
- 接 `VirtualizingStackPanel`（或 ItemsControl 虚拟化）渲染行模型，split 左右列同步滚动。
- 用万行级 diff 做性能测试（内存、滚动流畅度），记录基线数据供后续回归对比。

**验收**：

- [ ] 占位行三种方向展开均生效，展开后行号与内容正确。
- [ ] 万行级 diff 滚动流畅，无整表重建；虚拟化下无可见的行错位。
- [ ] 性能基线已记录。

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
