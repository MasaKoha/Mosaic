using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Brief;
using GameMockStudio.Brief.Planning;
using GameMockStudio.Storage;
using Xunit;

namespace GameMockStudio.Tests.Storage;

/// <summary>保存キャンセルと破損入力で、既存企画を失わないことを守る。</summary>
public sealed class WorkspaceSessionStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "mock-session-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>種類がない旧セッションの企画・履歴・感想を保ったまま、新しい下書きを保存できる。</summary>
    [Fact]
    public async Task LegacySessionMigratesAndRetainsHistoryAndFeedback()
    {
        Directory.CreateDirectory(directory);
        using var store = new WorkspaceSessionStore(directory);
        var path = JsonSerializer.Serialize(directory);
        await File.WriteAllTextAsync(store.SessionPath, $$$"""
            {"Version":1,"Brief":{"Version":1,"Format":1,"Genres":["パズル"],"Values":{"future_rule":"旧指定"}},
            "Executable":"codex","OutputRoot":{{{path}}},"History":[{"StartedAt":"2026-09-12T00:00:00Z",
            "OutputDirectory":{{{path}}},"Format":0,"Outcome":1,"Feedback":"残す感想"}]}
            """, TestContext.Current.CancellationToken);
        var loaded = (await store.LoadAsync(CancellationToken.None))!;
        Assert.Equal(MockKind.Game, loaded.History[0].Kind);
        Assert.Equal("残す感想", loaded.History[0].Feedback);
        var service = new BriefDocument { Kind = MockKind.Service, Values = new() { ["service_title"] = "新しい道具" } };
        await store.SaveAsync(loaded with { Brief = service }, CancellationToken.None);
        var saved = (await store.LoadAsync(CancellationToken.None))!;
        Assert.Equal(WorkspaceSession.CurrentVersion, saved.Version);
        Assert.Equal("旧指定", saved.Drafts.Single(brief => brief.Kind == MockKind.Game).Values["future_rule"]);
        Assert.Equal(MockFormat.Unity, saved.Drafts.Single(brief => brief.Kind == MockKind.Game).Format);
        Assert.Equal("新しい道具", saved.Brief.Values["service_title"]);
        Assert.Equal(loaded.History, saved.History);
    }

    /// <summary>重複した種類の下書きを黙って上書きせず、原本の救出が可能なエラーにする。</summary>
    [Fact]
    public async Task DuplicateDraftsAreRejectedWithoutReplacingSavedSession()
    {
        using var store = new WorkspaceSessionStore(directory);
        await store.SaveAsync(CreateSession("保存済み"), CancellationToken.None);
        var duplicate = CreateSession("失う入力") with { Drafts = [new BriefDocument(), new BriefDocument()] };
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(duplicate, CancellationToken.None));
        Assert.Equal("保存済み", (await store.LoadAsync(CancellationToken.None))!.Brief.Values["world_theme"]);
    }

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
