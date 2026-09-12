using System;
using System.IO;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using GameMockStudio.Brief;
using GameMockStudio.Generation;
using GameMockStudio.Generation.History;
using GameMockStudio.Generation.Refinement;
using GameMockStudio.Storage;

namespace GameMockStudio.Workspace;

/// <summary>企画の編集、保存、生成のユーザーフローを制御する。</summary>
public sealed class WorkspacePresenter : IDisposable
{
    private static readonly TimeSpan PreviewDelay = TimeSpan.FromMilliseconds(200);
    private readonly WorkspaceView view;
    private readonly WorkspaceFiles files;
    private readonly PromptComposer composer;
    private readonly CodexRunner runner;
    private readonly GenerationHistory history = new();
    private readonly WorkspacePersistence persistence;
    private readonly IScheduler userInterface = new SynchronizationContextScheduler(new AvaloniaSynchronizationContext());
    private readonly CompositeDisposable subscriptions = new();
    private readonly SerialDisposable generation = new();
    private readonly SemaphoreSlim operationLock = new(1, 1);
    private WorkspaceState state = WorkspaceState.Editing;
    private string variationIdentifier = Guid.NewGuid().ToString("N");
    private string outputDirectory = string.Empty;
    private MockFormat generatedFormat;
    private GenerationHistoryEntry? activeGeneration;
    private RefinementRequest? activeRefinement;

    /// <summary>画面の表示責務とファイル・生成の処理を接続する。</summary>
    public WorkspacePresenter(WorkspaceView view, WorkspaceFiles files, PromptComposer composer,
        CodexRunner runner, WorkspaceSessionStore sessionStore)
    {
        this.view = view;
        this.files = files;
        this.composer = composer;
        this.runner = runner;
        persistence = new WorkspacePersistence(view, history, sessionStore, userInterface);
        subscriptions.Add(generation);
        subscriptions.Add(history);
    }

