# Changelog

## Unreleased

### 修复 — fast-diff 负 cursor 后缀切片对齐 JS slice 钳制

- `FastDiff.FindCursorEditDiff` 的 editAfter 分支:`cursor < 0` 时两个 before 串同为
  空串即可通过 `newBefore != oldBefore` 早退,`suffixLength = min(oldLen,newLen)+|cursor|`
  可超过 `oldAfter/newAfter.Length`;JS 原版 `slice(len - n)` 越界钳制到整串/空串,
  C# 的 range 运算符在该域抛 `ArgumentOutOfRangeException`(golden 集合 cursor 最小值
  为 0,从未覆盖;库内调用方 `ChangeRange` 只传 0,但公开 API `FastDiff.Diff` 接受任意
  int)。新增 `JsSliceSuffix`/`JsSliceWithoutSuffix` 复刻 slice 钳制语义,三组
  node 实证用例(`diff('abc','abcdefghij',-5)` 等)修复后逐字段一致;
  新增 `FastDiffInvariantTests` 锚定(cursor 后缀 7 例、commonOverlap 探测循环 4 例、
  lossless 位移循环 2 例,期望值来自 fast-diff@1.3.0 node 实测)。

### 性能 — 大样例热路径优化(行为零变化,golden 全量护栏)

- **行查询字典化 + 选区链路去 O(n²)**:`DiffFile` 四个 `*ByLineNumber` 查询
  (线性扫描)改为构建时字典 O(1);`DiffView.CanCopySelection` 不再每次指针移动
  全量物化选区结果;`ApplySplitSelection` 每行 O(rows) 查找改行号字典。
- **`ComposeFile` 拼接改 StringBuilder**:缺失侧从 diff 结果合成文件内容时的
  逐行 `+=`(O(n²) 拷贝)改区间拼接。
- **TextMate 高亮结果 LRU 缓存与减分配**:`SourceFile.DoSyntax` 内建有界 LRU(容量 8,
  键 = 内容/语言/文件名/引擎/主题,Class 引擎主题无关)记忆化 tokenize 结果——非上游
  `cache.ts` 的跨实例整实例缓存(仍不移植),保持「syntax 仅在 DoSyntax 后存在」的
  可观测行为;引擎配置变更(Transform/忽略列表/阈值)清空缓存;逐 token 拼接与
  style 字符串 memoize 减分配。
- **`DiffSegmentText` 测量改手写循环取最大宽**:段内逐行测量取 `TextLines[i]` 宽度
  最大值,去掉逐段 `TextLayout` 重建。
- **fast-diff 分配优化**:`DiffCleanupSemanticLossless` 的逐字符右移探测(JS 每步重建
  三串,O(k²))改为只记最佳偏移量 + 三段只读视图打分、终了一次切片重构(位移超过
  edit 长度后尾段是 equality2 上的滑动窗而非增长前缀);`DiffCleanupMerge` 的
  `textDelete/textInsert` 逐 tuple `+=` 改段累积、每个 equality 边界一次物化;
  `DiffCommonOverlap` 探测循环不变量注释钉住。等价性:golden 全量 + 12000 例
  node 差分模糊(混合/高重复/窗口密集三组语料)逐字段一致。
- 复测(10312 行大样例 `CreateLargeSample(98,100)`,Release,Stopwatch 中位数 +
  `GC.GetAllocatedBytesForCurrentThread`;「前」为 2026-10 性能批次开始前基线):

  | 场景 | 前 | 后 |
  |---|---|---|
  | multiSelect 全选重算 split | 314–450 ms | 0.3 ms |
  | multiSelect 全选重算 unified | 258–367 ms | 0.3–0.4 ms |
  | 指针 move 选区数据层重算 ×2 | ~0.6–0.9 s | 0.6–0.7 ms |
  | ComposeFile 单侧合成 10312 行 | 582 ms / 4.4 GB | ~1 ms / <5 MB |
  | TextMate 同文件重复 tokenize | 505 ms | 0.08–0.09 ms(LRU 命中;冷 321–352 ms) |
  | fast-diff DiffChanges ×2000 对 | 24 ms / 15.3 MB | 8 ms / 3.8 MB |
  | `GetSplitLineByLineNumber` 单次 worst | 0.09 ms | ~0.0009 ms |
  | `GetSplitLineIndexByLineNumber` 单次 worst | 0.91 ms | <0.00005 ms |

