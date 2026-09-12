using System.Collections.Generic;

namespace GameMockStudio.Brief;

/// <summary>同じ企画判断に関係する入力欄をまとめる。</summary>
public sealed record CategoryDefinition
{
    /// <summary>カテゴリの表示名。</summary>
    public required string Label { get; init; }
    /// <summary>カテゴリの説明。</summary>
    public required string Description { get; init; }
    /// <summary>表示順に並んだ入力欄。</summary>
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }
}
