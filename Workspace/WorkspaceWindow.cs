using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GameMockStudio.Brief.Planning;
using GameMockStudio.Generation;
using GameMockStudio.Storage;

namespace GameMockStudio.Workspace;

/// <summary>アプリの依存関係を組み立て、画面の寿命で破棄する。</summary>
public sealed partial class WorkspaceWindow : Window
{
    private readonly WorkspacePresenter presenter;
    private readonly WorkspaceView view;
    private readonly CompositeDisposable subscriptions = new();
    private readonly Subject<Unit> closing = new();
    private bool canClose;
    private bool isClosing;
    private readonly Task initialization;
    private readonly IScheduler userInterface = new SynchronizationContextScheduler(new AvaloniaSynchronizationContext());

    /// <summary>モデル・ビュー・Presenterを構成する。</summary>
    public WorkspaceWindow() : this(WorkspaceSessionStore.DefaultDirectory)
    {
    }

    /// <summary>保存先を明示してワークスペースを作る。検証時は利用中の企画から隔離する。</summary>
    public WorkspaceWindow(string dataDirectory)
    {
        AvaloniaXamlLoader.Load(this);
        var catalogs = new FieldCatalogs();
        var controls = new WorkspaceControls(this);
        view = new WorkspaceView(controls, catalogs);
        var store = new BriefStore();
        var command = new CodexCommand();
        var runner = new CodexRunner(command, store, new ArtifactValidator(catalogs, store));
        presenter = new WorkspacePresenter(view, new WorkspaceFiles(this, store, dataDirectory),
            new PromptComposer(catalogs), runner, new WorkspaceSessionStore(dataDirectory));
        view.Configure(command.FindExecutable(), WorkspaceFiles.DefaultOutputRoot);
        IsEnabled = false;
        initialization = presenter.InitializeAsync(CancellationToken.None);
        subscriptions.Add(initialization.ToObservable().ObserveOn(userInterface).Subscribe(_ => IsEnabled = !isClosing,
            exception => ShowCloseFailure(exception)));
        subscriptions.Add(closing.Select(_ => Observable.FromAsync(PrepareCloseAsync).SubscribeOn(userInterface))
            .Concat().ObserveOn(userInterface).Where(shouldClose => shouldClose).Subscribe(_ =>
            {
                canClose = true;
                Close();
            }));
    }

    /// <summary>入力直後の終了でも保存が完了するまでウィンドウを残す。</summary>
    protected override void OnClosing(WindowClosingEventArgs arguments)
    {
        base.OnClosing(arguments);
        if (canClose)
        {
            return;
        }
        arguments.Cancel = true;
        if (isClosing)
        {
            return;
        }
        isClosing = true;
        IsEnabled = false;
        closing.OnNext(Unit.Default);
    }

    /// <summary>ウィンドウを閉じた時点で生成と購読を終了する。</summary>
    protected override void OnClosed(EventArgs arguments)
    {
        subscriptions.Dispose();
        closing.Dispose();
        presenter.Dispose();
        view.Dispose();
        base.OnClosed(arguments);
    }

    private void ShowCloseFailure(Exception exception)
    {
        isClosing = false;
        IsEnabled = true;
        view.ShowStatus($"保存・復元に失敗しました: {exception.Message}");
        view.AppendLog(exception.ToString());
    }

    private async Task<bool> PrepareCloseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await initialization;
            await presenter.PrepareCloseAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ShowCloseFailure(exception);
            return await new WorkspaceCloseDialog().ConfirmDiscardAsync(this, exception.Message);
        }
    }
}
