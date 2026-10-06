# Changelog

All notable changes to this project will be documented in this file. Each entry is provided in English and Simplified Chinese.

This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and [Semantic Versioning](https://semver.org/).

本文件记录项目的重要变更，每条内容均提供英文与简体中文版本。

本项目遵循 [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) 格式和 [Semantic Versioning](https://semver.org/) 版本规范。

## Unreleased

### English

#### ✨ Added

* Added `DiffView.UseSingleLineNumberColumn` — merges the unified view's old/new line-number columns into one: deleted lines show the old number, context and added lines the new one. Split view is unaffected, and toggling at runtime applies immediately without rebuilding rows.

### 简体中文

#### ✨ 新增

* 新增 `DiffView.UseSingleLineNumberColumn`：将统一视图的新旧两个行号列合并为一列——删除行显示旧行号，上下文行与新增行显示新行号。分栏模式不受影响，运行时切换立即生效、无需重建行。

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
