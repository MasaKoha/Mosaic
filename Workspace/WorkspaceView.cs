using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using GameMockStudio.Brief;
using GameMockStudio.Generation.History;
using GameMockStudio.Workspace.Editing;

namespace GameMockStudio.Workspace;

/// <summary>ワークスペースの入力と表示をPresenterへ提供する。</summary>
public sealed class WorkspaceView : IDisposable
{
    private const int MaximumLogCharacters = 60000;
    private const int LogTabIndex = 1;
    private readonly WorkspaceControls controls;
    private readonly BriefEditor editor;
    private readonly ComboBox format;
    private readonly TextBox prompt;
    private readonly TextBox log;
    private readonly TextBlock status;
    private readonly TextBox executable;
    private readonly TextBox outputRoot;
    private readonly ComboBox history;
    private readonly Dictionary<WorkspaceAction, Button> buttons;

    /// <summary>UIイベントをObservableとして公開する。</summary>
    public WorkspaceView(WorkspaceControls controls, FieldCatalog catalog)
    {
        this.controls = controls;
        editor = new BriefEditor(catalog, controls);
        format = controls.Find<ComboBox>("FormatSelector");
        prompt = controls.Find<TextBox>("PromptBox");
        log = controls.Find<TextBox>("LogBox");
        status = controls.Find<TextBlock>("StatusText");
        executable = controls.Find<TextBox>("ExecutableBox");
        outputRoot = controls.Find<TextBox>("OutputRootBox");
        history = controls.Find<ComboBox>("HistorySelector");
        buttons = Enum.GetValues<WorkspaceAction>().ToDictionary(action => action,
            action => controls.Find<Button>(action + "Button"));
        Commands = buttons.Select(pair => pair.Value.GetObservable(Button.ClickEvent).Select(_ => pair.Key)).Merge();
        Changes = editor.Changes.Merge(format.GetObservable(SelectingItemsControl.SelectedIndexProperty)
            .Skip(1).Select(_ => Unit.Default));
        SettingsChanges = executable.GetObservable(TextBox.TextProperty)
            .Merge(outputRoot.GetObservable(TextBox.TextProperty)).Skip(2).Select(_ => Unit.Default);
        HistorySelection = history.GetObservable(SelectingItemsControl.SelectedIndexProperty)
            .Skip(1).Select(_ => Unit.Default);
    }

    /// <summary>利用者が要求した操作。</summary>
    public IObservable<WorkspaceAction> Commands { get; }
    /// <summary>企画または生成形式の変更。</summary>
    public IObservable<Unit> Changes { get; }
    /// <summary>生成の接続設定または出力先の変更。</summary>
    public IObservable<Unit> SettingsChanges { get; }
    /// <summary>閲覧する生成履歴の変更。</summary>
    public IObservable<Unit> HistorySelection { get; }
    /// <summary>選択中の生成履歴の位置。</summary>
    public int SelectedHistoryIndex => history.SelectedIndex;
    /// <summary>表示中のCLI実行ファイル。</summary>
    public string Executable => executable.Text?.Trim() ?? string.Empty;
    /// <summary>表示中の出力先。</summary>
    public string OutputRoot => outputRoot.Text?.Trim() ?? string.Empty;

    /// <summary>起動時のローカル設定を表示する。</summary>
    public void Configure(string executablePath, string outputDirectory)
    {
        executable.Text = executablePath;
        outputRoot.Text = outputDirectory;
    }

    /// <summary>全企画入力を取得する。</summary>
    public BriefDocument CaptureBrief()
    {
        return editor.Capture((MockFormat)format.SelectedIndex);
    }

    /// <summary>読み込んだ企画を表示する。</summary>
    public void ApplyBrief(BriefDocument document)
    {
        editor.Apply(document);
        format.SelectedIndex = (int)document.Format;
    }

    /// <summary>生成指示のプレビューを更新する。</summary>
    public void ShowPrompt(string text)
    {
        prompt.Text = text;
    }

    /// <summary>指定された出力先を表示する。</summary>
    public void SetOutputRoot(string path)
    {
        outputRoot.Text = path;
    }

