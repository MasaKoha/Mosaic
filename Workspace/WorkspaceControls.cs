using System;
using Avalonia.Controls;

namespace GameMockStudio.Workspace;

/// <summary>XAMLで宣言した必須コントロールの参照を解決する。</summary>
public sealed class WorkspaceControls(Window window)
{
    /// <summary>名前が一致するコントロールを取得し、XAMLとの不一致を即座に通知する。</summary>
    public TControl Find<TControl>(string name) where TControl : Control
    {
        return window.FindControl<TControl>(name)
            ?? throw new InvalidOperationException($"画面の必須コントロールがありません: {name}");
    }
}
