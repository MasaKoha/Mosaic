namespace GameMockStudio.Brief;

/// <summary>生成するモックの実装環境。</summary>
public enum MockFormat
{
    /// <summary>ブラウザから直接開けるゲーム。</summary>
    Browser,
    /// <summary>Unity用のプロジェクトソース。</summary>
    Unity,
    /// <summary>Avalonia用のプロジェクトソース。</summary>
    Avalonia
}
