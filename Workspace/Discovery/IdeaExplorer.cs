using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using GameMockStudio.Brief;
using GameMockStudio.Brief.Discovery;
using GameMockStudio.Brief.Planning;

namespace GameMockStudio.Workspace.Discovery;

/// <summary>検討用の案を選び、仮説を読んでから企画へ進む画面。</summary>
public sealed class IdeaExplorer : IDisposable
{
    private readonly IdeaCatalog catalog;
    private readonly ComboBox selector;
    private readonly TextBlock description;
    private readonly CompositeDisposable subscriptions = new();
    private IReadOnlyList<IdeaSeed> ideas = [];

    /// <summary>案の選択と説明を接続する。</summary>
    public IdeaExplorer(WorkspaceControls controls, IdeaCatalog catalog)
    {
        this.catalog = catalog;
        selector = controls.Find<ComboBox>("IdeaSelector");
        description = controls.Find<TextBlock>("IdeaDescriptionText");
        SetEvent();
    }

    /// <summary>対象の種類に合わせた案を表示する。</summary>
    public void ShowKind(MockKind kind)
    {
        ideas = catalog.ForKind(kind);
        selector.ItemsSource = ideas.Select(idea => idea.Title).ToArray();
        selector.SelectedIndex = 0;
        ShowDescription();
    }

    /// <summary>選択した案から編集用の下書きを作る。</summary>
    public BriefDocument Capture(MockFormat format)
    {
        if (selector.SelectedIndex < 0 || selector.SelectedIndex >= ideas.Count)
        {
            throw new InvalidOperationException("出発点にする案を選んでください。");
        }
        return ideas[selector.SelectedIndex].CreateBrief(format);
    }

    /// <summary>選択変更の購読を解除する。</summary>
    public void Dispose()
    {
        subscriptions.Dispose();
    }

    private void SetEvent()
    {
        subscriptions.Add(selector.GetObservable(SelectingItemsControl.SelectedIndexProperty)
            .Subscribe(_ => ShowDescription()));
    }

    private void ShowDescription()
    {
        description.Text = selector.SelectedIndex >= 0 && selector.SelectedIndex < ideas.Count
            ? ideas[selector.SelectedIndex].Description : string.Empty;
    }
}
