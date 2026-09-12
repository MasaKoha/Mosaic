using System;
using System.IO;
using GameMockStudio.Brief;
using GameMockStudio.Generation.Refinement;

namespace GameMockStudio.Generation.History;

/// <summary>再起動後も成果物と補完企画へ戻れる生成記録。</summary>
public sealed record GenerationHistoryEntry
{
    /// <summary>生成を開始した時刻。</summary>
    public required DateTimeOffset StartedAt { get; init; }
    /// <summary>成果物フォルダの絶対パス。</summary>
    public required string OutputDirectory { get; init; }
    /// <summary>生成したゲームの形式。</summary>
    public required MockFormat Format { get; init; }
    /// <summary>最後に確認できた結果。</summary>
    public required GenerationOutcome Outcome { get; init; }

    /// <summary>このゲームを遊んだ感想の下書き。</summary>
    public string Feedback { get; init; } = string.Empty;
    /// <summary>改善元の成果物フォルダ。新規生成は空文字。</summary>
    public string SourceDirectory { get; init; } = string.Empty;
    /// <summary>この改善版の生成に使用した感想。下書きとは独立して保持する。</summary>
    public string AppliedFeedback { get; init; } = string.Empty;

    /// <summary>外部JSONから読み込んだパスと状態を検証する。</summary>
    public GenerationHistoryEntry Validate()
    {
        if (string.IsNullOrWhiteSpace(OutputDirectory) || !Path.IsPathFullyQualified(OutputDirectory)
            || !Enum.IsDefined(Format) || !Enum.IsDefined(Outcome)
            || Feedback is null || Feedback.Length > RefinementRequest.MaximumFeedbackCharacters
            || AppliedFeedback is null || AppliedFeedback.Length > RefinementRequest.MaximumFeedbackCharacters
            || SourceDirectory is null || (SourceDirectory.Length > 0 && !Path.IsPathFullyQualified(SourceDirectory)))
        {
            throw new InvalidOperationException("保存された生成履歴が不正です。");
        }
        return this;
    }
}
