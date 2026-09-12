using System.Collections.Generic;
using GameMockStudio.Brief.Planning;

namespace GameMockStudio.Brief.Discovery;

/// <summary>検証前の仮説を、編集可能な企画の出発点として提供する。</summary>
public sealed record IdeaSeed
{
    /// <summary>案の対象領域。</summary>
    public required MockKind Kind { get; init; }
    /// <summary>選択肢として表示する組み合わせ。</summary>
    public required string Title { get; init; }
    /// <summary>価値の仮説と最初に試す体験。</summary>
    public required string Description { get; init; }
    /// <summary>ゲームを構成するジャンル。サービスでは空配列。</summary>
    public required string[] Genres { get; init; }
    /// <summary>編集の出発点となる企画項目。</summary>
    public required Dictionary<string, string> Values { get; init; }

    /// <summary>選択中の生成形式で、元の案と独立した下書きを作る。</summary>
    public BriefDocument CreateBrief(MockFormat format)
    {
        return new BriefDocument { Kind = Kind, Format = format, Genres = Genres, Values = Values }.Normalize();
    }
}