### 性能 — 组装/词级 diff/高亮管线减遍历与减分配(行为零变化)

- **`DiffFileUtils` 四方法去 `NumIterator` 中间列表**:`GetSplitLines`/`GetUnifiedLines`
  与 `GetSplitContentLines`/`GetUnifiedContentLine` 不再先物化 `0..n-1` 整数列表再投影,
  改为 for 循环直接构建结果(隐藏行筛选、输出字段与顺序不变);公开的
  `DiffTool.NumIterator` 本身保留不动。
- **`DiffFile.ComposeDiff` 分组列表复用**:additions/deletions 从每个 hunk、每个
  context 边界新建改为跨 hunk 复用 + `Clear`(`GetDiffRange` 只按索引读取、不保留
  列表引用;分组边界、配对规则与 `GetDiffRange` 调用序列不变)。
- **`ChangeRange.DiffChanges` 单次遍历**:两次 `Where` 过滤 + `Any` 短路合并为一次
  遍历,同步构建两侧 `DiffItem` 列表并内联 `hasLineChange`(两侧各自 offset 递增、
  Operation 筛选、Range 起止与短路求值顺序逐项等价)。
- **`TextMateHighlighter.Tokenize` 减分配**:逐行发射的 `Select` 闭包迭代器改直接
  循环;`StandardTokenType` 的逐 scope `Split('.')`(段数组与子串仅用于等值比较,
  每 token 每 scope 都分配)改右到左 span 扫描,含空段语义逐段等价。
- **恢复 `DiffSegmentText.MeasureOverride` 手写循环取最大宽**:`1f8e112` 的测量
  热路径优化被 `40bb434` 全项目格式化意外回退为 LINQ 链(代码注释仍在),按原
  提交意图恢复。
- 等价性:核心 624 + Avalonia headless 68 全绿;6 个 demo 用例全模型 dump
  (行模型/HunkInfo/SplitInfo/Changes/DiffChanges/语法 span 全字段)SHA256 与
  改动前逐字节一致。
- 测量(临时 console harness 引 Release 产物、6 个 demo 用例;分配为
  `GC.GetTotalAllocatedBytes` 确定性计数;计时中位数受测量机并发负载影响,
  仅采信与噪声量级分离的项):

  | 场景 | 分配/次 前→后 | 计时 |
  |---|---|---|
  | 6 demo 用例 `Init()`(含 tokenize) | 67.7 MB → 52.4 MB(−22.6%) | 未采信(负载噪声) |
  | `GetSplitLines` 等四方法各一次(case a) | 64.8 KB → 39.7 KB(−38.9%) | 0.034 → 0.016 ms |
  | DiffFile.cs(1346 行)tokenize | 20.6 MB → 17.7 MB(−14.5%) | 未采信(负载噪声) |
  | `DiffChanges` × 200 对 | 1.53 MB → 1.51 MB(−0.9%;FastDiff 主导) | 未采信 |
  | 6 demo 用例 `InitRaw` | 677 KB → 666 KB(−1.7%) | 未采信 |

### 重构 — JS 语法怪癖 .NET 化(行为不变,golden 测试护栏)

- **删除零消费死代码**(公开 API 删除):`DiffLine` 的
  `PlainTemplate/PlainTemplateMode/SyntaxTemplate/SyntaxTemplateName/SyntaxTemplateMode`
  (M2 为不移植的 HTML 模板功能预留);`Utils/Symbol.cs` 的 `DiffModeEnum`
  (JS 侧仅框架包装组件使用,明确不移植)。
