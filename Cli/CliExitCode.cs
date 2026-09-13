namespace GameMockStudio.Cli;

/// <summary>ターミナルとエージェントが判定する実行結果。</summary>
public enum CliExitCode
{
    /// <summary>要求された操作が完了した。</summary>
    Success = 0,
    /// <summary>検証、保存、生成またはキャンセルにより完了しなかった。</summary>
    Failure = 1,
    /// <summary>コマンドまたはオプションの指定が不正だった。</summary>
    UsageError = 2
}
