using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GameMockStudio.Generation;

/// <summary>シェル展開を介さず、固定モデルでCodexを起動する。</summary>
public sealed class CodexCommand
{
    /// <summary>ローカルのモデル一覧で確認したGPT-6の識別子。</summary>
    public const string Model = "gpt-6-astra";
    /// <summary>指定された推論強度。</summary>
    public const string ReasoningEffort = "xhigh";

    /// <summary>標準的なインストール先を含めて実行ファイルを探す。</summary>
    public string FindExecutable()
    {
        var executableName = OperatingSystem.IsWindows() ? "codex.exe" : "codex";
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator);
        foreach (var directory in directories)
        {
            var candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        string[] macLocations = ["/opt/homebrew/bin/codex", "/usr/local/bin/codex"];
        foreach (var candidate in macLocations)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return executableName;
    }

    /// <summary>企画本文を引数へ埋め込まないプロセス設定を返す。</summary>
    public ProcessStartInfo CreateStartInfo(string executable, string outputDirectory)
    {
        var information = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = outputDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        string[] arguments =
        [
            "exec", "--ignore-user-config", "--model", Model,
            "--config", $"model_reasoning_effort=\"{ReasoningEffort}\"",
            "--config", "model_provider=\"openai\"",
            "--config", "approval_policy=\"never\"",
            "--config", "sandbox_workspace_write.network_access=false",
            "--config", "shell_environment_policy.inherit=\"core\"",
            "--config", "shell_environment_policy.ignore_default_excludes=false",
            "--sandbox", "workspace-write",
            "--skip-git-repo-check", "--json", "--color", "never",
            "--output-last-message", Path.Combine(outputDirectory, "result.md"),
            "--cd", outputDirectory, "-"
        ];
        foreach (var argument in arguments)
        {
            information.ArgumentList.Add(argument);
        }
        return information;
    }
}
