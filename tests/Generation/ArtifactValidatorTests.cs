using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Brief;
using GameMockStudio.Generation;
using GameMockStudio.Storage;
using Xunit;

namespace GameMockStudio.Tests.Generation;

/// <summary>終了コード0でも不足や指定違反があれば失敗にすることを検証する。</summary>
public sealed class ArtifactValidatorTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "game-mock-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FieldCatalog catalog = new();
    private readonly BriefStore store = new();
    private readonly BriefDocument requested = new()
    {
        Genres = ["パズル"],
        Values = new() { ["combat_style"] = "なし" }
    };

    /// <summary>各テストに独立した成果物の仮置き場を用意する。</summary>
    public ArtifactValidatorTests()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "index.html"), "<!doctype html><html><body>fixture</body></html>");
        File.WriteAllText(Path.Combine(directory, "README.md"), "テスト用の説明");
        File.WriteAllText(Path.Combine(directory, "decisions.md"), "テスト用の決定理由");
        File.WriteAllText(Path.Combine(directory, "generation-report.json"),
            """{"Version":1,"Status":"completed","Summary":"テスト用のモックを実装","BlockingIssues":[]}""");
    }

    /// <summary>全補完項目と明示指定が揃えばファイル検査を通す。</summary>
    [Fact]
    public async Task CompleteArtifactSetPassesFileValidation()
    {
        await WriteResolvedAsync(CreateResolved());
        await new ArtifactValidator(catalog, store).ValidateAsync(directory, requested, CancellationToken.None);
    }

    /// <summary>明示された不採用をAIが書き換えた場合は拒否する。</summary>
    [Fact]
    public async Task ExplicitExclusionCannotBeOverridden()
    {
        var resolved = CreateResolved();
        resolved.Values["combat_style"] = "リアルタイム戦闘";
        await WriteResolvedAsync(resolved);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ArtifactValidator(catalog, store).ValidateAsync(directory, requested, CancellationToken.None));
    }

    /// <summary>未入力項目の補完が欠けた場合は未完了として拒否する。</summary>
    [Fact]
    public async Task MissingRandomDecisionIsRejected()
    {
        var resolved = CreateResolved();
        resolved.Values.Remove("world_theme");
        await WriteResolvedAsync(resolved);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ArtifactValidator(catalog, store).ValidateAsync(directory, requested, CancellationToken.None));
    }

    /// <summary>AIが指定ジャンルを追加した場合は拒否する。</summary>
    [Fact]
    public async Task AdditionalGenreIsRejected()
    {
        await WriteResolvedAsync(CreateResolved() with { Genres = ["パズル", "アクション"] });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ArtifactValidator(catalog, store).ValidateAsync(directory, requested, CancellationToken.None));
    }

    /// <summary>CLIが正常終了しても矛盾や実装不足を報告した成果物は成功にしない。</summary>
    [Theory]
    [InlineData("incomplete", "[]")]
    [InlineData("completed", "[\"明示指定が矛盾しています\"]")]
    public async Task IncompleteImplementationIsRejected(string status, string blockingIssues)
    {
        await WriteResolvedAsync(CreateResolved());
        await File.WriteAllTextAsync(Path.Combine(directory, "generation-report.json"),
            $$"""{"Version":1,"Status":"{{status}}","Summary":"操作指定が矛盾しています","BlockingIssues":{{blockingIssues}}}""", TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ArtifactValidator(catalog, store).ValidateAsync(directory, requested, CancellationToken.None));

        Assert.Contains("未完了", exception.Message);
    }

    /// <summary>不正な完了レポートを暗黙の既定値で成功扱いしない。</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Version\":1,\"Summary\":\"実装済み\",\"BlockingIssues\":[]}")]
    [InlineData("{\"Version\":1,\"Status\":1,\"Summary\":\"実装済み\",\"BlockingIssues\":[]}")]
    [InlineData("{\"Version\":1,\"Status\":\"completed\",\"Summary\":null,\"BlockingIssues\":[]}")]
    [InlineData("{\"Version\":1,\"Status\":\"completed\",\"Summary\":\"実装済み\",\"BlockingIssues\":null}")]
    public async Task MalformedCompletionReportIsRejected(string report)
    {
        await WriteResolvedAsync(CreateResolved());
        await File.WriteAllTextAsync(Path.Combine(directory, "generation-report.json"), report, TestContext.Current.CancellationToken);

        var exception = await Record.ExceptionAsync(() =>
            new ArtifactValidator(catalog, store).ValidateAsync(directory, requested, CancellationToken.None));

        Assert.True(exception is InvalidOperationException or JsonException);
    }

    /// <summary>実装本体や完了レポートが欠けた生成を成功として通知しない。</summary>
    [Theory]
    [InlineData("index.html")]
    [InlineData("generation-report.json")]
    public async Task RequiredArtifactCannotBeMissing(string relativePath)
    {
        await WriteResolvedAsync(CreateResolved());
        File.Delete(Path.Combine(directory, relativePath));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ArtifactValidator(catalog, store).ValidateAsync(directory, requested, CancellationToken.None));

        Assert.Contains(relativePath, exception.Message);
    }

    /// <summary>中身が空白のみのファイルを実装済みの成果物と誤認しない。</summary>
    [Theory]
    [InlineData("index.html")]
    [InlineData("README.md")]
    [InlineData("decisions.md")]
    public async Task WhitespaceArtifactIsRejected(string relativePath)
    {
        await WriteResolvedAsync(CreateResolved());
        await File.WriteAllTextAsync(Path.Combine(directory, relativePath), " \r\n\t", TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ArtifactValidator(catalog, store).ValidateAsync(directory, requested, CancellationToken.None));

        Assert.Contains(relativePath, exception.Message);
    }

    /// <summary>名前だけ用意した空のC#ソースでは実装済みと判定しない。</summary>
    [Theory]
    [InlineData(MockFormat.Unity)]
    [InlineData(MockFormat.Avalonia)]
    public async Task EmptyImplementationSourceIsRejected(MockFormat format)
    {
        PrepareProjectFiles(format);
        await WriteResolvedAsync(CreateResolved() with { Format = format });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ArtifactValidator(catalog, store).ValidateAsync(directory, requested with { Format = format }, CancellationToken.None));

        Assert.Contains("実装ソース", exception.Message);
    }

    /// <summary>拡張項目にも入力された明示指定の維持を要求する。</summary>
    [Fact]
    public async Task UnrecognizedSpecifiedFieldCannotBeDiscarded()
    {
        var extendedRequest = requested with { Values = new(requested.Values) { ["future_rule"] = "一度だけ巻き戻せる" } };
        await WriteResolvedAsync(CreateResolved());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ArtifactValidator(catalog, store).ValidateAsync(directory, extendedRequest, CancellationToken.None));

        Assert.Contains("future_rule", exception.Message);
    }

    /// <summary>改善では仕様の変更を許すが、未知の企画項目の消失は検出する。</summary>
    [Fact]
    public async Task RefinementCannotDiscardUnknownBaselineFields()
    {
        var baseline = CreateResolved();
        baseline.Values["future_rule"] = "残すルール";
        await store.SaveAsync(Path.Combine(directory, "baseline-resolved-brief.json"), CreateResolved(), CancellationToken.None);
        await WriteResolvedAsync(CreateResolved());
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ArtifactValidator(catalog, store).ValidateRefinementAsync(directory, baseline, CancellationToken.None));
        Assert.Contains("future_rule", exception.Message);
    }

    /// <summary>テストで作成した専用ディレクトリだけを破棄する。</summary>
    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }

    private BriefDocument CreateResolved()
    {
        return new BriefDocument
        {
            Genres = requested.Genres,
            Values = catalog.Fields.ToDictionary(field => field.Identifier, _ => "なし")
        };
    }

    private Task WriteResolvedAsync(BriefDocument document)
    {
        return store.SaveAsync(Path.Combine(directory, "resolved-brief.json"), document, CancellationToken.None);
    }

    private void PrepareProjectFiles(MockFormat format)
    {
        if (format == MockFormat.Unity)
        {
            Directory.CreateDirectory(Path.Combine(directory, "Assets"));
            Directory.CreateDirectory(Path.Combine(directory, "Packages"));
            Directory.CreateDirectory(Path.Combine(directory, "ProjectSettings"));
            File.WriteAllText(Path.Combine(directory, "Assets/Empty.cs"), " \n");
            File.WriteAllText(Path.Combine(directory, "Packages/manifest.json"), "{}");
            File.WriteAllText(Path.Combine(directory, "ProjectSettings/ProjectVersion.txt"), "m_EditorVersion: 6000.0.0f1");
            return;
        }
        Directory.CreateDirectory(Path.Combine(directory, "GameMock"));
        File.WriteAllText(Path.Combine(directory, "GameMock/GameMock.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(directory, "GameMock/Empty.cs"), " \n");
    }
}
