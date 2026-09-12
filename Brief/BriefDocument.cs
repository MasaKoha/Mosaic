using System;
using System.Collections.Generic;
using System.Linq;
using GameMockStudio.Brief.Planning;

namespace GameMockStudio.Brief;

/// <summary>指定された企画とランダム指定を保存する。</summary>
public sealed record BriefDocument
{
    /// <summary>保存形式の現行バージョン。</summary>
    public const int CurrentVersion = 2;
    /// <summary>選択できるジャンル数の上限。</summary>
    public const int MaximumGenres = 3;
    /// <summary>互換性を判定する形式バージョン。</summary>
    public int Version { get; init; } = CurrentVersion;
    /// <summary>ゲーム、サービス、ゲーミフィケーションの区別。旧企画はゲームとして読む。</summary>
    public MockKind Kind { get; init; } = MockKind.Game;
    /// <summary>生成モックの実装環境。</summary>
    public MockFormat Format { get; init; } = MockFormat.Browser;
    /// <summary>空なら1〜3種類をAIが決定するジャンル。</summary>
    public string[] Genres { get; init; } = [];
    /// <summary>空欄または未登録のキーはランダム指定。</summary>
    public Dictionary<string, string> Values { get; init; } = new();

    /// <summary>外部ファイルの不正値を拒否し、空白と重複を正規化する。</summary>
    public BriefDocument Normalize()
    {
        if (Version is not (1 or CurrentVersion) || !Enum.IsDefined(Format) || !Enum.IsDefined(Kind)
            || Genres is null || Values is null || (Version == 1 && Kind != MockKind.Game))
        {
            throw new InvalidOperationException("対応していない企画ファイルです。");
        }
        var genres = Genres.Where(genre => !string.IsNullOrWhiteSpace(genre))
            .Select(genre => genre.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (genres.Length > MaximumGenres)
        {
            throw new InvalidOperationException("ジャンルは最大3種類です。");
        }
        if (Kind == MockKind.Service && (Format == MockFormat.Unity || genres.Length > 0))
        {
            throw new InvalidOperationException("サービス企画はHTMLまたはAvaloniaで作成し、ゲームジャンルは指定しません。");
        }
        return this with
        {
            Version = CurrentVersion,
            Genres = genres,
            Values = Values.ToDictionary(pair => pair.Key, pair => (pair.Value ?? string.Empty).Trim())
        };
    }
}
