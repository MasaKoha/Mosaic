using System.IO;
using System.Threading.Tasks;
using GameMockStudio.Cli;
using Xunit;

namespace GameMockStudio.Tests.Cli;

/// <summary>指定誤りを実行失敗と区別し、ファイル操作より前に拒否する。</summary>
public sealed class CliArgumentsTests
{
    /// <summary>未対応の引数、必須値、型変換、排他条件の失敗例。</summary>
    public static TheoryData<string[]> InvalidArguments => new()
    {
        new[] { "unknown" },
        new[] { "interview" },
        new[] { "interview", "unknown" },
        new[] { "kinds", "--unknown" },
        new[] { "kinds", "--unknown", "--help" },
        new[] { "fields" },
        new[] { "fields", "--kind" },
        new[] { "fields", "--kind", "" },
        new[] { "fields", "--kind", "0" },
        new[] { "fields", "--kind", "other" },
        new[] { "fields", "--kind", "game", "--kind", "service" },
        new[] { "kinds", "unexpected" },
        new[] { "interview", "start", "--brief", "unused.json", "--kind", "game", "--format", "html" },
        new[] { "interview", "start", "--brief", "unused.json", "--kind", "game", "--idea", "1.5" },
        new[] { "interview", "start", "--brief", "unused.json", "--kind", "game", "--idea", "0" },
        new[] { "interview", "answer", "--brief", "unused.json", "--field", "title" },
        new[] { "interview", "answer", "--brief", "unused.json", "--random" },
        new[] { "interview", "answer", "--brief", "unused.json", "--field", "title", "--value", "--random" },
        new[] { "interview", "answer", "--brief", "unused.json", "--field", "title", "--value", "名前", "--none" },
        new[] { "interview", "answer", "--brief", "unused.json", "--field", "title", "--random", "--none" },
        new[] { "interview", "answer", "--brief", "unused.json", "--from-json", "answers.json", "--field", "title" },
        new[] { "interview", "answer", "--brief", "unused.json", "--from-json", "answers.json", "--random" },
        new[] { "interview", "genres", "--brief", "unused.json" },
        new[] { "generate", "--brief", "unused.json" },
        new[] { "generate", "--brief", "unused.json", "--output", "relative" },
        new[] { "refine", "--mock", "relative", "--feedback-file", "feedback.md", "--output", "relative" }
    };

    /// <summary>指定誤りではstdoutへ結果を混ぜず終了コード2を返す。</summary>
    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public async Task InvalidOptionsReturnUsageErrorBeforeReadingFiles(string[] arguments)
    {
        using var workspace = new CliTestWorkspace();
        var result = await workspace.RunAsync(arguments);
        Assert.Equal(CliExitCode.UsageError, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("引数の誤り", result.Error);
        Assert.Empty(Directory.GetFiles(workspace.DirectoryPath));
    }
}
