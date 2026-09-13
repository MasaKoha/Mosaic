using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Brief;
using GameMockStudio.Brief.Planning;
using GameMockStudio.Cli;
using GameMockStudio.Storage;
using Xunit;

namespace GameMockStudio.Tests.Cli.Interview;

/// <summary>別々のCLI呼び出しで回答とJSON進捗が一貫して保存されることを守る。</summary>
public sealed class InterviewCommandsTests
{
    /// <summary>値、AI補完、不採用を順に回答し、次の質問と集計に反映する。</summary>
    [Fact]
    public async Task StatelessInterviewPersistsEachAnswerAndReportsProgress()
    {
        using var workspace = new CliTestWorkspace();
        using var started = await workspace.RunJsonAsync("interview", "start", "--brief", workspace.BriefPath, "--kind", "SERVICE", "--format", "BROWSER");
        Assert.False(started.RootElement.GetProperty("done").GetBoolean());
        Assert.Equal("service", started.RootElement.GetProperty("kind").GetString());
        Assert.Equal("サービスの仮説", started.RootElement.GetProperty("category").GetProperty("label").GetString());
        Assert.Equal("service_title", started.RootElement.GetProperty("field").GetProperty("identifier").GetString());
        Assert.Equal(0, started.RootElement.GetProperty("progress").GetProperty("answered").GetInt32());
        Assert.Contains("おすそわけノート", started.RootElement.GetRawText());

        using var named = await workspace.RunJsonAsync("interview", "answer", "--brief", workspace.BriefPath, "--field", "service_title", "--value", "検証ノート");
        Assert.Equal("service_type", named.RootElement.GetProperty("field").GetProperty("identifier").GetString());
        using var random = await workspace.RunJsonAsync("interview", "answer", "--brief", workspace.BriefPath, "--field", "service_type", "--random");
        Assert.Equal(2, random.RootElement.GetProperty("progress").GetProperty("answered").GetInt32());
        Assert.Equal(1, random.RootElement.GetProperty("progress").GetProperty("specified").GetInt32());
        using var none = await workspace.RunJsonAsync("interview", "answer", "--brief", workspace.BriefPath, "--field", "service_pitch", "--none");
        using var next = await workspace.RunJsonAsync("interview", "next", "--brief", workspace.BriefPath);
        Assert.Equal(none.RootElement.GetRawText(), next.RootElement.GetRawText());
        Assert.Equal("service_problem", next.RootElement.GetProperty("field").GetProperty("identifier").GetString());

        using var status = await workspace.RunJsonAsync("interview", "status", "--brief", workspace.BriefPath);
        var progress = status.RootElement.GetProperty("progress");
        Assert.Equal(3, progress.GetProperty("answered").GetInt32());
        Assert.Equal(2, progress.GetProperty("specified").GetInt32());
        Assert.Equal(60, progress.GetProperty("total").GetInt32());
        Assert.Equal("browser", status.RootElement.GetProperty("format").GetString());
        Assert.Equal(0, status.RootElement.GetProperty("genres").GetArrayLength());
        Assert.Equal(3, status.RootElement.GetProperty("categories")[0].GetProperty("answered").GetInt32());
        Assert.Equal(2, status.RootElement.GetProperty("categories")[0].GetProperty("specified").GetInt32());
        var saved = await new BriefStore().LoadAsync(workspace.BriefPath, CancellationToken.None);
        Assert.Equal("検証ノート", saved.Values["service_title"]);
        Assert.Equal("", saved.Values["service_type"]);
        Assert.Equal("なし", saved.Values["service_pitch"]);
    }

    /// <summary>既存ファイルへのstartは失敗し、企画の内容を変更しない。</summary>
    [Fact]
    public async Task StartDoesNotOverwriteExistingBrief()
    {
        using var workspace = new CliTestWorkspace();
        using var started = await workspace.RunJsonAsync("interview", "start", "--brief", workspace.BriefPath, "--kind", "game");
        var original = await File.ReadAllBytesAsync(workspace.BriefPath, TestContext.Current.CancellationToken);
        var result = await workspace.RunAsync(["interview", "start", "--brief", workspace.BriefPath, "--kind", "service"]);
        Assert.Equal(CliExitCode.Failure, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("既に存在", result.Error);
        Assert.Equal(original, await File.ReadAllBytesAsync(workspace.BriefPath, TestContext.Current.CancellationToken));
        Assert.Single(Directory.GetFiles(workspace.DirectoryPath));
    }

    /// <summary>未知IDが含まれる一括回答は、妥当な項目も含めて保存しない。</summary>
    [Fact]
    public async Task BatchRejectsUnknownIdentifiersWithoutChangingFile()
    {
        using var workspace = new CliTestWorkspace();
        var store = new BriefStore();
        await store.SaveAsync(workspace.BriefPath, new BriefDocument
        {
            Kind = MockKind.Service,
            Values = new() { ["service_title"] = "保存済み", ["future_rule"] = "既存の未知キー" }
        }, CancellationToken.None);
        var original = await File.ReadAllBytesAsync(workspace.BriefPath, TestContext.Current.CancellationToken);
        var answersPath = Path.Combine(workspace.DirectoryPath, "answers.json");
        await File.WriteAllTextAsync(answersPath, """{"service_title":"更新","future_rule":"拒否"}""", TestContext.Current.CancellationToken);
        var result = await workspace.RunAsync(["interview", "answer", "--brief", workspace.BriefPath, "--from-json", answersPath]);
        Assert.Equal(CliExitCode.Failure, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("future_rule", result.Error);
        Assert.Equal(original, await File.ReadAllBytesAsync(workspace.BriefPath, TestContext.Current.CancellationToken));

        await File.WriteAllTextAsync(answersPath, """{"service_title":"更新","service_type":""}""", TestContext.Current.CancellationToken);
        using var accepted = await workspace.RunJsonAsync("interview", "answer", "--brief", workspace.BriefPath, "--from-json", answersPath);
        Assert.Equal("service_pitch", accepted.RootElement.GetProperty("field").GetProperty("identifier").GetString());
        var saved = await store.LoadAsync(workspace.BriefPath, CancellationToken.None);
        Assert.Equal("更新", saved.Values["service_title"]);
        Assert.Equal("", saved.Values["service_type"]);
        Assert.Equal("既存の未知キー", saved.Values["future_rule"]);
    }
}