- **`IDiffHighlighter.Type` enum 化**:`string`("class"/"style")→
  `Banned.CodeDiff.Models.HighlighterType { Class, Style }`(新文件
  `Models/HighlighterType.cs`);`SourceFile.HighlighterType` 与
  `DiffFile` 的 `_highlighterType` 同步改为
  `HighlighterType?`(null 取代原空串"未设置"语义;访问器重命名见下)。契约收窄:引擎只能
  返回 Class/Style,不再接受任意字符串。
- **`IDiffHighlighter.IgnoreSyntaxHighlightList` 强类型化**:`IReadOnlyList<object>`
  (string | RegExp 混装)→ 判别联合 `IgnorePattern`(新文件 `Models/IgnorePattern.cs`:
  `abstract record IgnorePattern` + `FileNameIgnorePattern(string FileName)` +
  `RegexIgnorePattern(Regex Regex)`);文件名相等比较与 `Regex.IsMatch` 两种匹配
  语义保持不变。`TextMateHighlighter.SetIgnoreSyntaxHighlightList` 参数同步换型。
- **FastDiff 操作码 enum 化**:`FastDiff` 的 `Delete=-1/Insert=1/Equal=0` int 常量与
  `DiffTuple.Op`、`DiffItem.Type` 统一为 `enum DiffOp { Delete = -1, Insert = 1, Equal = 0 }`
  (显式赋值保值,golden JSON 数值不变);消费点 `ChangeRange`、Avalonia
  `DiffHighlights`、golden dump(`(int)` 转换)同步。
- **命名规范**:`DiffFile` 私有 camelCase 属性 `_composeByRange` 改为字段
  (与兄弟状态位 `_composeByDiff` 一致,仍未被赋值——bundle 序列化不移植)。
- **注释微改善**(不改代码):FastDiff `/ 2.0` 处注明 JS 浮点除法语义;
  `DiffFile` hunk 头解析 `?? 0` 兜底处注明被 `DiffHeaderRegex` 纯数字捕获保证不可达;
  `HighlightAst` `int.MaxValue` 占位处注明立即被覆盖、不参与运算。
- **核心库无参 `Get*()` JS 访问器改为 .NET 惯用属性**(带参 `Get*()` 如
  `GetSplitLeftLine(index)`/`GetOldSyntaxLine(lineNumber)` 保留方法形态;仅
  `Banned.CodeDiff` 的 JS 移植访问器,`Banned.CodeDiff.Avalonia` 控件库自身 API
  不受影响)。完整映射:

  | 旧 | 新 |
  |---|---|
  | `DiffFile.GetTheme()` | `DiffFile.Theme` |
  | `DiffFile.GetHighlighterName()` | `DiffFile.HighlighterName` |
  | `DiffFile.GetHighlighterType()` | `DiffFile.HighlighterType`(`HighlighterType?`) |
  | `DiffFile.GetExpandEnabled()` | `DiffFile.IsExpandEnabled` |
  | `DiffFile.GetIsPureDiffRender()` | `DiffFile.IsPureDiffRender` |
  | `DiffFile.GetOldFileContent()` | `DiffFile.OldFileRaw` |
  | `DiffFile.GetNewFileContent()` | `DiffFile.NewFileRaw` |
  | `DiffFile.GetCurrentComposeLength()`(静态) | `DiffFile.CurrentComposeLength`(静态) |
  | `ChangeRange.GetMaxLengthToIgnoreLineDiff()`(静态) | `ChangeRange.MaxLengthToIgnoreLineDiff`(静态) |
  | `TemplateOptions.GetEnableFastDiffTemplate()`(静态) | `TemplateOptions.EnableFastDiffTemplate`(静态) |
  | `TemplateOptions.GetEnableBuildTemplate()`(静态) | `TemplateOptions.EnableBuildTemplate`(静态) |

  其中 `GetOldFileContent()`/`GetNewFileContent()` 未沿用 `OldFileContent`/`NewFileContent`
  属性名:后者是构造入参(可在纯 diff 模式下被重组),前者是 `SourceFile.Raw`
  (经 `Transform.ProcessTransformForFile` 处理、`InitRaw` 前为 null),语义不同
  (JS 原版即两个不同成员:`_oldFileContent` 与 `getOldFileContent()`),故改名为
  `OldFileRaw`/`NewFileRaw` 而非合并。消费点(Avalonia RowBuilder/DiffView、Demo VM、
  两个测试项目)已全部同步。

