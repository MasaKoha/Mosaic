using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Generation.History;
using GameMockStudio.Storage;

namespace GameMockStudio.Workspace;

/// <summary>編集変更の自動保存と、起動・終了時の引き継ぎを接続する。</summary>
public sealed class WorkspacePersistence(
    WorkspaceView view, GenerationHistory history, WorkspaceSessionStore store, IScheduler userInterface) : IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(700);
    private readonly SerialDisposable automaticSave = new();
    private bool restored;
    private long revision;

    /// <summary>前回の入力・設定・履歴を復元し、変更後の自動保存を開始する。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var session = await store.LoadAsync(cancellationToken);
            if (session is not null)
            {
                view.ApplyBrief(session.Brief);
                view.Configure(session.Executable, session.OutputRoot);
                history.Restore(session.History);
                view.ShowStatus("前回の企画・設定・生成履歴を復元しました。");
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            var backup = store.PreserveUnreadableSession();
            view.ShowStatus($"自動保存を読み込めませんでした。元ファイルの退避先: {backup}");
            view.AppendLog(exception.Message);
        }
        restored = true;
        Resume();
        view.ShowSaveStatus("入力と設定は自動保存されます。");
    }

    /// <summary>待機中の保存を取り消し、画面の最新状態が保存されるまで待つ。</summary>
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (!restored)
        {
            throw new InvalidOperationException("前回データを読み込めていません。企画を手動保存してからアプリを終了してください。");
        }
        automaticSave.Disposable = Disposable.Empty;
        try
        {
            await store.SaveAsync(Capture(), cancellationToken);
            view.ShowSaveStatus("自動保存済み");
        }
        catch
        {
            Resume();
            throw;
        }
    }

    /// <summary>変更監視を停止する。終了時の書き込み完了後に呼び出す。</summary>
    public void Dispose()
    {
        automaticSave.Dispose();
        store.Dispose();
    }

    private void Resume()
    {
        automaticSave.Disposable = view.Changes.Merge(view.SettingsChanges).Merge(history.Changes)
            .Do(_ =>
            {
                revision++;
                view.ShowSaveStatus("未保存の変更があります…");
            })
            .Throttle(SaveDelay).ObserveOn(userInterface)
            .Select(_ => (Revision: revision, Session: Capture()))
            .Select(snapshot => Observable.FromAsync(cancellationToken => store.SaveAsync(snapshot.Session, cancellationToken))
                .ObserveOn(userInterface)
                .Do(_ =>
                {
                    if (revision == snapshot.Revision)
                    {
                        view.ShowSaveStatus("自動保存済み");
                    }
                })
                .Catch<Unit, Exception>(exception =>
                {
                    view.ShowSaveStatus("自動保存に失敗しました。企画を保存してください。");
                    view.AppendLog(exception.Message);
                    return Observable.Empty<Unit>();
                }))
            .Switch().Subscribe();
    }

    private WorkspaceSession Capture()
    {
        return new WorkspaceSession
        {
            Brief = view.CaptureBrief(),
            Executable = view.Executable,
            OutputRoot = view.OutputRoot,
            History = history.Entries.ToArray()
        };
    }
}
