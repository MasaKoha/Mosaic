namespace GameMockStudio.Generation;

/// <summary>生成処理から画面へ通知する進捗。</summary>
public sealed record GenerationUpdate
{
    /// <summary>現在の段階。</summary>
    public required GenerationStage Stage { get; init; }
    /// <summary>利用者向けの進行メッセージ。</summary>
    public required string Message { get; init; }
    /// <summary>今回の成果物フォルダ。</summary>
    public required string OutputDirectory { get; init; }
}
