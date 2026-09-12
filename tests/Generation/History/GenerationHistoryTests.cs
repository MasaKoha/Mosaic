using System;
using System.IO;
using System.Linq;
using GameMockStudio.Brief;
using GameMockStudio.Generation.History;
using Xunit;

namespace GameMockStudio.Tests.Generation.History;

/// <summary>履歴の上限・重複・異常終了時の結果遷移を検証する。</summary>
public sealed class GenerationHistoryTests
{
    /// <summary>異常終了した生成を成功にせず、新しい順の重複なし履歴へ復元する。</summary>
    [Fact]
    public void RestoreMarksRunningAsInterruptedAndKeepsNewestEntries()
    {
        using var history = new GenerationHistory();
        var entries = Enumerable.Range(0, GenerationHistory.MaximumEntries + 1).Select(number =>
            new GenerationHistoryEntry
            {
                StartedAt = DateTimeOffset.UnixEpoch.AddMinutes(number),
                OutputDirectory = Path.Combine(Path.GetTempPath(), "mock-history-" + number),
                Format = MockFormat.Browser,
                Outcome = GenerationOutcome.Running
            }).ToArray();
        history.Restore(entries.Append(entries[^1]));
        Assert.Equal(GenerationHistory.MaximumEntries, history.Entries.Count);
        Assert.Equal(entries[^1].OutputDirectory, history.Entries[0].OutputDirectory);
        Assert.All(history.Entries, entry => Assert.Equal(GenerationOutcome.Interrupted, entry.Outcome));
        history.Record(history.Entries[0] with { Outcome = GenerationOutcome.Completed });
        Assert.Equal(GenerationHistory.MaximumEntries, history.Entries.Count);
        Assert.Equal(GenerationOutcome.Completed, history.Entries[0].Outcome);
    }
}
