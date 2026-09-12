using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;

namespace GameMockStudio.Generation.History;

/// <summary>生成履歴を新しい順に保持し、実行結果の遷移を通知する。</summary>
public sealed class GenerationHistory : IDisposable
{
    /// <summary>画面と自動保存へ残す履歴数。</summary>
    public const int MaximumEntries = 20;
    private readonly List<GenerationHistoryEntry> entries = [];
    private readonly Subject<Unit> changes = new();

    /// <summary>新しい順の生成履歴。</summary>
    public IReadOnlyList<GenerationHistoryEntry> Entries => entries;
    /// <summary>履歴の追加と結果更新。</summary>
    public IObservable<Unit> Changes => changes;

    /// <summary>保存された実行中の記録を中断扱いにし、重複を除いて復元する。</summary>
    public void Restore(IEnumerable<GenerationHistoryEntry> saved)
    {
        var restored = saved.Select(Validate).OrderByDescending(entry => entry.StartedAt)
            .DistinctBy(entry => entry.OutputDirectory).Take(MaximumEntries)
            .Select(entry => entry.Outcome == GenerationOutcome.Running
                ? entry with { Outcome = GenerationOutcome.Interrupted } : entry).ToArray();
        entries.Clear();
        entries.AddRange(restored);
        changes.OnNext(Unit.Default);
    }

    /// <summary>開始済みの生成を重複登録せず、最新の結果へ更新する。</summary>
    public void Record(GenerationHistoryEntry entry)
    {
        Validate(entry);
        entries.RemoveAll(existing => existing.OutputDirectory == entry.OutputDirectory);
        entries.Insert(0, entry);
        if (entries.Count > MaximumEntries)
        {
            entries.RemoveRange(MaximumEntries, entries.Count - MaximumEntries);
        }
        changes.OnNext(Unit.Default);
    }

    /// <summary>結果通知の寿命を終了する。</summary>
    public void Dispose()
    {
        changes.Dispose();
    }

    private GenerationHistoryEntry Validate(GenerationHistoryEntry entry)
    {
        if (entry is null)
        {
            throw new InvalidOperationException("保存された生成履歴が不正です。");
        }
        return entry.Validate();
    }
}
