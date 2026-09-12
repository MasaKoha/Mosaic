using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace GameMockStudio.Tests.Generation.Fixtures;

/// <summary>POSIXシェルを使う実プロセス検証を対応環境だけで実行する。</summary>
public sealed class UnixFactAttribute : FactAttribute
{
    /// <summary>Windowsでは別の実行形式が必要なため未実行であることを明示する。</summary>
    public UnixFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "偽CLIのプロセス検証はmacOS/Linuxを対象としています。";
        }
    }
}
