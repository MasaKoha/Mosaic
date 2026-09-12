using System;

namespace GameMockStudio.Generation.Completion;

/// <summary>CLIの正常終了とゲームの実装完了を区別する生成結果。</summary>
public sealed record GenerationReport
{
    /// <summary>生成結果の現行形式。</summary>
    public const int CurrentVersion = 1;
    /// <summary>生成先に保存する機械可読な結果ファイル。</summary>
    public const string FileName = "generation-report.json";
    /// <summary>生成結果の形式バージョン。</summary>
    public required int Version { get; init; }
    /// <summary>企画に対する実装の完了状態。</summary>
    public required GenerationOutcome Status { get; init; }
    /// <summary>完了した範囲、または完了できなかった理由。</summary>
    public required string Summary { get; init; }
    /// <summary>完了を妨げている指定の矛盾や実装不足。</summary>
    public required string[] BlockingIssues { get; init; }

    /// <summary>不正な結果と未完了の実装を生成成功として扱わない。</summary>
    public void EnsureCompleted()
    {
        if (Version != CurrentVersion || !Enum.IsDefined(Status)
            || string.IsNullOrWhiteSpace(Summary) || BlockingIssues is null)
        {
            throw new InvalidOperationException("生成結果レポートの形式が不正です。");
        }
        if (Status != GenerationOutcome.Completed || BlockingIssues.Length > 0)
        {
            var details = string.Join(Environment.NewLine, BlockingIssues);
            throw new InvalidOperationException($"モックの実装は未完了です: {Summary}\n{details}");
        }
    }
}
