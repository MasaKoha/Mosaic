namespace GameMockStudio.Generation.Completion;

/// <summary>生成されたモックが企画の実装を完了できたかを表す。</summary>
public enum GenerationOutcome
{
    /// <summary>指定の矛盾や実装不足により生成を完了できなかった状態。</summary>
    Incomplete,
    /// <summary>モックの実装を完了し、残る阻害事項がない状態。</summary>
    Completed
}
