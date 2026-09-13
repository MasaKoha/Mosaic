using GameMockStudio.Brief;

namespace GameMockStudio.Cli.Interview;

/// <summary>次に尋ねる未回答項目と所属カテゴリ。</summary>
public sealed record InterviewQuestion
{
    /// <summary>質問の背景を示すカテゴリ。</summary>
    public required CategoryDefinition Category { get; init; }
    /// <summary>回答先の識別子と入力案内。</summary>
    public required FieldDefinition Field { get; init; }
}
