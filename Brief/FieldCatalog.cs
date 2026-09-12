using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using GameMockStudio.Brief.Planning;

namespace GameMockStudio.Brief;

/// <summary>画面と生成指示が共有する企画項目の定義を読み込む。</summary>
public sealed class FieldCatalog
{
    /// <summary>埋め込み定義を検証して読み込む。</summary>
    public FieldCatalog(MockKind kind = MockKind.Game)
    {
        Kind = kind;
        Categories = kind switch
        {
            MockKind.Game => Read("field-catalog.json"),
            MockKind.Service => Read("Planning.service-fields.json"),
            MockKind.Gamification => Read("Planning.gamification-fields.json").Concat(Read("field-catalog.json")).ToArray(),
            _ => throw new InvalidOperationException("対応していない企画の種類です。")
        };
        Fields = Categories.SelectMany(category => category.Fields).ToArray();
        if (Fields.Count == 0 || Fields.Any(field => string.IsNullOrWhiteSpace(field.Identifier))
            || Fields.Select(field => field.Identifier).Distinct().Count() != Fields.Count)
        {
            throw new InvalidOperationException("企画項目の識別子が空、または重複しています。");
        }
    }

    /// <summary>表示順のカテゴリ。</summary>
    public IReadOnlyList<CategoryDefinition> Categories { get; }
    /// <summary>全入力欄。</summary>
    public IReadOnlyList<FieldDefinition> Fields { get; }
    /// <summary>この項目群が設計する体験。</summary>
    public MockKind Kind { get; }
    /// <summary>利用者へ表示する種類名。</summary>
    public string Label => Kind switch
    {
        MockKind.Game => "ゲーム",
        MockKind.Service => "サービス",
        _ => "ゲーミフィケーション"
    };

    private IReadOnlyList<CategoryDefinition> Read(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GameMockStudio.Brief." + resourceName)
            ?? throw new InvalidOperationException("企画項目の定義が見つかりません。");
        return JsonSerializer.Deserialize<List<CategoryDefinition>>(stream)
            ?? throw new InvalidOperationException("企画項目の定義が空です。");
    }
}
