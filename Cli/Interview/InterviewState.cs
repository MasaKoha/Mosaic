using System;
using System.Collections.Generic;
using System.Linq;
using GameMockStudio.Brief;
using GameMockStudio.Brief.Planning;

namespace GameMockStudio.Cli.Interview;

/// <summary>ファイル操作を持たず、カタログ順の質問と企画の回答状態を決定する。</summary>
public sealed class InterviewState(FieldCatalogs catalogs)
{
    /// <summary>対象範囲で最初の未回答を返す。ジャンル未指定は質問の完了を妨げない。</summary>
    public InterviewQuestion? FindNext(BriefDocument brief, string? categoryLabel = null)
    {
        foreach (var category in SelectCategories(brief, categoryLabel))
        {
            var field = category.Fields.FirstOrDefault(candidate => !brief.Values.ContainsKey(candidate.Identifier));
            if (field is not null)
            {
                return new InterviewQuestion { Category = category, Field = field };
            }
        }
        return null;
    }

    /// <summary>既存の未知キーを除き、対象カタログ内だけで回答数と指定数を数える。</summary>
    public InterviewProgress GetProgress(BriefDocument brief, string? categoryLabel = null)
    {
        var fields = SelectCategories(brief, categoryLabel).SelectMany(category => category.Fields).ToArray();
        return new InterviewProgress
        {
            Answered = fields.Count(field => brief.Values.ContainsKey(field.Identifier)),
            Specified = fields.Count(field => brief.Values.TryGetValue(field.Identifier, out var value)
                && !string.IsNullOrWhiteSpace(value)),
            Total = fields.Length
        };
    }

    /// <summary>全識別子を検証してから独立した企画へ回答を反映し、未知の既存キーを保持する。</summary>
    public BriefDocument ApplyAnswers(BriefDocument brief, IReadOnlyDictionary<string, string> answers)
    {
        var identifiers = catalogs.ForKind(brief.Kind).Fields.Select(field => field.Identifier).ToHashSet(StringComparer.Ordinal);
        var unknown = answers.Keys.FirstOrDefault(identifier => !identifiers.Contains(identifier));
        if (unknown is not null)
        {
            throw new InvalidOperationException($"この種類に存在しない項目です: {unknown}");
        }
        var values = new Dictionary<string, string>(brief.Values, StringComparer.Ordinal);
        foreach (var answer in answers)
        {
            values[answer.Key] = answer.Value;
        }
        return brief with { Values = values };
    }

    /// <summary>未回答だけをAI補完にし、既存の回答と外部JSONの未知キーを保持する。</summary>
    public BriefDocument Finish(BriefDocument brief)
    {
        var values = new Dictionary<string, string>(brief.Values, StringComparer.Ordinal);
        foreach (var field in catalogs.ForKind(brief.Kind).Fields)
        {
            values.TryAdd(field.Identifier, string.Empty);
        }
        return brief with { Values = values };
    }

    private IReadOnlyList<CategoryDefinition> SelectCategories(BriefDocument brief, string? categoryLabel)
    {
        var categories = catalogs.ForKind(brief.Kind).Categories;
        if (categoryLabel is null)
        {
            return categories;
        }
        var selected = categories.FirstOrDefault(category => category.Label == categoryLabel)
            ?? throw new InvalidOperationException($"この種類に存在しないカテゴリです: {categoryLabel}");
        return [selected];
    }
}