- **`_Xxx` 快照字段改为快照语义命名**(`Banned.CodeDiff.Models`,公开 API 重命名,行为不变):
  `HunkInfo`/`HunkLineInfo` 的 `_OldStartIndex/_OldLength/_NewStartIndex/_NewLength` →
  `OldStartIndexSnapshot/OldLengthSnapshot/NewStartIndexSnapshot/NewLengthSnapshot`;
  `HunkLineInfo` 的 `_StartHiddenIndex/_EndHiddenIndex/_PlainText` →
  `StartHiddenIndexSnapshot/EndHiddenIndexSnapshot/PlainTextSnapshot`;
  `SplitLineItem`/`UnifiedLineItem` 的 `_IsHidden` → `IsHiddenSnapshot`。
  可空性(`int?`/`bool?` 等)与数值语义均保持原样(`HunkLineInfo` 与 `HunkInfo` 同名字段
  的可空性差异是行为锚点,不统一);测试 JSON dump key(`"_oldStartIndex"` 等字面量)不变,
  golden 输出逐字节一致。

### M6 — 长行换行模式(上游 diffViewWrap 移植,批次 4)

- **`Banned.CodeDiff.Avalonia`**:`DiffView` 新增可绑定属性 `Wrap`(bool,默认 false,不改变现有宿主
  渲染——与 `IsSelectionEnabled` 同样的 opt-in 策略;上游包装层默认开启,端口不沿用)。行号列不参与
  换行(宽度仍由 `NumberColumnWidth` 决定)。
- **`DiffSegmentText` 多行化**:`Wrap=true` 时以测量可用宽度构建 `TextLayout`
  (`TextWrapping.Wrap` + `maxWidth`),控件高度 = 全部 TextLine 高度之和,宽度变化时布局缓存按宽度
  重建;`Wrap=false` 路径行为不变(现有测试为回归守卫)。词级高亮矩形 `ComputeHighlightRects`
  按行分段:跨行 range 碎片化为每行一块(首行从起点到行尾、中间行整行、末行从行首到终点),对齐浏览器
  inline box 背景的跨行行为;`HitTestTextPosition` 的 Rect 自带行内 y 偏移,按 `TextLine` 归属取 y。
  语法段(`GetSyntaxLayouts`)同样按行边界切分,每片布局放在该行自己的 (x, y)。
- **模板(Generic.axaml)**:内容单元格由横向 StackPanel 改为 `Auto,*` Grid(star 列给文本传递有限
  换行宽度;nowrap 下测量结果不变),`Wrap` 经 `{Binding $parent[v:DiffView].Wrap}` 下传;wrap 开启
  时内容与行号改为顶对齐(上游 wrap 组件的 `align-top`,nowrap 保持居中),hunk 头文本同步换行
  (上游 unified hunk 行 pre-wrap;split hunk 行依赖 div 默认 white-space:normal 同样换行)。
  split 同一行左右 cell 由 Grid 共享行高天然等高(= 较高者),对应上游 useSyncHeight;上游 wrap 模式
  的 DOM 形态差异(左右挤同一 tr、JS 同步高度)不移植,以模型行为为准(既定决策)。
- **换行策略对照**:上游为 CSS `white-space: pre-wrap` + `word-break: break-all`(逐字符贪心填行);
  端口用 `TextWrapping.Wrap`(词级断行 + 超宽单词内断行,即 GitHub diff 风格)——Avalonia TextLayout
  无 break-all 等价物且不引入新依赖,该差异仅影响"单词整体能否挤入行尾"的断点选择,不影响可见文本。
