namespace GameMockStudio.Generation;

/// <summary>生成処理の観測可能な進行段階。</summary>
public enum GenerationStage
{
    /// <summary>生成先が確定した状態。</summary>
    Prepared,
    /// <summary>Codexが処理を進めている状態。</summary>
    Running,
    /// <summary>必須ファイルと補完企画の確認が完了した状態。</summary>
    Completed
}
