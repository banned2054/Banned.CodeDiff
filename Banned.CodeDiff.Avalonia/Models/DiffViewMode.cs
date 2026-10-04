namespace Banned.CodeDiff.Avalonia.Models;

/// <summary><see cref="Views.DiffView" /> 的显示模式。<br />Display mode of <see cref="Views.DiffView" />.</summary>
public enum DiffViewMode
{
    /// <summary>双栏分栏视图:旧文件在左,新文件在右。<br />Two-column split view: old file on the left, new file on the right.</summary>
    Split = 0,

    /// <summary>
    ///     单栏统一视图:删除行位于新增行之上,带双行号。<br />
    ///     Single-column unified view: deleted lines above the added ones, dual line numbers.
    /// </summary>
    Unified = 1
}