    /// <summary>Rxで入力・コマンド・進行通知を購読する。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await persistence.InitializeAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ShowFailure(exception);
            view.ShowSaveStatus("自動保存を復元できません。企画を手動保存してください。");
        }
        view.ShowHistory(history.Entries);
        ShowSelectedOutput();
        SetEvent();
        RefreshPrompt();
    }

    private void SetEvent()
    {
        subscriptions.Add(history.Changes.Subscribe(_ =>
        {
            view.ShowHistory(history.Entries);
            ShowSelectedOutput();
        }));
        subscriptions.Add(view.HistorySelection.Subscribe(_ =>
        {
            ShowSelectedOutput();
            RefreshPrompt();
        }));
        subscriptions.Add(view.FeedbackEditor.Changes.Subscribe(_ => SaveFeedback()));
        subscriptions.Add(view.FeedbackEditor.ModeChanges.Subscribe(_ => RefreshPrompt()));
        subscriptions.Add(view.Changes.Merge(view.FeedbackEditor.Changes).Throttle(PreviewDelay).ObserveOn(userInterface)
            .Subscribe(_ => RefreshPrompt(), ShowFailure));
        subscriptions.Add(view.Commands.Where(action => action == WorkspaceAction.Cancel)
            .Subscribe(_ => CancelGeneration()));
        subscriptions.Add(view.Commands.Where(action => action != WorkspaceAction.Cancel)
            .Select(action => Observable.FromAsync(cancellationToken => ExecuteAsync(action, cancellationToken))
                .SubscribeOn(userInterface).ObserveOn(userInterface)
                .Catch<Unit, Exception>(exception =>
                {
                    ShowFailure(exception);
                    return Observable.Empty<Unit>();
                }))
            .Concat().Subscribe());
    }

    /// <summary>生成を停止し、最後のキー入力を保存してから終了を許可する。</summary>
    public async Task PrepareCloseAsync(CancellationToken cancellationToken)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            CancelGeneration();
            state = WorkspaceState.Closing;
            await persistence.FlushAsync(cancellationToken);
        }
        catch
        {
            state = WorkspaceState.Editing;
            throw;
        }
        finally
        {
            operationLock.Release();
        }
    }

    /// <summary>実行中の生成と全購読をウィンドウの寿命で破棄する。</summary>
    public void Dispose()
    {
        subscriptions.Dispose();
        persistence.Dispose();
        operationLock.Dispose();
    }

    private async Task ExecuteAsync(WorkspaceAction action, CancellationToken cancellationToken)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            await ExecuteCommandAsync(action, cancellationToken);
        }
        finally
        {
            operationLock.Release();
        }
    }

    private async Task ExecuteCommandAsync(WorkspaceAction action, CancellationToken cancellationToken)
    {
        if (state == WorkspaceState.Closing ||
            (state == WorkspaceState.Generating && action != WorkspaceAction.OpenOutput))
        {
            return;
        }
        switch (action)
        {
            case WorkspaceAction.New:
                await ApplyDocumentAsync(new BriefDocument(), cancellationToken);
                break;
            case WorkspaceAction.Load:
                await LoadAsync(cancellationToken);
                break;
            case WorkspaceAction.Save:
                var savedPath = await files.SaveBriefAsync(view.CaptureBrief(), cancellationToken);
                if (savedPath is not null)
                {
                    view.ShowStatus($"企画を保存しました: {savedPath}");
                }
                break;
            case WorkspaceAction.ChooseOutput:
                var directory = await files.ChooseOutputAsync();
                if (directory is not null)
                {
                    view.SetOutputRoot(directory);
                }
                break;
            case WorkspaceAction.Generate:
                StartGeneration(null);
                break;
            case WorkspaceAction.Refine:
                var selected = SelectedHistory();
                if (selected is not { Outcome: GenerationOutcome.Completed })
                {
                    throw new InvalidOperationException("生成完了したゲームを履歴から選んでください。");
                }
                StartGeneration(new RefinementRequest
                {
                    SourceDirectory = selected.OutputDirectory,
                    Feedback = view.FeedbackEditor.Feedback
                });
                break;
            case WorkspaceAction.OpenOutput:
                files.Open(outputDirectory);
                break;
            case WorkspaceAction.Play:
                files.Open(Path.Combine(outputDirectory, "index.html"));
                break;
            case WorkspaceAction.UseResolved:
                await ApplyDocumentAsync(await files.LoadResolvedAsync(outputDirectory, cancellationToken), cancellationToken);
                break;
            case WorkspaceAction.OpenRecovery:
                files.OpenRecovery();
                break;
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var document = await files.LoadBriefAsync(cancellationToken);
        if (document is null)
        {
            return;
        }
        await ApplyDocumentAsync(document, cancellationToken);
    }

    private async Task ApplyDocumentAsync(BriefDocument document, CancellationToken cancellationToken)
    {
        view.SetFileOperation(true);
        try
        {
            var backup = await files.SaveRecoveryAsync(view.CaptureBrief(), cancellationToken);
            view.ApplyBrief(document);
            variationIdentifier = Guid.NewGuid().ToString("N");
            RefreshPrompt();
            view.ShowStatus($"企画を切り替えました。直前の入力の退避先: {backup}");
        }
        finally
        {
            view.SetFileOperation(false);
        }
    }

    private void StartGeneration(RefinementRequest? refinement)
    {
        if (string.IsNullOrWhiteSpace(view.Executable) || !Path.IsPathFullyQualified(view.OutputRoot))
        {
            throw new InvalidOperationException("Codex実行ファイルと、絶対パスの出力先を指定してください。");
        }
        refinement?.Validate();
        var brief = refinement is null ? view.CaptureBrief() : new BriefDocument { Format = SelectedHistory()!.Format };
        var prompt = refinement is null ? composer.Compose(brief, variationIdentifier)
            : composer.ComposeRefinement(brief.Format, refinement.Feedback, variationIdentifier);
        view.ShowPrompt(prompt);
        var request = new GenerationRequest
        {
            Brief = brief,
            Prompt = prompt,
            Executable = view.Executable,
            OutputRoot = view.OutputRoot,
            Refinement = refinement
        };
        state = WorkspaceState.Generating;
        generatedFormat = brief.Format;
        outputDirectory = string.Empty;
        activeGeneration = null;
        activeRefinement = refinement;
        view.BeginGeneration();
        view.ShowStatus($"Codex GPT-6 {CodexCommand.ReasoningEffort}で生成中。完了まで数分かかる場合があります。");
        // ObserveOnの即時エラーが準備通知を追い越すと履歴を失うため、終端も通常通知としてキューへ載せる。
        generation.Disposable = runner.Run(request).SubscribeOn(TaskPoolScheduler.Default).Materialize()
            .ObserveOn(userInterface).Dematerialize().Subscribe(ReceiveUpdate, GenerationFailed, GenerationEnded);
    }

    private void ReceiveUpdate(GenerationUpdate update)
    {
        outputDirectory = update.OutputDirectory;
        if (update.Stage == GenerationStage.Prepared)
        {
            activeGeneration = new GenerationHistoryEntry
            {
                StartedAt = DateTimeOffset.Now,
                OutputDirectory = outputDirectory,
                Format = generatedFormat,
                Outcome = GenerationOutcome.Running,
                SourceDirectory = activeRefinement?.SourceDirectory ?? string.Empty,
                AppliedFeedback = activeRefinement?.Feedback ?? string.Empty
            };
            history.Record(activeGeneration);
            view.SelectLatestHistory();
            ShowSelectedOutput();
        }
        view.ShowOutputPath(outputDirectory);
        view.AppendLog(update.Message);
        var completed = update.Stage == GenerationStage.Completed;
        view.SetOutputAvailability(OutputAvailability.OpenDirectory);
        if (completed)
        {
            RecordOutcome(GenerationOutcome.Completed);
            view.ShowStatus(update.Message);
        }
    }

    private void CancelGeneration()
    {
        if (state != WorkspaceState.Generating)
        {
            return;
        }
        generation.Disposable = Disposable.Empty;
        RecordOutcome(GenerationOutcome.Cancelled);
        GenerationEnded();
        view.AppendLog("利用者が生成をキャンセルしました。");
        view.ShowStatus("生成をキャンセルしました。途中のファイルとログは成果物フォルダに残しています。");
    }

    private void GenerationFailed(Exception exception)
    {
        RecordOutcome(GenerationOutcome.Failed);
        GenerationEnded();
        ShowFailure(exception);
    }

    private void GenerationEnded()
    {
        state = WorkspaceState.Editing;
        view.SetRunning(false);
        ShowSelectedOutput();
        variationIdentifier = Guid.NewGuid().ToString("N");
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        if (state != WorkspaceState.Editing)
        {
            return;
        }
        var selected = SelectedHistory();
        if (view.FeedbackEditor.IsRefining)
        {
            var prompt = selected is null ? "改善するゲームを生成履歴から選んでください。"
                : composer.ComposeRefinement(selected.Format, view.FeedbackEditor.Feedback, variationIdentifier);
            view.ShowPrompt(prompt);
            return;
        }
        view.ShowPrompt(composer.Compose(view.CaptureBrief(), variationIdentifier));
    }

    private void ShowFailure(Exception exception)
    {
        view.ShowStatus(exception.Message);
        view.AppendLog(exception.ToString());
    }

    private void RecordOutcome(GenerationOutcome outcome)
    {
        if (activeGeneration is null)
        {
            return;
        }
        activeGeneration = activeGeneration with { Outcome = outcome };
        history.Record(activeGeneration);
    }

    private void ShowSelectedOutput()
    {
        var selected = SelectedHistory();
        view.FeedbackEditor.ShowTarget(selected);
        if (selected is null)
        {
            return;
        }
        outputDirectory = selected.OutputDirectory;
        view.ShowOutputPath(outputDirectory);
        var completed = selected.Outcome == GenerationOutcome.Completed;
        var availability = Directory.Exists(outputDirectory) ? OutputAvailability.OpenDirectory : OutputAvailability.None;
        if (completed && selected.Format == MockFormat.Browser && File.Exists(Path.Combine(outputDirectory, "index.html")))
        {
            availability |= OutputAvailability.Play;
        }
        if (completed && state == WorkspaceState.Editing && File.Exists(Path.Combine(outputDirectory, "resolved-brief.json")))
        {
            availability |= OutputAvailability.UseResolved;
        }
        view.SetOutputAvailability(availability);
    }

    private GenerationHistoryEntry? SelectedHistory()
    {
        var selectedIndex = view.SelectedHistoryIndex;
        return selectedIndex >= 0 && selectedIndex < history.Entries.Count ? history.Entries[selectedIndex] : null;
    }

    private void SaveFeedback()
    {
        var selected = SelectedHistory();
        if (selected is not null && state == WorkspaceState.Editing)
        {
            history.UpdateFeedback(selected.OutputDirectory, view.FeedbackEditor.Feedback);
        }
    }
}
