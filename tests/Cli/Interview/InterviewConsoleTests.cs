using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Cli;
using GameMockStudio.Storage;
using Xunit;

namespace GameMockStudio.Tests.Cli.Interview;

/// <summary>人間向け対話の空行と中断で、直前までの回答を失わないことを守る。</summary>
public sealed class InterviewConsoleTests
{
    /// <summary>StringReaderの値・空行・不採用を保存し、:qとEOFのどちらでも再開可能にする。</summary>
    [Theory]
    [InlineData(":q\n")]
    [InlineData("")]
    public async Task InterruptionKeepsAllCompletedAnswers(string ending)
    {
        using var workspace = new CliTestWorkspace();
        using var started = await workspace.RunJsonAsync("interview", "start", "--brief", workspace.BriefPath, "--kind", "service");
        var result = await workspace.RunAsync(["interview", "ask", "--brief", workspace.BriefPath], "値\n\nなし\n" + ending);
        Assert.Equal(CliExitCode.Failure, result.ExitCode);
        Assert.Contains("中断", result.Error);
        Assert.Contains("【サービスの仮説】", result.Output);
        Assert.Contains("仮の名前で構いません。", result.Output);
        Assert.Contains("記入例: おすそわけノート", result.Output);
        Assert.Contains("回答済み 3/60", result.Output);
        var saved = await new BriefStore().LoadAsync(workspace.BriefPath, CancellationToken.None);
        Assert.Equal("値", saved.Values["service_title"]);
        Assert.Equal("", saved.Values["service_type"]);
        Assert.Equal("なし", saved.Values["service_pitch"]);
        Assert.Equal(3, saved.Values.Count);
        Assert.False(saved.Values.ContainsKey("service_problem"));
        using var next = await workspace.RunJsonAsync("interview", "next", "--brief", workspace.BriefPath);
        Assert.Equal("service_problem", next.RootElement.GetProperty("field").GetProperty("identifier").GetString());
    }
}
