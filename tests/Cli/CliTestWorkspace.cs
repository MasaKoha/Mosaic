using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using GameMockStudio.Cli;
using Xunit;

namespace GameMockStudio.Tests.Cli;

/// <summary>CLIの保存先と入出力を実際のユーザー環境から隔離する。</summary>
internal sealed class CliTestWorkspace : IDisposable
{
    /// <summary>このテストだけが使う一時ディレクトリ。</summary>
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "mosaic-cli-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>対話の保存先。</summary>
    public string BriefPath => Path.Combine(DirectoryPath, "brief.json");

    /// <summary>テスト用の保存先を作成する。</summary>
    public CliTestWorkspace()
    {
        Directory.CreateDirectory(DirectoryPath);
    }

    /// <summary>呼び出しごとにCLIを作り直し、状態をファイルだけで引き継ぐ。</summary>
    public async Task<(CliExitCode ExitCode, string Output, string Error)> RunAsync(string[] arguments, string inputText = "")
    {
        using var input = new StringReader(inputText);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await new CliApplication(input, output, error).RunAsync(arguments);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>成功を確認したJSONを返す。呼び出し側で破棄する。</summary>
    public async Task<JsonDocument> RunJsonAsync(params string[] arguments)
    {
        var result = await RunAsync(arguments);
        Assert.Equal(CliExitCode.Success, result.ExitCode);
        Assert.Empty(result.Error);
        return JsonDocument.Parse(result.Output);
    }

    /// <summary>作成したファイルをテスト終了時に破棄する。</summary>
    public void Dispose()
    {
        Directory.Delete(DirectoryPath, recursive: true);
    }
}
