using GameMockStudio.Brief;
using GameMockStudio.Generation.History;

namespace GameMockStudio.Storage;

/// <summary>次回起動へ引き継ぐ企画・ローカル設定・生成履歴。</summary>
public sealed record WorkspaceSession
{
    /// <summary>対応している保存形式。</summary>
    public const int CurrentVersion = 1;
    /// <summary>保存形式の互換性バージョン。</summary>
    public int Version { get; init; } = CurrentVersion;
    /// <summary>編集中の企画。</summary>
    public required BriefDocument Brief { get; init; }
    /// <summary>選択されたCLI実行ファイル。</summary>
    public required string Executable { get; init; }
    /// <summary>選択された生成先。</summary>
    public required string OutputRoot { get; init; }
    /// <summary>最近の生成記録。</summary>
    public required GenerationHistoryEntry[] History { get; init; }
}
