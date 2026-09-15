namespace GameMockStudio.Cli.Interview;

/// <summary>回答の有無と具体的な指定を区別した進捗。</summary>
public sealed record InterviewProgress
{
    /// <summary>空文字も含め、キーが存在する項目数。</summary>
    public required int Answered { get; init; }
    /// <summary>空白以外の指定がある項目数。不採用も含む。</summary>
    public required int Specified { get; init; }
    /// <summary>対象カタログ内の項目数。</summary>
    public required int Total { get; init; }
}
