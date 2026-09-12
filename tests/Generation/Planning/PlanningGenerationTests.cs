using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using GameMockStudio.Brief;
using GameMockStudio.Brief.Planning;
using GameMockStudio.Generation;
using GameMockStudio.Generation.Refinement;
using GameMockStudio.Storage;
using GameMockStudio.Tests.Generation.Fixtures;
using Xunit;

namespace GameMockStudio.Tests.Generation.Planning;

/// <summary>新しい種類も同じCLI・検査・改善経路を通り、偽の成功を返さないことを守る。</summary>
[UnsupportedOSPlatform("windows")]
public sealed class PlanningGenerationTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);
    private const string CompletedEvent = """printf '{"type":"turn.completed"}\n'""";

    /// <summary>種類に対応する全項目で新規生成を完了し、別フォルダで検証メモも更新して改善する。</summary>
    [Theory]
    [InlineData(MockKind.Service)]
    [InlineData(MockKind.Gamification)]
    public async Task NewKindsGenerateAndRefineWithoutChangingSource(MockKind kind)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("偽CLIはPOSIXシェルを使用します。");
            return;
        }
        using var fake = new FakeCodexProcess(CompletedEvent, kind: kind);
        using var timeout = new CancellationTokenSource(TestTimeout);
        var original = await fake.CreateRunner().Run(fake.Request).Do(fake.PrepareArtifacts).ToTask(timeout.Token);
        Assert.Equal(GenerationStage.Completed, original.Stage);
        using var refinement = new FakeCodexProcess("""
            cp baseline-resolved-brief.json resolved-brief.json
            cp baseline-README.md README.md
            cp baseline-decisions.md decisions.md
            cp baseline-generation-report.json generation-report.json
            printf '今回の比較手順' > experiment.md
            printf '<html>improved</html>' > index.html
            """ + "\n" + CompletedEvent, kind: kind);
        var request = refinement.Request with
        {
            Brief = new BriefDocument { Kind = kind },
            Refinement = new RefinementRequest { SourceDirectory = original.OutputDirectory, Feedback = "比較手順を改善" }
        };
        var improved = await refinement.CreateRunner().Run(request).ToTask(timeout.Token);
        Assert.Equal(GenerationStage.Completed, improved.Stage);
        Assert.Equal("今回の比較手順", await File.ReadAllTextAsync(Path.Combine(improved.OutputDirectory, "experiment.md"), TestContext.Current.CancellationToken));
        Assert.Equal("テスト用の検証メモ", await File.ReadAllTextAsync(Path.Combine(original.OutputDirectory, "experiment.md"), TestContext.Current.CancellationToken));
        Assert.Equal("テスト用の検証メモ", await File.ReadAllTextAsync(Path.Combine(improved.OutputDirectory, "baseline-experiment.md"), TestContext.Current.CancellationToken));
    }

    /// <summary>前回の検証メモを更新しない改善を完了扱いしない。</summary>
    [Fact]
    public async Task RefinementCannotReuseOnlyPreviousExperiment()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("偽CLIはPOSIXシェルを使用します。");
            return;
        }
        using var fake = new FakeCodexProcess("""
            cp baseline-resolved-brief.json resolved-brief.json
            cp baseline-README.md README.md
            cp baseline-decisions.md decisions.md
            cp baseline-generation-report.json generation-report.json
            """ + "\n" + CompletedEvent, kind: MockKind.Service);
        var source = Path.Combine(fake.Request.OutputRoot, "original");
        Directory.CreateDirectory(source);
        fake.PrepareArtifacts(new GenerationUpdate { Stage = GenerationStage.Prepared, OutputDirectory = source, Message = "" });
        var request = fake.Request with
        {
            Brief = new BriefDocument { Kind = MockKind.Service },
            Refinement = new RefinementRequest { SourceDirectory = source, Feedback = "手順を改善" }
        };
        using var timeout = new CancellationTokenSource(TestTimeout);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fake.CreateRunner().Run(request).ToTask(timeout.Token));
        Assert.Contains("experiment.md", exception.Message);
    }

    /// <summary>同じファイル形式でも別の種類にすり替わった補完企画を拒否する。</summary>
    [Fact]
    public async Task ArtifactCannotChangeKind()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("偽CLIはPOSIXシェルを使用します。");
            return;
        }
        using var fake = new FakeCodexProcess(CompletedEvent, kind: MockKind.Gamification);
        var source = Path.Combine(fake.Request.OutputRoot, "original");
        Directory.CreateDirectory(source);
        fake.PrepareArtifacts(new GenerationUpdate { Stage = GenerationStage.Prepared, OutputDirectory = source, Message = "" });
        var validator = new ArtifactValidator(new FieldCatalogs(), new BriefStore());
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => validator.ValidateAsync(source,
            new BriefDocument(), CancellationToken.None));
        Assert.Contains("種類", exception.Message);
    }
}
