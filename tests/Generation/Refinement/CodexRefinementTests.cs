using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Brief;
using GameMockStudio.Generation;
using GameMockStudio.Generation.Refinement;
using GameMockStudio.Storage;
using GameMockStudio.Tests.Generation.Fixtures;
using Xunit;

namespace GameMockStudio.Tests.Generation.Refinement;

/// <summary>改善が元のゲームと秘密情報を保護し、新しい成果物を検査することを確認する。</summary>
[UnsupportedOSPlatform("windows")]
public sealed class CodexRefinementTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);
    private const string CompletedEvent = """printf '{"type":"turn.completed"}\n'""";
    private const string RestoreDocuments = """
        cp baseline-README.md README.md
        cp baseline-decisions.md decisions.md
        cp baseline-resolved-brief.json resolved-brief.json
        """;

    /// <summary>前回の指定を変更でき、元のファイルと未知項目を残して繰り返し改善できる。</summary>
    [UnixFact]
    public async Task RefinementCopiesGameAndAllowsRequestedChangesWithoutOverwritingSource()
    {
        using var fake = new FakeCodexProcess(RestoreDocuments + "\n" + """
            cp expected-brief.json resolved-brief.json
            cp baseline-generation-report.json generation-report.json
            printf '<html>improved game</html>' > index.html
            """ + "\n" + CompletedEvent);
        var source = PrepareSource(fake);
        var store = new BriefStore();
        var baseline = await store.LoadAsync(Path.Combine(source, "resolved-brief.json"), CancellationToken.None);
        baseline.Values["future_rule"] = "残すルール";
        await store.SaveAsync(Path.Combine(source, "resolved-brief.json"), baseline, CancellationToken.None);
        var improved = baseline with { Genres = ["アクション"], Values = new(baseline.Values) { ["title"] = "改善したゲーム" } };
        await store.SaveAsync(Path.Combine(source, "expected-brief.json"), improved, CancellationToken.None);
        var original = await File.ReadAllTextAsync(Path.Combine(source, "index.html"));
        var launcher = Path.Combine(source, "launch.sh");
        File.WriteAllText(launcher, "#!/bin/sh\nexit 0\n");
        File.SetUnixFileMode(launcher, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.WriteAllText(Path.Combine(source, ".env"), "private test fixture");
        Directory.CreateDirectory(Path.Combine(source, ".codex"));
        File.WriteAllText(Path.Combine(source, ".codex/config.toml"), "private configuration fixture");
        File.WriteAllText(Path.Combine(source, "auth.json"), "private fixture");
        Directory.CreateDirectory(Path.Combine(source, "node_modules"));
        File.WriteAllText(Path.Combine(source, "node_modules/cache.txt"), "cache");
        using var timeout = new CancellationTokenSource(TestTimeout);

        var update = await fake.CreateRunner().Run(CreateRequest(fake, source)).ToTask(timeout.Token);
        var refined = await store.LoadAsync(Path.Combine(update.OutputDirectory, "resolved-brief.json"), CancellationToken.None);
        Assert.Equal(GenerationStage.Completed, update.Stage);
        Assert.Equal("改善したゲーム", refined.Values["title"]);
        Assert.Equal("残すルール", refined.Values["future_rule"]);
        Assert.Equal(original, await File.ReadAllTextAsync(Path.Combine(source, "index.html")));
        Assert.Equal("<html>improved game</html>", await File.ReadAllTextAsync(Path.Combine(update.OutputDirectory, "index.html")));
        Assert.False(File.Exists(Path.Combine(update.OutputDirectory, ".env")));
        Assert.False(File.Exists(Path.Combine(update.OutputDirectory, "auth.json")));
        Assert.False(Directory.Exists(Path.Combine(update.OutputDirectory, ".codex")));
        Assert.False(Directory.Exists(Path.Combine(update.OutputDirectory, "node_modules")));
        Assert.True(File.GetUnixFileMode(Path.Combine(update.OutputDirectory, "launch.sh")).HasFlag(UnixFileMode.UserExecute));
        var next = await fake.CreateRunner().Run(CreateRequest(fake, update.OutputDirectory)).ToTask(timeout.Token);
        Assert.Equal(GenerationStage.Completed, next.Stage);
        Assert.NotEqual(update.OutputDirectory, next.OutputDirectory);
    }

    /// <summary>古い完了レポートしかない改善を、新しく完了したものとして扱わない。</summary>
    [UnixFact]
    public async Task PreviousCompletionReportCannotCompleteRefinement()
    {
        using var fake = new FakeCodexProcess(RestoreDocuments + "\n" + CompletedEvent);
        var source = PrepareSource(fake);
        using var timeout = new CancellationTokenSource(TestTimeout);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fake.CreateRunner().Run(CreateRequest(fake, source)).ToTask(timeout.Token));
        Assert.Contains("generation-report.json", exception.Message);
        Assert.True(File.Exists(Path.Combine(source, "generation-report.json")));
    }

    /// <summary>コピー先の再帰混入、外部ファイルのリンク、過大なファイルをCLI起動前に拒否する。</summary>
    [Theory]
    [InlineData("nested")]
    [InlineData("alias")]
    [InlineData("link")]
    [InlineData("oversize")]
    public async Task UnsafeSnapshotIsRejectedBeforeStartingCli(string scenario)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("偽CLIとリンクの再現はPOSIX環境を使用します。");
            return;
        }
        using var fake = new FakeCodexProcess(CompletedEvent);
        var source = PrepareSource(fake);
        var request = CreateRequest(fake, source);
        if (scenario == "nested")
        {
            request = request with { OutputRoot = source };
        }
        if (scenario == "alias")
        {
            var alias = Path.Combine(fake.Request.OutputRoot, "source-alias");
            Directory.CreateSymbolicLink(alias, source);
            request = request with { OutputRoot = alias };
        }
        if (scenario == "link")
        {
            File.CreateSymbolicLink(Path.Combine(source, "linked-game.html"), fake.Request.Executable);
        }
        if (scenario == "oversize")
        {
            await using var oversized = File.Create(Path.Combine(source, "oversized.asset"));
            oversized.SetLength(257L * 1024 * 1024);
        }
        using var timeout = new CancellationTokenSource(TestTimeout);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fake.CreateRunner().Run(request).ToTask(timeout.Token));
        Assert.Empty(Directory.GetFiles(fake.Request.OutputRoot, "observed-prompt.txt", SearchOption.AllDirectories));
        Assert.True(File.Exists(Path.Combine(source, "index.html")));
    }

    /// <summary>コピーの取消が元のゲームを失わせず、CLIを起動しない。</summary>
    [UnixFact]
    public async Task CancelledCopyPreservesSource()
    {
        using var fake = new FakeCodexProcess(CompletedEvent);
        var source = PrepareSource(fake);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GameSnapshot().CopyAsync(
            source, Path.Combine(fake.Request.OutputRoot, "cancelled-copy"), cancellation.Token));
        Assert.True(File.Exists(Path.Combine(source, "index.html")));
    }

    private string PrepareSource(FakeCodexProcess fake)
    {
        var source = Path.Combine(fake.Request.OutputRoot, "original-game");
        Directory.CreateDirectory(source);
        fake.PrepareArtifacts(new GenerationUpdate { Stage = GenerationStage.Prepared, OutputDirectory = source, Message = "" });
        return source;
    }

    private GenerationRequest CreateRequest(FakeCodexProcess fake, string source)
    {
        return fake.Request with
        {
            Brief = new BriefDocument(),
            Refinement = new RefinementRequest { SourceDirectory = source, Feedback = "操作を気持ちよく。タイトルも改善する。" }
        };
    }
}