- **虚拟化**:VirtualizingStackPanel 对可变高度项工作正常(测试实证:extent 随实现行高度精化,
  滚到底部/中部目标行可见,不丢行,仍只实现可见切片);展开锚定在 wrap 下用邻近行高估算,不崩、
  视口不越界(不承诺逐像素几何)。
- **Demo**:工具栏新增「自动换行」开关(`IsWrap` → `DiffView.Wrap`)。
- 测试:UI 13 个 headless 测试——DiffSegmentText nowrap 回归/多行布局与按宽度重建/跨行高亮分段
  (含第二行 y 断言)/语法段按行切分;DiffView 行高开关切换、split 左右等高(左右长度悬殊)、
  unified 行增长且行号列定宽、词级高亮落在换行后行、选区遮罩覆盖换行行高、复制不受 wrap 影响、
  虚拟化滚底/滚中回顶、wrap 下展开视口合法。

### M6 — 复制功能(原生新功能,上游无对应实现,批次 3)

- **`Banned.CodeDiff`**:`Utils/MultiSelectData.cs` 新增 `GetSelectedTextFromResult(MultiSelectResult?)`
  ——选区纯文本生成(纯逻辑,NUnit 可测):逐行取 `SelectedLine.Value`,跳过 `IsHide` 的行
  (用户看到什么复制什么),每行去尾部换行(与渲染层 `TrimEnd('\r','\n')` 一致——模型 Value
  自带尾换行,不去除会产生空行),行间用 `\n` 连接(与 diff 文本一致,不用
  `Environment.NewLine`);`Value` 为 null 的行复制为空行(保留行位);null 结果或全隐藏
  返回空串。整文件复制直接用现有 `DiffFile.GetOldFileContent()/GetNewFileContent()`,
  原样传出(含结尾换行)不二次处理。
- **`Banned.CodeDiff.Avalonia`**:`DiffView` 新增复制 API(与 ExpandHunk 命令同风格):
  `CopySelectionCommand`(无选区或全为隐藏行时 CanExecute=false;CanExecuteChanged 在
  选区变化/完成/清除与 DiffFile 重建时触发)、`CopyOldFileCommand`/`CopyNewFileCommand`
  (不依赖选区,无 DiffFile 内容时 CanExecute=false),及公开方法 `CopySelectionAsync()`/
  `CopyOldFileAsync()`/`CopyNewFileAsync()`(返回 `Task<bool>`,false = 未写剪贴板的
  静默 no-op)。剪贴板走 `TopLevel.Clipboard.SetTextAsync`(Avalonia 12 为
  `Avalonia.Input.Platform.ClipboardExtensions` 扩展);命令 Execute 为 async void
  fire-and-forget。**不内置键盘快捷键**(避免与宿主绑定冲突),由宿主/Demo 绑定。
- **Demo**:工具栏新增「复制选中行」「复制旧文件」「复制新文件」按钮;复制选中行按钮
  随选区状态可用(VM 跟踪 SelectionCompleted 的可见行数),复制后状态栏反馈
  「已复制 N 行」;`Window.KeyBindings` 绑定 Ctrl+C(宿主接入示例,控件本身不绑)。
- 测试:核心 10 个文本生成单测(普通/含隐藏/跨 hunk/null Value/全隐藏/CRLF/边界);
  UI 10 个 headless 测试——Avalonia Headless 12.1.3 实测提供可用剪贴板
  (`TopLevel.Clipboard` 非空、SetText/TryGetText 往返成功且同步完成),直接断言最终
  剪贴板内容,无需注入隔离。

### M6 — 行选择(上游 multiSelect 移植,批次 2)

- **`Banned.CodeDiff`**:数据层移植自 `multiSelect/types.ts` + `multiSelect/data.ts`(+ `dom.ts`
  的纯函数 `normalizeRange`、`visual.ts` 的纯函数 `changePreselectedLinesToLineRange`):
  `MultiSelectRange`/`SelectedLine`/`MultiSelectResult`/`MultiSelectState` 等类型与
  `GetSelectedLinesFromDiffFile_Split/_Unified`,逐行号查询 DiffFile,`IsHide` 来自
  `CheckCurrentLineIsHidden`,`IsContext` 保留上游 `diff?.type === undefined` 也算 context 的
  怪癖,`Index` 为 1-based(split/unified 模型索引 +1)。命名差异:`LineRange` 已被
  change-range 移植占用,multiSelect 范围类型命名 `MultiSelectRange`;side 统一用 `SplitSide`
  枚举(上游字符串联合)。不移植:`extendDataToPreselectedLines`(评论流适配)。
