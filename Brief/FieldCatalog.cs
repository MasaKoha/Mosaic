using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace GameMockStudio.Brief;

/// <summary>画面と生成指示が共有する企画項目の定義を読み込む。</summary>
public sealed class FieldCatalog
{
    private const string ResourceName = "GameMockStudio.Brief.field-catalog.json";

    /// <summary>埋め込み定義を検証して読み込む。</summary>
    public FieldCatalog()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("企画項目の定義が見つかりません。");
        Categories = JsonSerializer.Deserialize<List<CategoryDefinition>>(stream)
            ?? throw new InvalidOperationException("企画項目の定義が空です。");
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
}
