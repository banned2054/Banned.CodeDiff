# Changelog

All notable changes to this project will be documented in this file. Each entry is provided in English and Simplified Chinese.

This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and [Semantic Versioning](https://semver.org/).

本文件记录项目的重要变更，每条内容均提供英文与简体中文版本。

本项目遵循 [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) 格式和 [Semantic Versioning](https://semver.org/) 版本规范。

## Unreleased

### English

#### ✨ Added

* Added `DiffView.UseSingleLineNumberColumn` — merges the unified view's old/new line-number columns into one: deleted lines show the old number, context and added lines the new one. Split view is unaffected, and toggling at runtime applies immediately without rebuilding rows.
* Added line-range comments (M7): `DiffView.FilePath` carries the host-provided file identity, `GetCommentAnchor()` derives an anchor (side + start/end line numbers + file path) from the current selection, the explicit `BeginCommentCommand` raises `CommentRequested`, and `DiffView.Comments` renders one card per anchor below the last visible line of its range with a persistent anchor highlight. Comment anchors are independent of the selection state — they survive new selections, view-mode switches, and hunk expansions, and hidden anchors re-appear when an expansion reveals their lines. Comments are file-scoped: only those whose `Anchor.FilePath` matches `DiffView.FilePath` render, so switching files never leaks comments onto same-numbered lines of another file. Drafting, submitting, replying, deleting, and persisting comments stay in the host; reassigning `Comments` refreshes the presentation.
* Added `DiffView.Palette` — a unified semantic color entry (`Light`/`Dark` slots for added/deleted/context line backgrounds, the selection overlay and edge strip, and the comment highlight plus comment card background/border). `null` slots keep the built-in upstream colors, so the default appearance is unchanged; each slot drives the number and content cells of its line kind.

### 简体中文

#### ✨ 新增

* 新增 `DiffView.UseSingleLineNumberColumn`：将统一视图的新旧两个行号列合并为一列——删除行显示旧行号，上下文行与新增行显示新行号。分栏模式不受影响，运行时切换立即生效、无需重建行。
* 新增行范围评论（M7）：`DiffView.FilePath` 携带宿主提供的文件身份，`GetCommentAnchor()` 从当前选区推导锚点（侧别 + 起止行号 + 文件路径），显式的 `BeginCommentCommand` 引发 `CommentRequested`，`DiffView.Comments` 按锚点在锚定范围最后一个可见行下方渲染评论卡片，并带锚点持久高亮。评论锚点与选区状态相互独立——后续选区替换、视图模式切换与 hunk 展开都不会清除，隐藏行全部折叠时不渲染卡片、展开后按行号恢复。评论按文件身份隔离——仅 `Anchor.FilePath` 与 `DiffView.FilePath` 一致的评论参与渲染，切换文件不会把评论串到其他文件的同号行上。草稿、提交、回复、删除与持久化由宿主负责；重新赋值 `Comments` 即刷新呈现。
* 新增 `DiffView.Palette`——统一的语义配色入口（`Light`/`Dark` 两套槽位：增删行与上下文行背景、选区覆盖层与边条、评论高亮与评论卡片背景/边框）。槽位为 `null` 时保留内置上游配色，默认外观不变；每个槽位同时作用于该类行的行号格与内容格。

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
