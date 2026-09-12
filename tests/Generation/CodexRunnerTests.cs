using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Generation;
using GameMockStudio.Generation.Completion;
using GameMockStudio.Tests.Generation.Fixtures;
using Xunit;

namespace GameMockStudio.Tests.Generation;

/// <summary>偽CLIとの実際のパイプ通信、終了判定、購読解除による停止を検証する。</summary>
[UnsupportedOSPlatform("windows")]
public sealed class CodexRunnerTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ExecutionTimeLimit = TimeSpan.FromSeconds(2);
    private const string CompletedEvent = """printf '{"type":"turn.completed"}\n'""";
    private const string WaitingProcess = """
        printf '%s\n' "$$" > process.pid
        sleep 60 &
        child_process=$!
        printf '%s\n' "$child_process" > child.pid
        printf '{"type":"item.started","item":{"text":"waiting"}}\n'
        wait "$child_process"
        """;

    /// <summary>大量の標準エラーがあっても停止せず、入力をそのまま渡してログを保存する。</summary>
    [UnixFact]
    public async Task SuccessfulProcessPreservesInputAndDrainsBothStreams()
    {
        using var fake = new FakeCodexProcess("cat diagnostics-fixture.txt >&2\n" + CompletedEvent);
        using var timeout = new CancellationTokenSource(TestTimeout);

        var update = await fake.CreateRunner().Run(fake.Request).Do(fake.PrepareArtifacts).ToTask(timeout.Token);

        Assert.Equal(GenerationStage.Completed, update.Stage);
        Assert.Equal(fake.Request.Prompt, await File.ReadAllTextAsync(Path.Combine(update.OutputDirectory, "observed-prompt.txt")));
        Assert.Equal(fake.Diagnostics, await File.ReadAllTextAsync(Path.Combine(update.OutputDirectory, "diagnostics.log")));
        Assert.Contains("turn.completed", await File.ReadAllTextAsync(Path.Combine(update.OutputDirectory, "events.jsonl")));
    }

    /// <summary>終了コードが0でもターン完了イベントがなければ成功にしない。</summary>
    [UnixFact]
    public async Task ExitWithoutCompletedEventIsRejected()
    {
        using var fake = new FakeCodexProcess("printf 'CLI stopped before completion\n'");
        using var timeout = new CancellationTokenSource(TestTimeout);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fake.CreateRunner().Run(fake.Request).Do(fake.PrepareArtifacts).ToTask(timeout.Token));

        Assert.Contains("終了コード 0", exception.Message);
    }

    /// <summary>完了イベントがあってもプロセスの失敗終了を成功にしない。</summary>
    [UnixFact]
    public async Task NonZeroExitAfterCompletedEventIsRejected()
    {
        using var fake = new FakeCodexProcess(CompletedEvent + "\nexit 9");
        using var timeout = new CancellationTokenSource(TestTimeout);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fake.CreateRunner().Run(fake.Request).Do(fake.PrepareArtifacts).ToTask(timeout.Token));

        Assert.Contains("終了コード 9", exception.Message);
    }

    /// <summary>CLI自体が正常終了しても実装結果が未完了なら生成成功を通知しない。</summary>
    [UnixFact]
    public async Task IncompleteReportRejectsOtherwiseSuccessfulProcess()
    {
        using var fake = new FakeCodexProcess(CompletedEvent, GenerationOutcome.Incomplete);
        using var timeout = new CancellationTokenSource(TestTimeout);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fake.CreateRunner().Run(fake.Request).Do(fake.PrepareArtifacts).ToTask(timeout.Token));

        Assert.Contains("未完了", exception.Message);
    }

    /// <summary>ログ保存に失敗しても読み取り停止によるプロセス待機を残さない。</summary>
    [UnixFact]
    public async Task LogPersistenceFailureTerminatesGeneration()
    {
        using var fake = new FakeCodexProcess("sleep 60");
        using var timeout = new CancellationTokenSource(TestTimeout);

        var exception = await Record.ExceptionAsync(() => fake.CreateRunner().Run(fake.Request).Do(update =>
        {
            fake.PrepareArtifacts(update);
            if (update.Stage == GenerationStage.Prepared)
            {
                Directory.CreateDirectory(Path.Combine(update.OutputDirectory, "events.jsonl"));
            }
        }).ToTask(timeout.Token));

        Assert.True(exception is IOException or UnauthorizedAccessException);
    }

    /// <summary>時間上限で子孫プロセスまで停止し、利用者取消とは異なる失敗を通知する。</summary>
    [UnixFact]
    public async Task ExecutionTimeLimitStopsProcessTreeAndReportsTimeout()
    {
        using var fake = new FakeCodexProcess(WaitingProcess);
        using var timeout = new CancellationTokenSource(TestTimeout);
        var running = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = fake.Request with { ExecutionTimeout = ExecutionTimeLimit };
        var generation = fake.CreateRunner().Run(request).Do(update =>
        {
            fake.PrepareArtifacts(update);
            if (update.Message == "waiting")
            {
                running.TrySetResult(update.OutputDirectory);
            }
        }, exception => running.TrySetException(exception)).ToTask(timeout.Token);
        var directory = await running.Task.WaitAsync(TestTimeout);
        using var process = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "process.pid"))));
        using var child = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "child.pid"))));

        var exception = await Assert.ThrowsAsync<TimeoutException>(() => generation);

        Assert.True(process.HasExited);
        await child.WaitForExitAsync().WaitAsync(TestTimeout);
        Assert.Contains("制限時間", exception.Message);
    }

    /// <summary>生成の購読解除で子孫まで停止し、解除後の通知や成果物削除を発生させない。</summary>
    [UnixFact]
    public async Task DisposingSubscriptionStopsProcessTreeAndRetainsPartialOutput()
    {
        using var fake = new FakeCodexProcess(WaitingProcess);
        var running = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var notificationCount = 0;
        using var subscription = fake.CreateRunner().Run(fake.Request).Subscribe(update =>
        {
            Interlocked.Increment(ref notificationCount);
            fake.PrepareArtifacts(update);
            if (update.Message == "waiting")
            {
                running.TrySetResult(update.OutputDirectory);
            }
        }, exception => running.TrySetException(exception));
        var directory = await running.Task.WaitAsync(TestTimeout);
        using var process = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "process.pid"))));
        using var child = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "child.pid"))));

        subscription.Dispose();
        var countAfterDisposal = Volatile.Read(ref notificationCount);
        await Task.WhenAll(process.WaitForExitAsync(), child.WaitForExitAsync()).WaitAsync(TestTimeout);

        Assert.Equal(countAfterDisposal, Volatile.Read(ref notificationCount));
        Assert.True(File.Exists(Path.Combine(directory, "request.json")));
        Assert.True(File.Exists(Path.Combine(directory, "prompt.md")));
    }
}
