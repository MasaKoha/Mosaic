using System;
using System.IO;
using GameMockStudio.Brief;

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

    /// <summary>外部JSONから読み込んだパスと状態を検証する。</summary>
    public GenerationHistoryEntry Validate()
    {
        if (string.IsNullOrWhiteSpace(OutputDirectory) || !Path.IsPathFullyQualified(OutputDirectory)
            || !Enum.IsDefined(Format) || !Enum.IsDefined(Outcome))
        {
            throw new InvalidOperationException("保存された生成履歴が不正です。");
        }
        return this;
    }
}