- **`Banned.CodeDiff.Avalonia`**:行选择交互移植自 `multiSelect/manager.ts` +
  `DiffViewWithMultiSelect.tsx`(`Services/DiffSelection.cs` 状态机、`Services/DiffSelectionDom.cs`
  的 dom.ts 视觉树等价物、`DiffView` 的指针路由)。拖拽从行号列开始(PointerCapture 保证
  PointerReleased 全局接住,等价上游 document mouseup;拖拽中锁定起始侧;split 拖拽悬停行
  内容也会延伸选区、unified 悬停行号区才延伸、无起始侧行号的行不延伸,均照上游);release
  归一化并产出结果,完成后选区经由 preselected 通道保持高亮,新拖拽清空上次选区;数据变化
  (展开/收起)后同步重算高亮(上游 16ms debounce 是 DOM 批处理优化,不复刻)——隐藏行
  不高亮但保留在选区,展开后补齐。视觉:上游 `_com.css` 的
  `#f0c000` 15% 遮罩 + `#2588fa` 4px 边条(light/dark 同值),边条在选中侧行号格右缘
  (上游 `-2px` 外溢简化为格内 4px);split 按数据层 isContext 决定双侧/单侧高亮,unified
  整行高亮。公开 API(`DiffView`):`IsSelectionEnabled`(默认 false,与上游默认 true 不同,
  避免改变既有宿主行为)、`SelectionChanged`/`SelectionCompleted` 事件、
  `GetSelectionResult()`、`GetSelectionState()`、`ClearSelection()`、`SetPreselectedLines(old/new)`
  (min/max 大区间合并为上游已知语义)。有意差异:ClearSelection 连 preselected 通道一并清空
  (上游 manager 的 clearSelection 保留 preselected,单通道移植下保留会导致高亮永远清不掉);
  DOM 形态分支(wrap/nowrap 双 tr)不移植,以模型行 + side 为键;scopeToHunk 钩子不移植。
- **Demo**:「启用行选择」开关 + 选区完成后的状态栏(「已选 N 行(old 12-34)」),为批次 3
  复制功能做铺垫。
- NOTICE 新增 multiSelect 衍生记录。

### M6 — 展开 hunk 后的视口锚定(M4 遗留)

- **`Banned.CodeDiff.Avalonia`**:点击展开按钮后视口不再跳变。展开命令先记录锚点
  (点击的占位行索引、滚动偏移、占位行与相邻内容行的实化高度),`DiffFile.Updated`
  重建 Rows 后按各方向的实际插行几何补偿 `ScrollViewer.Offset`:Up 存活时占位行
  flat 索引不变(偏移不动);Up 揭示全隐藏区间 / All 时占位行消失、揭示行落在其后
  行上方(偏移 += 揭示高度 − 占位行高);Down 时占位行下移(偏移 += 插入高度);
  尾部折叠条消失时行追加在末尾(偏移不动)。偏移设置延迟到重建后的
  `ScrollViewer.ScrollChanged`(extent 更新)再应用,避开 `Offset` 对旧 extent 的
  coerce 钳制。行为对标上游 web 端依赖的浏览器 scroll anchoring。

### M6 — 主题颜色对齐上游(批次 1)

