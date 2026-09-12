using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Brief;
using GameMockStudio.Storage;
using Xunit;

namespace GameMockStudio.Tests.Storage;

/// <summary>保存キャンセルと破損入力で、既存企画を失わないことを守る。</summary>
public sealed class WorkspaceSessionStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "mock-session-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>中断した上書きが、直前の保存を壊さない。</summary>
    [Fact]
    public async Task CancelledSavePreservesPreviousDocument()
    {
        using var store = new WorkspaceSessionStore(directory);
        await store.SaveAsync(CreateSession("保存済み"), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.SaveAsync(CreateSession("中断した内容"), cancellation.Token));
        var loaded = await store.LoadAsync(CancellationToken.None);
        Assert.Equal("保存済み", loaded!.Brief.Values["world_theme"]);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    /// <summary>破損した履歴を読み込んでも原本を維持し、別名で取り出せる。</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("{\"StartedAt\":\"2026-09-12T00:00:00Z\",\"OutputDirectory\":null,\"Format\":0,\"Outcome\":0}")]
    public async Task InvalidHistoryCanBePreservedWithoutOverwriting(string historyEntry)
    {
        Directory.CreateDirectory(directory);
        using var store = new WorkspaceSessionStore(directory);
        var content = "{\"Version\":1,\"Brief\":{},\"Executable\":\"codex\",\"OutputRoot\":\"/tmp\",\"History\":[" + historyEntry + "]}";
        await File.WriteAllTextAsync(store.SessionPath, content, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync(CancellationToken.None));
        Assert.Equal(content, await File.ReadAllTextAsync(store.SessionPath, TestContext.Current.CancellationToken));
        var backup = store.PreserveUnreadableSession();
        await store.SaveAsync(CreateSession("復旧後"), CancellationToken.None);
        Assert.Equal(content, await File.ReadAllTextAsync(backup, TestContext.Current.CancellationToken));
    }

    /// <summary>隔離した保存先だけを破棄する。</summary>
    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private WorkspaceSession CreateSession(string theme)
    {
        return new WorkspaceSession
        {
            Brief = new BriefDocument { Values = new() { ["world_theme"] = theme } },
            Executable = "codex",
            OutputRoot = directory,
            History = []
        };
    }
}
