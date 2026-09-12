using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using GameMockStudio.Brief;
using GameMockStudio.Brief.Planning;
using GameMockStudio.Generation;
using GameMockStudio.Generation.Completion;
using GameMockStudio.Storage;

namespace GameMockStudio.Tests.Generation.Fixtures;

/// <summary>外部通信なしで標準入出力と子プロセス停止を検証するCLIの代役。</summary>
[UnsupportedOSPlatform("windows")]
public sealed class FakeCodexProcess : IDisposable
{
    private const int DiagnosticCharacterCount = 100000;
    private readonly FieldCatalog catalog;
    private readonly BriefStore store = new();
    private readonly string root = Path.Combine(Path.GetTempPath(), "game-mock-runner-" + Guid.NewGuid().ToString("N"));
    private readonly GenerationOutcome outcome;

    /// <summary>指定の応答を返す実行ファイルをテスト専用ディレクトリへ用意する。</summary>
    public FakeCodexProcess(string behavior, GenerationOutcome outcome = GenerationOutcome.Completed,
        MockKind kind = MockKind.Game)
    {
        this.outcome = outcome;
        catalog = new FieldCatalog(kind);
        Directory.CreateDirectory(root);
        var executable = Path.Combine(root, "fake codex 日本語.sh");
        File.WriteAllText(executable, "#!/bin/sh\ncat > observed-prompt.txt\n" + behavior + "\n");
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Request = new GenerationRequest
        {
            Executable = executable,
            OutputRoot = root,
            Brief = new BriefDocument { Kind = kind, Genres = kind == MockKind.Service ? [] : ["パズル"] },
            Prompt = "日本語の企画\n引用符 \" ' と $(literal) を展開しない"
        };
    }

    /// <summary>実行時に標準入力へ渡す固定条件。</summary>
    public GenerationRequest Request { get; }
    /// <summary>パイプ容量を超える標準エラー出力。</summary>
    public string Diagnostics { get; } = new string('診', DiagnosticCharacterCount) + Environment.NewLine;

    /// <summary>実プロセスを起動する生成処理を組み立てる。</summary>
    public CodexRunner CreateRunner()
    {
        return new CodexRunner(new CodexCommand(), store, new ArtifactValidator(new FieldCatalogs(), store));
    }

    /// <summary>生成の準備通知でテスト対象の成果物を独立した生成先へ配置する。</summary>
    public void PrepareArtifacts(GenerationUpdate update)
    {
        if (update.Stage != GenerationStage.Prepared)
        {
            return;
        }
        var directory = update.OutputDirectory;
        var resolved = Request.Brief with { Values = catalog.Fields.ToDictionary(field => field.Identifier, _ => "なし") };
        File.WriteAllText(Path.Combine(directory, "resolved-brief.json"), JsonSerializer.Serialize(resolved));
        File.WriteAllText(Path.Combine(directory, "README.md"), "テスト用の説明");
        File.WriteAllText(Path.Combine(directory, "decisions.md"), "テスト用の判断理由");
        if (Request.Brief.Kind != MockKind.Game)
        {
            File.WriteAllText(Path.Combine(directory, "experiment.md"), "テスト用の検証メモ");
        }
        File.WriteAllText(Path.Combine(directory, "index.html"), "<!doctype html><html><body>fixture</body></html>");
        File.WriteAllText(Path.Combine(directory, "diagnostics-fixture.txt"), Diagnostics);
        var status = outcome == GenerationOutcome.Completed ? "completed" : "incomplete";
        File.WriteAllText(Path.Combine(directory, "generation-report.json"),
            $$"""{"Version":1,"Status":"{{status}}","Summary":"テスト用の実装結果","BlockingIssues":[]}""");
    }

    /// <summary>このテストが作成したファイルだけを破棄する。</summary>
    public void Dispose()
    {
        Directory.Delete(root, recursive: true);
    }
}
