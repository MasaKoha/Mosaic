using System;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Storage;

namespace GameMockStudio.Generation;

/// <summary>Codexのプロセスとログ、キャンセル、成果物検証を一回の生成として管理する。</summary>
public sealed class CodexRunner(CodexCommand command, BriefStore store, ArtifactValidator validator)
{
    /// <summary>購読の解除で子プロセスを終了し、生成の進捗を通知する。</summary>
    public IObservable<GenerationUpdate> Run(GenerationRequest request)
    {
        return Observable.Create<GenerationUpdate>(async (observer, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var synchronized = Observer.Synchronize(observer);
            var directory = CreateOutputDirectory(request.OutputRoot);
            synchronized.OnNext(new GenerationUpdate
            {
                Stage = GenerationStage.Prepared,
                Message = $"生成先: {directory}",
                OutputDirectory = directory
            });
            await store.SaveAsync(Path.Combine(directory, "request.json"), request.Brief, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "prompt.md"), request.Prompt, cancellationToken);
            await ExecuteWithinTimeLimitAsync(request, directory, synchronized, cancellationToken);
            await validator.ValidateAsync(directory, request.Brief, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            synchronized.OnNext(new GenerationUpdate
            {
                Stage = GenerationStage.Completed,
                Message = "生成完了。完了レポート、必須ファイル、企画の整合を確認しました。ゲームの動作は未確認です。",
                OutputDirectory = directory
            });
        });
    }

    private string CreateOutputDirectory(string outputRoot)
    {
        if (!Path.IsPathFullyQualified(outputRoot))
        {
            throw new InvalidOperationException("出力先は絶対パスで指定してください。");
        }
        var folderName = $"mock-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        var directory = Path.Combine(outputRoot, folderName);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private async Task ExecuteAsync(GenerationRequest request, string directory,
        IObserver<GenerationUpdate> observer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = command.CreateStartInfo(request.Executable, directory) };
        if (!process.Start())
        {
            throw new InvalidOperationException("Codexを起動できませんでした。");
        }
        using var registration = cancellationToken.Register(() => StopProcess(process));
        var reader = new CodexEventReader();
        var output = StopOnFailureAsync(PumpAsync(process.StandardOutput, Path.Combine(directory, "events.jsonl"), reader,
            observer, cancellationToken), process);
        var diagnostics = StopOnFailureAsync(PumpDiagnosticsAsync(process.StandardError, Path.Combine(directory, "diagnostics.log"),
            observer, cancellationToken), process);
        try
        {
            await process.StandardInput.WriteAsync(request.Prompt.AsMemory(), cancellationToken);
            process.StandardInput.Close();
            await Task.WhenAll(output, diagnostics, process.WaitForExitAsync(cancellationToken));
            if (process.ExitCode != 0 || !reader.TurnCompleted || reader.Failure.Length > 0)
            {
                throw new InvalidOperationException($"Codexが完了しませんでした（終了コード {process.ExitCode}）。{reader.Failure}\n認証・利用上限・権限制限の詳細はログを確認してください。自動再試行は行いません。");
            }
        }
        finally
        {
            StopProcess(process);
            // キャンセルされたストリームはプロセスより先に終了するため、OS側の終了も待ってから破棄する。
            await process.WaitForExitAsync(CancellationToken.None);
            // 入力書き込みが先に失敗しても、ストリーム処理が破棄済みProcessへ触れないよう終了を待つ。
            try
            {
                await Task.WhenAll(output, diagnostics);
            }
            catch (Exception exception) when (exception is IOException or OperationCanceledException)
            {
            }
        }
    }

    private async Task ExecuteWithinTimeLimitAsync(GenerationRequest request, string directory,
        IObserver<GenerationUpdate> observer, CancellationToken cancellationToken)
    {
        if (request.ExecutionTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Codexの実行時間上限は0より長い時間を指定してください。");
        }
        using var timeout = new CancellationTokenSource(request.ExecutionTimeout);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await ExecuteAsync(request, directory, observer, execution.Token);
        }
        catch (Exception exception) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // 利用者の取消は購読解除として扱い、時間切れだけを再編集できる失敗として画面へ通知する。
            throw new TimeoutException($"Codexの実行が制限時間（{request.ExecutionTimeout:g}）を超えたため停止しました。"
                + "途中の成果物とログは保存されています。自動再試行は行いません。", exception);
        }
    }

    private async Task PumpAsync(StreamReader stream, string logPath, CodexEventReader reader,
        IObserver<GenerationUpdate> observer, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(logPath);
        while (await stream.ReadLineAsync(cancellationToken) is { } line)
        {
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            observer.OnNext(new GenerationUpdate
            {
                Stage = GenerationStage.Running,
                Message = reader.Read(line),
                OutputDirectory = Path.GetDirectoryName(logPath)!
            });
        }
    }

    private async Task PumpDiagnosticsAsync(StreamReader stream, string logPath,
        IObserver<GenerationUpdate> observer, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(logPath);
        while (await stream.ReadLineAsync(cancellationToken) is { } line)
        {
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            observer.OnNext(new GenerationUpdate
            {
                Stage = GenerationStage.Running,
                Message = line,
                OutputDirectory = Path.GetDirectoryName(logPath)!
            });
        }
    }

    private async Task StopOnFailureAsync(Task streamOperation, Process process)
    {
        try
        {
            await streamOperation;
        }
        catch
        {
            // ログ保存に失敗して読み取りが止まると、子プロセスがパイプ満杯で待ち続けるため先に停止する。
            StopProcess(process);
            throw;
        }
    }

    private void StopProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // 終了通知とキャンセルが競合した場合、すでに停止したプロセスを再停止する必要はない。
        }
    }
}
