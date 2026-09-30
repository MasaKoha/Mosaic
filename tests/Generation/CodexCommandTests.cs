using System;
using System.IO;
using GameMockStudio.Generation;
using Xunit;

namespace GameMockStudio.Tests.Generation;

/// <summary>企画やパスがシェルの追加コマンドにならないことを検証する。</summary>
public sealed class CodexCommandTests
{
    /// <summary>空白やメタ文字を含むパスも一つの引数として固定モデルへ渡す。</summary>
    [Fact]
    public void ArgumentsKeepPathsLiteralAndModelFixed()
    {
        var directory = Path.Combine(Path.GetTempPath(), "game mock $(literal) 日本語");
        var command = new CodexCommand();
        var information = command.CreateStartInfo("codex", directory);
        Assert.False(information.UseShellExecute);
        Assert.True(information.RedirectStandardInput);
        Assert.Contains(directory, information.ArgumentList);
        Assert.Contains("gpt-6-astra", information.ArgumentList);
        Assert.Contains("model_reasoning_effort=\"xhigh\"", information.ArgumentList);
        Assert.Contains("--ignore-user-config", information.ArgumentList);
        Assert.Contains("model_provider=\"openai\"", information.ArgumentList);
        Assert.Contains("approval_policy=\"never\"", information.ArgumentList);
        Assert.Contains("sandbox_workspace_write.network_access=false", information.ArgumentList);
        Assert.Contains("shell_environment_policy.inherit=\"core\"", information.ArgumentList);
        Assert.Contains("shell_environment_policy.ignore_default_excludes=false", information.ArgumentList);
        Assert.Contains("workspace-write", information.ArgumentList);
        Assert.DoesNotContain("--dangerously-bypass-approvals-and-sandbox", information.ArgumentList);
        Assert.Equal("-", information.ArgumentList[^1]);
    }
}
