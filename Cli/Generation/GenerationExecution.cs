using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using GameMockStudio.Generation;

namespace GameMockStudio.Cli.Generation;

/// <summary>生成進捗を出力し、Ctrl+Cと完了を購読の寿命へ結び付ける。</summary>
internal sealed class GenerationExecution(CodexRunner runner, TextWriter output, bool useJson)
{
    private readonly CliJson json = new(output);

    /// <summary>成果物検証まで待ち、成功時に実際の出力先を通知する。</summary>
    public async Task RunAsync(GenerationRequest request)
    {
        using var subscriptions = new CompositeDisposable();
        var completion = new TaskCompletionSource<GenerationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetEvent(request, completion, subscriptions);
        var completed = await completion.Task;
        if (completed.Stage != GenerationStage.Completed)
        {
            throw new InvalidOperationException("生成の完了通知を受け取れませんでした。");
        }
        WriteUpdate(completed with { Message = $"出力: {completed.OutputDirectory}" });
    }

    private void SetEvent(GenerationRequest request, TaskCompletionSource<GenerationUpdate> completion,
        CompositeDisposable subscriptions)
    {
        var cancellation = Observable.FromEvent<ConsoleCancelEventHandler, ConsoleCancelEventArgs>(
                handler => (_, arguments) => handler(arguments),
                handler => Console.CancelKeyPress += handler,
                handler => Console.CancelKeyPress -= handler)
            .Do(arguments => arguments.Cancel = true)
            .SelectMany(_ => Observable.Throw<Unit>(new OperationCanceledException("生成を中断しました。途中の成果物は残ります。")));
        // キャンセルもエラー終端へ合成し、TakeUntilによる購読解除でプロセスツリーを停止する。
        subscriptions.Add(runner.Run(request).TakeUntil(cancellation).Do(WriteUpdate).LastAsync().Subscribe(
            update => completion.TrySetResult(update), exception => completion.TrySetException(exception)));
    }

    private void WriteUpdate(GenerationUpdate update)
    {
        if (useJson)
        {
            json.WriteLine(update);
            return;
        }
        output.WriteLine(update.Message.ReplaceLineEndings(" "));
        output.Flush();
    }
}
