using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using GameMockStudio.Brief;

namespace GameMockStudio.Workspace.Editing;

/// <summary>最大3種類のジャンルを候補選択または自由入力する。</summary>
public sealed class GenreEditor
{
    private const double SectionSpacing = 10;
    private readonly AutoCompleteBox[] inputs;

    /// <summary>固定数の入力欄でジャンル上限を表現する。</summary>
    public GenreEditor()
    {
        string[] suggestions =
        [
            "アクション", "アドベンチャー", "RPG", "ローグライク", "ローグライト", "パズル",
            "シミュレーション", "ストラテジー", "タワーディフェンス", "シューティング", "リズム",
            "レース", "スポーツ", "格闘", "プラットフォーマー", "サバイバル", "ホラー", "ステルス",
            "カード", "デッキ構築", "ボードゲーム", "ノベル", "経営", "育成", "農業", "クラフト",
            "サンドボックス", "探索", "メトロイドヴァニア", "放置", "パーティー", "教育"
        ];
        inputs = Enumerable.Range(1, BriefDocument.MaximumGenres).Select(number =>
        {
            var input = new AutoCompleteBox
            {
                ItemsSource = suggestions,
                PlaceholderText = $"ジャンル {number}（未入力可）",
                MinimumPrefixLength = 0,
                FilterMode = AutoCompleteFilterMode.Contains
            };
            AutomationProperties.SetName(input, $"ジャンル {number}");
            return input;
        }).ToArray();
        var panel = new StackPanel { Spacing = SectionSpacing };
        panel.Children.Add(new TextBlock { Text = "ジャンル  /  最大3種類", FontWeight = FontWeight.SemiBold });
        panel.Children.Add(new TextBlock
        {
            Text = "候補から選択、または自由に入力。すべて空欄なら1〜3種類をAIが決定します。",
            TextWrapping = TextWrapping.Wrap,
            Classes = { "muted" }
        });
        foreach (var input in inputs)
        {
            panel.Children.Add(input);
        }
        Control = new Border { Child = panel, Classes = { "card" } };
        Changes = inputs.Select(input => input.GetObservable(AutoCompleteBox.TextProperty).Skip(1)
            .Select(_ => Unit.Default)).Merge();
    }

    /// <summary>ジャンル入力のコントロール。</summary>
    public Border Control { get; }
    /// <summary>ジャンルの変更通知。</summary>
    public IObservable<Unit> Changes { get; }

    /// <summary>空欄と重複を除いた指定ジャンルを返す。</summary>
    public string[] Capture()
    {
        return inputs.Select(input => input.Text?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>検証済みのジャンルを表示する。</summary>
    public void Apply(string[] genres)
    {
        for (var index = 0; index < inputs.Length; index++)
        {
            inputs[index].Text = index < genres.Length ? genres[index] : string.Empty;
        }
    }
}
