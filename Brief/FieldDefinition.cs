namespace GameMockStudio.Brief;

/// <summary>企画入力欄の識別子、説明、記入例を保持する。</summary>
public sealed record FieldDefinition
{
    /// <summary>保存形式で使用する安定した識別子。</summary>
    public required string Identifier { get; init; }
    /// <summary>入力欄の表示名。</summary>
    public required string Label { get; init; }
    /// <summary>入力時に判断する内容。</summary>
    public required string Description { get; init; }
    /// <summary>自由入力の参考例。</summary>
    public required string Example { get; init; }
}
