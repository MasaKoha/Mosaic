using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using GameMockStudio.Generation.History;
using GameMockStudio.Generation.Refinement;

namespace GameMockStudio.Workspace.Refinement;

/// <summary>感想の入力と改善対象を表示し、新規生成との操作を区別する。</summary>
public sealed class FeedbackEditor : IDisposable
{
    private const int FeedbackTabIndex = 1;
    private readonly TabControl tabs;
    private readonly TextBox feedback;
    private readonly TextBlock target;
    private readonly TextBlock appliedFeedback;
    private readonly Button refine;
    private readonly Button generate;
    private readonly ComboBox format;
    private readonly CompositeDisposable subscriptions = new();
    private bool updating;
    private bool busy;
    private bool available;

    /// <summary>感想欄と新規・改善の操作を接続する。</summary>
    public FeedbackEditor(WorkspaceControls controls)
    {
        tabs = controls.Find<TabControl>("EditorTabs");
        feedback = controls.Find<TextBox>("FeedbackBox");
        target = controls.Find<TextBlock>("FeedbackTargetText");
        appliedFeedback = controls.Find<TextBlock>("AppliedFeedbackText");
        refine = controls.Find<Button>("RefineButton");
        generate = controls.Find<Button>("GenerateButton");
        format = controls.Find<ComboBox>("FormatSelector");
        feedback.MaxLength = RefinementRequest.MaximumFeedbackCharacters;
        Changes = feedback.GetObservable(TextBox.TextProperty).Skip(1).Where(_ => !updating).Select(_ => Unit.Default);
        ModeChanges = tabs.GetObservable(SelectingItemsControl.SelectedIndexProperty).Skip(1).Select(_ => Unit.Default);
        SetEvent();
        RefreshAvailability();
    }

    /// <summary>利用者による感想の編集。</summary>
    public IObservable<Unit> Changes { get; }
    /// <summary>企画と感想の編集切り替え。</summary>
    public IObservable<Unit> ModeChanges { get; }
    /// <summary>感想から改善する操作を表示しているか。</summary>
    public bool IsRefining => tabs.SelectedIndex == FeedbackTabIndex;
    /// <summary>改行を含めた現在の感想。</summary>
    public string Feedback => feedback.Text ?? string.Empty;

    /// <summary>企画を読み込んだとき、編集対象が見えるタブへ戻す。</summary>
    public void ShowPlanning()
    {
        tabs.SelectedIndex = 0;
    }

    /// <summary>ゲームごとの下書きと、今回の版に適用済みの感想を表示する。</summary>
    public void ShowTarget(GenerationHistoryEntry? selected)
    {
        updating = true;
        try
        {
            feedback.Text = selected?.Feedback ?? string.Empty;
            available = selected is { Outcome: GenerationOutcome.Completed }
                && Directory.Exists(selected.OutputDirectory)
                && File.Exists(Path.Combine(selected.OutputDirectory, "resolved-brief.json"));
            target.Text = selected is null ? "右の生成履歴から、改善したいゲームを選んでください。"
                : $"対象: {Path.GetFileName(selected.OutputDirectory)}\n{selected.Format}";
            if (selected is not null && !available)
            {
                target.Text += "\n生成完了したゲームと成果物が必要です。";
            }
            appliedFeedback.Text = string.IsNullOrEmpty(selected?.AppliedFeedback) ? string.Empty
                : $"この版に反映した感想:\n{selected.AppliedFeedback}";
            appliedFeedback.IsVisible = appliedFeedback.Text.Length > 0;
        }
        finally
        {
            updating = false;
        }
        RefreshAvailability();
    }

    /// <summary>生成や企画の置換中に感想と対象を変更させない。</summary>
    public void SetBusy(bool value)
    {
        busy = value;
        tabs.IsEnabled = !busy;
        RefreshAvailability();
    }

    /// <summary>画面の操作監視を解除する。</summary>
    public void Dispose()
    {
        subscriptions.Dispose();
    }

    private void SetEvent()
    {
        subscriptions.Add(Changes.Merge(ModeChanges).Subscribe(_ => RefreshAvailability()));
    }

    private void RefreshAvailability()
    {
        feedback.IsEnabled = !busy && available;
        refine.IsEnabled = !busy && available && !string.IsNullOrWhiteSpace(Feedback);
        refine.IsVisible = !busy && IsRefining;
        generate.IsVisible = !busy && !IsRefining;
        format.IsEnabled = !busy && !IsRefining;
    }
}
