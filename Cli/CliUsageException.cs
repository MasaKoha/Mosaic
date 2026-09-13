using System;

namespace GameMockStudio.Cli;

/// <summary>実行時の検証失敗と区別する引数の指定誤り。</summary>
internal sealed class CliUsageException(string message) : Exception(message)
{
}
