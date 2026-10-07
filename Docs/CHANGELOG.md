# Changelog

All notable changes to this project will be documented in this file. Each entry is provided in English and Simplified Chinese.

This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and [Semantic Versioning](https://semver.org/).

本文件记录项目的重要变更，每条内容均提供英文与简体中文版本。

本项目遵循 [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) 格式和 [Semantic Versioning](https://semver.org/) 版本规范。

## Unreleased

### English

#### ✨ Added

* Added theme presets and per-slot color resolution (M8): `DiffView.ThemePreset` applies a combined preset (`GitHub` / `Codex` / `Monokai` / `VisualStudio`) to both the Diff/interface palette and the syntax theme at once, while `DiffView.DiffPreset` and `DiffView.SyntaxPreset` pin one side independently (e.g. Monokai syntax over GitHub diff backgrounds). Resolution runs per slot: host overrides (`DiffView.Palette`, `DiffView.SyntaxOverrides`) beat the independent preset, which beats the combined preset, which beats the GitHub baseline — the golden-locked upstream look. Presets without a light variant (Monokai, Codex) fall back to GitHub light without color inversion; preset objects are shared read-onlys and theme state is isolated per `DiffView` instance.
* Extended `DiffPaletteColors` with fine-grained M8 slots: canvas background, line-number foreground, per-cell (number/content) added/deleted/context backgrounds, expand-row, hunk number/content/foreground, splitter, word-level add/delete emphasis, explicit per-kind selected-line backgrounds (`SelectedAddBackground` / `SelectedDeleteBackground` / `SelectedContextBackground` — the M7 coarse slots still work and a fine slot wins over its coarse counterpart), and reserved text-selection color slots (`TextSelectionBackground` / `TextSelectionForeground` — M8 defines the slots only, no character-selection interaction).
* Syntax colors now keep token scopes: the built-in TextMate engine retains each original token (range + scope stack) alongside the merged wrapper, and a per-view resolver re-matches them against the selected syntax preset and host scope overrides (`DiffView.SyntaxOverrides`: per-scope rules plus an optional default foreground that only rewrites tokens which took the theme default). Without customization the wrapper style's final colors are used directly (zero overhead, GitHub look untouched). The `ScopeThemeMatcher` and a `DiffSyntaxThemes` facade are now public; bundled syntax themes gained monokai, vs-dark, vs-light, and a codex-dark draft.
* Added `DiffView.UseSingleLineNumberColumn` — merges the unified view's old/new line-number columns into one: deleted lines show the old number, context and added lines the new one. Split view is unaffected, and toggling at runtime applies immediately without rebuilding rows.
* Added line-range comments (M7): `DiffView.FilePath` carries the host-provided file identity, `GetCommentAnchor()` derives an anchor (side + start/end line numbers + file path) from the current selection, the explicit `BeginCommentCommand` raises `CommentRequested`, and `DiffView.Comments` renders one card per anchor below the last visible line of its range with a persistent anchor highlight. Comment anchors are independent of the selection state — they survive new selections, view-mode switches, and hunk expansions, and hidden anchors re-appear when an expansion reveals their lines. Comments are file-scoped: only those whose `Anchor.FilePath` matches `DiffView.FilePath` render, so switching files never leaks comments onto same-numbered lines of another file. Drafting, submitting, replying, deleting, and persisting comments stay in the host; reassigning `Comments` refreshes the presentation.
* Added `DiffView.Palette` — a unified semantic color entry (`Light`/`Dark` slots for added/deleted/context line backgrounds, the selection overlay and edge strip, and the comment highlight plus comment card background/border). `null` slots keep the built-in upstream colors, so the default appearance is unchanged; each slot drives the number and content cells of its line kind.

#### 🐞 Fixed

* Fixed the opaque preset selection backgrounds painting above the code: the selected-line rectangles now render below the line text, the number text, and the word-level highlights in every template.
* Fixed the demo override toggles (`IsAddBackgroundOverridden` / `IsSyntaxScopeOverridden`) not raising change notifications, so the bound `DiffView.Palette` / `DiffView.SyntaxOverrides` kept receiving `null`.
* Fixed clearing a theme preset wiping a host-assigned background: clearing only undoes the canvas brush the preset applied and restores the background captured before it — a host background assigned before or after the preset is kept.
* Fixed a row rebuild re-applying the preset canvas over a later host background without re-capturing it: host background → enable a preset → host re-assigns → rebuild → clear used to restore the stale pre-preset background. The saved value now re-captures whenever the current background is not the control's own canvas brush, so clearing restores the latest host background.
* Fixed the host's coarse selection slot (`Palette.SelectionHighlight`) being masked by a preset's fine-grained selected backgrounds: the per-kind selection resolution now consults the host fine slot, then the host coarse slot, before any preset layer — the M7 "host overrides first" contract holds for selection too.
* Fixed an independent GitHub preset letting the combined preset's per-kind selection colors leak through (`ThemePreset=VisualStudio` + `DiffPreset=GitHub` kept the VS selection blue): when the chosen independent preset defines no fine-grained selection slots, its generic `SelectionHighlight` pins the selection.
* Fixed the light-theme fallback of a variant-less independent diff preset (Monokai/Codex): the diff side now pins to the GitHub baseline instead of leaking through to the combined preset's light values (the documented contract, mirroring the syntax side).
* Fixed invalid hex in `DiffSyntaxOverrides` (e.g. `#GGGGGG`) throwing `FormatException` at render time — values failing the theme pipeline's hex rule are now ignored.
* Fixed `#RRGGBBAA` override colors being read as Avalonia's `#AARRGGBB`: the TextMate channel order is preserved, so `#FF000080` renders as 50% red instead of opaque navy.
* Fixed the `DefaultForeground` override not applying to plain-text rows without syntax nodes (e.g. `.txt` files) — such rows now render the whole line with the override.
* Fixed shared preset and syntax brushes being mutable `SolidColorBrush` instances: they are now immutable, so mutating a brush exposed by one view can no longer repaint every other view.
* Fixed the unified comment card keeping two number gutters in the single-number-column mode — it now collapses like the content rows and stays aligned with the content.
* Fixed the expansion scroll anchor ignoring comment-card heights: after the estimate-based offset lands, the anchor is re-applied from the realized rows' measured positions (converging over a couple of layout passes), so revealing a comment card no longer drifts the clicked placeholder.
* Fixed the expansion anchor still getting lost when the revealed comment card is taller than the viewport: the anchor row could land outside the virtualized realized window, where the correction found no container and gave up after three layout passes. The correction now probes viewport-sized scrolls toward the unrealized anchor (direction from the nearest realized row) until it realizes, then re-anchors exactly — split, unified, and Expand All share the mechanism.
* Fixed bundled TextMate grammars dropping begin/while rules, which silenced whole rule families — most visibly the C# `//` and `///` comment rules, so comments tokenized as plain code. TextMateSharp 2.x supports while rules natively (verified against its `BeginWhileRule` pipeline and the golden suite), so the extraction keeps them verbatim; upstream begin-only rules (missing both `end` and `while`, which vscode-textmate tolerates by letting the rule persist) are normalized with a never-matching `end` instead of crashing TextMateSharp's compiler.
* Fixed `BeginCommentCommand` staying executable for a selection whose every line is hidden behind a collapsed hunk — it now follows exactly the copy commands' condition.

### 简体中文

#### ✨ 新增

* 新增主题预设与逐槽位配色解析（M8）：`DiffView.ThemePreset` 一键应用组合预设（`GitHub` / `Codex` / `Monokai` / `VisualStudio`），同时作用于 Diff/界面调色板与语法主题；`DiffView.DiffPreset` 与 `DiffView.SyntaxPreset` 可独立钉住一侧（如 Monokai 语法色搭配 GitHub Diff 行背景）。解析按槽位进行：宿主覆盖（`DiffView.Palette`、`DiffView.SyntaxOverrides`）胜过独立预设，独立预设胜过组合预设，组合预设胜过 GitHub 基线（即黄金锁定的上游外观）。没有浅色变体的预设（Monokai、Codex）回退 GitHub 浅色、不做颜色反转；预设对象为共享只读，主题状态按 `DiffView` 实例隔离。
* `DiffPaletteColors` 扩展 M8 细粒度槽位：画布背景、行号前景、增删/上下文行的行号格与内容格分离背景、展开行、hunk 行号/内容/前景、分隔线、词级增删强调、按行类别的选中行明确背景（`SelectedAddBackground` / `SelectedDeleteBackground` / `SelectedContextBackground`——M7 粗粒度槽位继续可用，细粒度槽位优先于同语义粗槽位），以及预留的文字选择颜色槽位（`TextSelectionBackground` / `TextSelectionForeground`——M8 仅定义槽位，不含字符选择交互）。
* 语法配色保留 token scope：内置 TextMate 引擎在合并 wrapper 的同时保留每个原始 token（区间 + scope 栈），渲染端按控件实例的语法预设与宿主 scope 覆盖重新匹配（`DiffView.SyntaxOverrides`：逐 scope 规则 + 可选默认前景，默认前景只改写吃到主题默认色的 token）。未定制时直接使用 wrapper style 的最终颜色（零开销，GitHub 外观不变）。`ScopeThemeMatcher` 与 `DiffSyntaxThemes` 门面转为公开；内置语法主题新增 monokai、vs-dark、vs-light 与 codex-dark 初稿。
* 新增 `DiffView.UseSingleLineNumberColumn`：将统一视图的新旧两个行号列合并为一列——删除行显示旧行号，上下文行与新增行显示新行号。分栏模式不受影响，运行时切换立即生效、无需重建行。
* 新增行范围评论（M7）：`DiffView.FilePath` 携带宿主提供的文件身份，`GetCommentAnchor()` 从当前选区推导锚点（侧别 + 起止行号 + 文件路径），显式的 `BeginCommentCommand` 引发 `CommentRequested`，`DiffView.Comments` 按锚点在锚定范围最后一个可见行下方渲染评论卡片，并带锚点持久高亮。评论锚点与选区状态相互独立——后续选区替换、视图模式切换与 hunk 展开都不会清除，隐藏行全部折叠时不渲染卡片、展开后按行号恢复。评论按文件身份隔离——仅 `Anchor.FilePath` 与 `DiffView.FilePath` 一致的评论参与渲染，切换文件不会把评论串到其他文件的同号行上。草稿、提交、回复、删除与持久化由宿主负责；重新赋值 `Comments` 即刷新呈现。
* 新增 `DiffView.Palette`——统一的语义配色入口（`Light`/`Dark` 两套槽位：增删行与上下文行背景、选区覆盖层与边条、评论高亮与评论卡片背景/边框）。槽位为 `null` 时保留内置上游配色，默认外观不变；每个槽位同时作用于该类行的行号格与内容格。

#### 🐞 修复

* 修复预设的不透明选中背景绘制在代码之上：所有模板中的选中行矩形改为绘制在行文本、行号文本与词级高亮之下。
* 修复 Demo 的覆盖开关（`IsAddBackgroundOverridden` / `IsSyntaxScopeOverridden`）不引发属性变更通知，绑定到视图的 `DiffView.Palette` / `DiffView.SyntaxOverrides` 因此一直收到 `null`。
* 修复清除主题预设会清掉宿主设置的背景：清除只撤销预设自己应用的画布画刷并恢复应用前捕获的背景——宿主在预设之前或之后设置的背景都会保留。
* 修复行重建把预设画布重新覆盖到宿主背景之上时没有重新保存宿主值：宿主背景 → 启用预设 → 宿主改色 → 重建 → 清除预设，最终恢复的是预设启用前的旧背景。现在只要当前背景不是控件自己的画布画刷就重新保存，清除时恢复的是最新的宿主背景。
* 修复宿主粗粒度选中槽位（`Palette.SelectionHighlight`）被预设的细粒度选中背景遮蔽：按行类别的选中色解析现在先查宿主细槽位、再查宿主粗槽位，之后才进入预设层——M7「宿主覆盖优先」契约在选中色上同样成立。
* 修复独立选择 GitHub 预设时组合预设的细粒度选中色穿透（`ThemePreset=VisualStudio` + `DiffPreset=GitHub` 时选中色仍是 VS 蓝）：独立预设未定义细粒度选中槽位时，改用其通用 `SelectionHighlight` 钉住选中色。
* 修复无浅色变体的独立 Diff 预设（Monokai/Codex）在浅色主题下的回退：Diff 侧现在钉定 GitHub 基线，不再穿透到组合预设的浅色值（与文档约定及语法侧一致）。
* 修复 `DiffSyntaxOverrides` 中的非法十六进制颜色（如 `#GGGGGG`）在渲染期抛出 `FormatException`——未通过主题管线十六进制规则的值现在会被忽略。
* 修复 `#RRGGBBAA` 覆盖色被按 Avalonia 的 `#AARRGGBB` 读取：保留 TextMate 通道顺序，`#FF000080` 渲染为 50% 透明红而非不透明藏蓝。
* 修复 `DefaultForeground` 覆盖不作用于无语法节点的普通文本行（如 `.txt` 文件）——这类行现在整行使用覆盖色。
* 修复共享预设画刷与语法画刷为可变 `SolidColorBrush` 实例：现在均为不可变画刷，修改某个视图暴露的画刷不会再影响其他视图。
* 修复统一视图评论卡片在单列行号模式下保留两条行号栏——现在与内容行同样折叠，保持与内容列对齐。
* 修复展开时的滚动锚定忽略评论卡片高度：估算偏移落地后，会基于 realized 行的实测位置重新校正（在几个布局趟内收敛），展开显露评论卡片不再使点击的占位行漂移。
* 修复评论卡片高于视口时展开锚点仍然丢失：锚点行可能落在虚拟化实化窗口之外，校正逻辑找不到容器、三个布局趟后直接放弃。校正现在会朝未实化的锚点方向按视口高度逐步探测（方向取自最近的已实化行），直到锚点实化后再精确校正——分栏、统一与 Expand All 共用该机制。
* 修复内置 TextMate 语法包丢弃 begin/while 规则导致整族规则失效——最明显的是 C# 的 `//` 与 `///` 注释规则被整行当普通代码着色。TextMateSharp 2.x 原生支持 while 规则（经其 `BeginWhileRule` 管线与黄金套件验证），提取脚本改为原样保留；上游「只有 begin、缺 end 与 while」的规则（vscode-textmate 容忍其永久驻留）改为补一个永不匹配的 `end`，避免 TextMateSharp 编译期崩溃。
* 修复选区所有行都被折叠 hunk 隐藏时 `BeginCommentCommand` 仍可执行——现在与复制命令的可用条件完全一致。

## 🚀 Release v0.1.0 — Initial Release

### English

#### ✨ Added

* **Core library (`Banned.CodeDiff`)** — Parses unified diff text and produces split and unified line models, word-level changes, and hunk expansion data.
* **Avalonia control library (`Banned.CodeDiff.Avalonia`)** — Provides `DiffView` with split and unified modes, expandable hunks, virtualized rows, GitHub-style light and dark themes, and word-level and syntax highlighting.
* Added line selection, copying selected or complete file contents, and long-line wrapping.
* Added built-in TextMate syntax highlighting and support for custom engines through `IDiffHighlighter`.
* Added an Avalonia demo application.

#### ⚡ Performance

* Improved large-diff line lookups, selection updates, content composition, and syntax highlighting by reducing repeated scans and temporary allocations.

#### 🐞 Fixed

* Fixed negative-cursor slicing in `FastDiff.Diff` so edge-case results match the upstream implementation.
* Hunk expansion now compensates for inserted content to reduce viewport jumps.

#### 📚 Documentation

* Added bilingual XML comments for the public APIs in both libraries and enabled XML documentation for IDE IntelliSense.
* Simplified the English and Chinese READMEs with installation, theme setup, and quick-start instructions.

### 简体中文

#### ✨ 新增

* **核心库（`Banned.CodeDiff`）**：解析 unified diff 文本，生成 split 和 unified 行模型，并提供词级变更信息与 hunk 展开数据。
* **Avalonia 控件库（`Banned.CodeDiff.Avalonia`）**：提供 `DiffView`，支持 split 和 unified 视图、hunk 展开、行虚拟化、GitHub 风格浅色与深色主题，以及词级和语法高亮。
* 支持行选择、复制选中内容或完整文件内容，以及长行自动换行。
* 提供内置 TextMate 语法高亮，并支持通过 `IDiffHighlighter` 接入自定义引擎。
* 提供 Avalonia Demo 应用。

#### ⚡ 性能优化

* 优化大文件 diff 的行查询、选区更新、内容组装和语法高亮，减少重复遍历与临时分配。

#### 🐞 修复

* 修复 `FastDiff.Diff` 在负 cursor 边界下的切片问题，使边界结果与上游实现一致。
* 展开 hunk 时补偿新增内容造成的滚动偏移，减少视口跳动。

#### 📚 文档

* 为两个库的公开 API 补充中英双语 XML 注释，并生成供 IDE IntelliSense 使用的 XML 文档。
* 精简中英文 README，保留安装、主题加载和快速入门说明。