- **`Banned.CodeDiff.Avalonia`**: light/dark 两套画笔逐项核对上游 `_com.css` 的
  `--diff-*--` token(react/vue/solid 一致,svelte 仅引号差异,lynx 为独立变体不计),
  既有值全部一致。唯一修正:展开 hunk 揭示的原始行(无 `DiffLine`)此前沿用 plain
  context 背景,现改用上游 `--diff-expand-content--`(light `#fafafa` / dark `#161b22`),
  通过新增 `DiffCellKind.Expand` 区分;行号格保持原值(上游
  `--diff-expand-lineNumber--` 与 plain 数值相同)。当前功能未消费的 token 仅记录不实现:
  展开按钮 hover(`--diff-hunk-lineNumber-hover--`)、"+" 添加小组件
  (`--diff-add-widget--`/`--diff-add-widget-color--`)、tooltip 配色、multiSelect 选区
  配色(批次 2)。

### M5 — Syntax Highlighting

- **`Banned.CodeDiff`**: syntax highlighting state ported from `file.ts` / `diff-file.ts` —
  `SourceFile.DoSyntax` (max-line threshold, language check, idempotence), `DiffFile.InitSyntax`
  (with optional injected engine), and `GetOldSyntaxLine` / `GetNewSyntaxLine`. `Init()` now runs
  both `InitRaw` and `InitSyntax`, matching the upstream `init()`.
- Built-in `TextMateHighlighter` engine: TextMateSharp 2.x tokenization carrying the rule stack
  across lines (block comments / template literals keep their state across collapsed hunks),
  producing shiki-shaped wrapper styles with both theme colors
  (`--diff-view-dark:#…;--diff-view-light:#…`). Theme matching is a straight port of the
  vscode-textmate matcher (`ScopeThemeMatcher`); TextMateSharp's own `Theme.Match` mis-resolves
  descendant selectors on scope stacks, and is not used.
- Grammars and themes embed the shiki-bundled sources (@shikijs/langs / @shikijs/themes,
  20 languages plus their dependency grammars, github-light/dark). Files over 2000 raw lines or
  in unregistered languages render plain, mirroring the upstream guard.
