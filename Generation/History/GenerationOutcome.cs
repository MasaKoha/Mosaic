namespace GameMockStudio.Generation.History;

/// <summary>履歴に残す生成結果。</summary>
public enum GenerationOutcome
{
    /// <summary>生成中。</summary>
    Running,
    /// <summary>成果物検査まで完了した。</summary>
    Completed,
    /// <summary>実行または検査に失敗した。</summary>
    Failed,
    /// <summary>利用者が取り消した。</summary>
    Cancelled,
    /// <summary>完了の記録前にアプリが終了した。</summary>
    Interrupted
}
