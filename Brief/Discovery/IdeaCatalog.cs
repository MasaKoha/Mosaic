using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using GameMockStudio.Brief.Planning;

namespace GameMockStudio.Brief.Discovery;

/// <summary>効果を保証しない検討用の案を、企画項目と対応付けて読み込む。</summary>
public sealed class IdeaCatalog
{
    private readonly IdeaSeed[] ideas;

    /// <summary>埋め込みの案に不明な項目や種類の混入がないか確認する。</summary>
    public IdeaCatalog(FieldCatalogs catalogs)
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("GameMockStudio.Brief.Discovery.idea-seeds.json")
            ?? throw new InvalidOperationException("アイデアの定義が見つかりません。");
        ideas = JsonSerializer.Deserialize<IdeaSeed[]>(stream)
            ?? throw new InvalidOperationException("アイデアの定義が空です。");
        foreach (var idea in ideas)
        {
            var fields = catalogs.ForKind(idea.Kind).Fields.Select(field => field.Identifier).ToHashSet();
            var brief = idea.CreateBrief(MockFormat.Browser);
            if (brief.Values.Keys.Any(identifier => !fields.Contains(identifier)))
            {
                throw new InvalidOperationException("アイデアに未知の企画項目があります。");
            }
        }
    }

    /// <summary>選択した種類の検討用の案を返す。</summary>
    public IReadOnlyList<IdeaSeed> ForKind(MockKind kind)
    {
        return ideas.Where(idea => idea.Kind == kind).ToArray();
    }
}