    /// <summary>次に必要な判断や現在の状況を表示する。</summary>
    public void ShowStatus(string message)
    {
        status.Text = message;
    }

    /// <summary>生成状況と混同しない位置へ保存状態を表示する。</summary>
    public void ShowSaveStatus(string message)
    {
        controls.Find<TextBlock>("SaveStatusText").Text = message;
    }

    /// <summary>生成履歴を表示して最新の結果を選択する。</summary>
    public void ShowHistory(IReadOnlyList<GenerationHistoryEntry> entries)
    {
        history.ItemsSource = entries.Select(entry =>
            $"{entry.StartedAt.ToLocalTime():MM/dd HH:mm} · {DescribeOutcome(entry.Outcome)} · {entry.Format}").ToArray();
        history.SelectedIndex = entries.Count > 0 ? 0 : -1;
    }

    /// <summary>実行ログを切り替え、過去の結果表示を初期化する。</summary>
    public void BeginGeneration()
    {
        log.Text = string.Empty;
        controls.Find<TabControl>("OutputTabs").SelectedIndex = LogTabIndex;
        controls.Find<TextBlock>("OutputPathText").Text = string.Empty;
        SetOutputAvailability(OutputAvailability.None);
        SetRunning(true);
    }

    /// <summary>生成中の重複操作と入力変更を防ぐ。</summary>
    public void SetRunning(bool running)
    {
        controls.Find<StackPanel>("ConfigurationPanel").IsEnabled = !running;
        controls.Find<StackPanel>("EditorHost").IsEnabled = !running;
        foreach (var action in new[] { WorkspaceAction.New, WorkspaceAction.Load, WorkspaceAction.Save, WorkspaceAction.Generate })
        {
            buttons[action].IsEnabled = !running;
        }
        buttons[WorkspaceAction.Cancel].IsVisible = running;
        buttons[WorkspaceAction.Generate].IsVisible = !running;
        history.IsEnabled = !running;
        if (running)
        {
            buttons[WorkspaceAction.UseResolved].IsEnabled = false;
        }
    }

    /// <summary>企画を退避・置換している間の追加入力による消失を防ぐ。</summary>
    public void SetFileOperation(bool busy)
    {
        controls.Find<StackPanel>("ConfigurationPanel").IsEnabled = !busy;
        controls.Find<StackPanel>("EditorHost").IsEnabled = !busy;
        foreach (var action in new[] { WorkspaceAction.New, WorkspaceAction.Load, WorkspaceAction.Save, WorkspaceAction.Generate })
        {
            buttons[action].IsEnabled = !busy;
        }
    }

    /// <summary>過大なログで画面が重くならない範囲で進捗を追加する。</summary>
    public void AppendLog(string message)
    {
        var combined = (log.Text ?? string.Empty) + message + Environment.NewLine;
        log.Text = combined.Length > MaximumLogCharacters ? combined[^MaximumLogCharacters..] : combined;
        log.CaretIndex = log.Text.Length;
    }

    /// <summary>今回の成果物の場所を表示する。</summary>
    public void ShowOutputPath(string path)
    {
        controls.Find<TextBlock>("OutputPathText").Text = path;
    }

    /// <summary>成果物に対して利用可能な操作を表示する。</summary>
    public void SetOutputAvailability(OutputAvailability availability)
    {
        buttons[WorkspaceAction.OpenOutput].IsEnabled = availability.HasFlag(OutputAvailability.OpenDirectory);
        buttons[WorkspaceAction.Play].IsEnabled = availability.HasFlag(OutputAvailability.Play);
        buttons[WorkspaceAction.UseResolved].IsEnabled = availability.HasFlag(OutputAvailability.UseResolved);
    }

    /// <summary>画面が所有する購読を解除する。</summary>
    public void Dispose()
    {
        editor.Dispose();
    }

    private string DescribeOutcome(GenerationOutcome outcome)
    {
        return outcome switch
        {
            GenerationOutcome.Running => "生成中",
            GenerationOutcome.Completed => "生成完了",
            GenerationOutcome.Failed => "失敗",
            GenerationOutcome.Cancelled => "取消",
            _ => "中断"
        };
    }
}