- Golden tests replay the REAL shiki engine (same `codeToHast` options as `@git-diff-view/shiki`)
  plus the upstream `processAST` against the C# engine, field-by-field, for C# / TypeScript /
  JSON / Vue samples. Three lines carry known TextMateSharp↔vscode-oniguruma engine divergences
  (C# interpolation capture boundaries; the CSS class-selector misjudgment inside `<style>`),
  explicitly pinned and relaxed in `SyntaxGoldenTests` — the port cannot correct those inside
  the embedded grammars.
- Upstream behavior difference (intentional): when the injected highlighter does not know the
  language, the JS falls back to lowlight (highlight.js); the C# port has no lowlight port, so
  such files simply stay unhighlighted.
- **`Banned.CodeDiff.Avalonia`**: `DiffView.SyntaxHighlight` (default on) drives `InitSyntax`;
  `DiffView.Highlighter` injects a custom engine. Content rows carry syntax runs resolved from
  the wrapper styles per theme variant (dark/light picked at row-build time, theme switches
  rebuild rows without re-tokenizing), rendered as per-run foregrounds on top of the existing
  word-level highlight rectangles. Lines with more than 150 spans degrade to plain text
  (upstream render guard).
- Demo: syntax toggle, a paste-mode filename extraction from the diff headers (so pasted
  `.cs` diffs colorize), and a C#/TypeScript/JSON sample cycler.
- NOTICE updated for TextMateSharp, the vscode-textmate theme-matching port, and the embedded
  shiki grammars/themes.

### Tests

- Migrated both test suites from xunit to NUnit 5.0.0 (constraint-model asserts,
  `NUnit3TestAdapter`/VSTest execution). Parallelization is disabled via
  `[assembly: Parallelizable(ParallelScope.None)]` because the core library holds global state;
  the Avalonia suite uses `Avalonia.Headless.NUnit` with `[AvaloniaTest]`. All 577 core cases and
  11 UI cases pass unchanged.

### Banned.CodeDiff

- Initial implementation of the core logic library, ported from
  [`@git-diff-view/core`](https://github.com/MrWangJustToDo/git-diff-view) and `fast-diff@1.3.0`:
  - Unified diff parsing (`RawDiff`, multi-file / multi-hunk, CRLF, no-newline markers, binary
    markers, bidi detection)
  - Split / unified line models with expandable hunks (up / down / all, configurable step,
    `Updated` notification)
  - Word-level change ranges from two algorithms (relative-changes and fast-diff), behind the
    `TemplateOptions` global switches
  - Language detection via `DiffTool.GetLang`
- 577 unit and golden tests comparing field-by-field against the JS original.

### Banned.CodeDiff.Avalonia

- Initial implementation (M2): the `DiffView` control renders a read-only, GitHub-style split view
  from a `DiffFile` — line-level add/delete/context backgrounds, collapsed hunk placeholder rows,
  a monospace default font (`Menlo, Consolas, monospace`, 14px), auto-sized line-number columns,
  and light/dark palettes mirroring the upstream color variables. Assigning the model invokes
  `Init`/`BuildSplitDiffLines` on demand and the view refreshes through the model's `Updated` event.
- Demo app (`Banned.CodeDiff.Avalonia.Demo`): paste a unified diff text and render it, with a
  built-in sample, light/dark theme toggle, and add/delete statistics.
- Word-level highlight rendering (M3): the changed ranges inside paired add/delete lines are
  highlighted with nested background blocks (`DiffSegmentText`, a custom-drawn text control based
  on `TextLayout.HitTestTextPosition` — Avalonia text runs expose no per-run background). Fast-diff
  segments are preferred with the relative-changes single range as fallback; both view modes and
  both palettes are covered, and the demo gains a highlight toggle.
- Unified view mode: `DiffView.ViewMode` switches between split (default) and unified rendering —
  single column with dual (old/new) line-number columns, deleted lines above the added ones,
  matching the GitHub unified layout. The demo gains a mode toggle and dual-model statistics.
- Hunk expand/collapse UI (M4): hunk placeholder rows now carry expand buttons wired to the
  model's expand API through `DiffView.ExpandHunkUpCommand` / `ExpandHunkDownCommand` /
  `ExpandHunkAllCommand`. Button placement mirrors the upstream git-diff-view components:
  a single Expand Up on the first hunk, a single Expand Down on the trailing collapse strip,
  a single Expand All when fewer than 40 hidden lines remain, and a stacked down+up pair
  otherwise. The synthetic trailing hunk (previously not rendered) now shows as the bottom
  expand strip. Expansion requires a model built with real old/new file contents; paste-only
  diffs render hunk rows without buttons.
- Row virtualization (M4): the row list renders through a `VirtualizingStackPanel`; a 10k-line
  model (785 visible rows) realizes only ~25 containers in an 800×600 viewport.
- Behavior alignment (M4): a hunk placeholder row is only rendered while it still hides lines
  (`startHiddenIndex < endHiddenIndex`), matching GitHub and the upstream view components —
  in particular the leading `@@` header of a hunk starting at line 1 is no longer shown, and
  fully expanded placeholders disappear. Raw gap lines revealed by expansion (no `DiffLine`,
  but a line number and file text) now render as plain context rows instead of empty cells.
- Demo: new "load expandable sample" action (a synthetic multi-hunk file with real contents
  exercising every button shape) and expand-all / collapse-all toolbar buttons.
- Hosts include the control theme explicitly — Avalonia does not auto-discover control-library
  themes: `<StyleInclude Source="avares://Banned.CodeDiff.Avalonia/Themes/Generic.axaml" />`.
- New headless UI test suite (`tests/Banned.CodeDiff.Avalonia.Tests`, now NUnit +
  `Avalonia.Headless.NUnit`): asserts the theme loads through the consumer-style include, the
  template instantiates, row models build correctly in both split and unified modes, mode
  switching rebuilds rows, and palette colors match the upstream values. Grown to 22 cases with
  M4 (expand directions, button placement, command wiring, virtualization, 10k-line performance
  baseline); the test app now also loads the Fluent theme like a real consumer — previously
  only the library theme was loaded, so `ItemsControl`/`Button` never received themes and row
  containers were never instantiated.
