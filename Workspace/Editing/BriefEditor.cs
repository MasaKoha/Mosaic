using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using GameMockStudio.Brief;

namespace GameMockStudio.Workspace.Editing;

/// <summary>カテゴリ切り替えと全項目検索を備えた企画フォーム。</summary>
public sealed class BriefEditor : IDisposable
{
    private const double SectionSpacing = 12;
    private const double HeadingFontSize = 22;
    private readonly FieldCatalog catalog;
    private readonly ListBox navigation;
    private readonly TextBox search;
    private readonly TextBlock completion;
    private readonly ScrollViewer scroll;
    private readonly GenreEditor genres = new();
    private readonly Dictionary<string, FieldEditor> fields;
    private Dictionary<string, string> unknownValues = new();
    private readonly List<StackPanel> sections = [];
    private readonly TextBlock noResults = new() { Text = "一致する項目がありません。検索語を変えてください。", IsVisible = false };
    private readonly CompositeDisposable subscriptions = new();

    /// <summary>カタログの全項目を一度作り、非表示の入力内容も維持する。</summary>
    public BriefEditor(FieldCatalog catalog, WorkspaceControls controls)
    {
        this.catalog = catalog;
        navigation = controls.Find<ListBox>("CategoryList");
        search = controls.Find<TextBox>("SearchBox");
        completion = controls.Find<TextBlock>("CompletionText");
        scroll = controls.Find<ScrollViewer>("EditorScroll");
        var host = controls.Find<StackPanel>("EditorHost");
        fields = catalog.Fields.ToDictionary(field => field.Identifier, field => new FieldEditor(field));
        navigation.ItemsSource = catalog.Categories.Select(category => category.Label).ToArray();
        navigation.SelectedIndex = 0;
        host.Children.Add(genres.Control);
        foreach (var category in catalog.Categories)
        {
            var section = CreateSection(category);
            sections.Add(section);
            host.Children.Add(section);
        }
        host.Children.Add(noResults);
        Changes = fields.Values.Select(field => field.Changes).Append(genres.Changes).Merge();
        subscriptions.Add(Changes.Subscribe(_ => UpdateCompletion()));
        subscriptions.Add(navigation.GetObservable(SelectingItemsControl.SelectedIndexProperty)
            .Subscribe(_ => Filter()));
        subscriptions.Add(search.GetObservable(TextBox.TextProperty).Subscribe(_ => Filter()));
        UpdateCompletion();
    }

    /// <summary>企画の入力変更。</summary>
    public IObservable<Unit> Changes { get; }

    /// <summary>非表示のカテゴリを含む全指定を読み取る。</summary>
    public BriefDocument Capture(MockFormat format)
    {
        return new BriefDocument
        {
            Format = format,
            Genres = genres.Capture(),
            Values = unknownValues.Concat(fields.Select(pair =>
                new KeyValuePair<string, string>(pair.Key, pair.Value.Value)))
                .ToDictionary(pair => pair.Key, pair => pair.Value)
        }.Normalize();
    }

    /// <summary>企画をフォーム全体へ反映する。</summary>
    public void Apply(BriefDocument document)
    {
        unknownValues = document.Values.Where(pair => !fields.ContainsKey(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var field in fields)
        {
            field.Value.SetValue(document.Values.GetValueOrDefault(field.Key, string.Empty));
        }
        genres.Apply(document.Genres);
        search.Text = string.Empty;
        navigation.SelectedIndex = 0;
        Filter();
        UpdateCompletion();
    }

    /// <summary>フォームと個別項目の購読を破棄する。</summary>
    public void Dispose()
    {
        subscriptions.Dispose();
        foreach (var field in fields.Values)
        {
            field.Dispose();
        }
    }

    private StackPanel CreateSection(CategoryDefinition category)
    {
        var section = new StackPanel { Spacing = SectionSpacing };
        section.Children.Add(new TextBlock { Text = category.Label, FontSize = HeadingFontSize, FontWeight = FontWeight.SemiBold });
        section.Children.Add(new TextBlock { Text = category.Description, Classes = { "muted" }, TextWrapping = TextWrapping.Wrap });
        foreach (var field in category.Fields)
        {
            section.Children.Add(fields[field.Identifier].Control);
        }
        return section;
    }

    private void Filter()
    {
        var query = search.Text?.Trim() ?? string.Empty;
        var searching = query.Length > 0;
        genres.Control.IsVisible = searching ? "ジャンル genre".Contains(query, StringComparison.OrdinalIgnoreCase)
            : navigation.SelectedIndex == 0;
        for (var categoryIndex = 0; categoryIndex < catalog.Categories.Count; categoryIndex++)
        {
            FilterSection(categoryIndex, query, searching);
        }
        noResults.IsVisible = !genres.Control.IsVisible && sections.All(section => !section.IsVisible);
        scroll.Offset = default;
    }

    private void FilterSection(int categoryIndex, string query, bool searching)
    {
        var category = catalog.Categories[categoryIndex];
        var categoryMatches = category.Label.Contains(query, StringComparison.OrdinalIgnoreCase);
        var visible = false;
        foreach (var definition in category.Fields)
        {
            var field = fields[definition.Identifier];
            field.Control.IsVisible = !searching || categoryMatches || field.Matches(query);
            visible |= field.Control.IsVisible;
        }
        sections[categoryIndex].IsVisible = searching ? visible : categoryIndex == navigation.SelectedIndex;
    }

    private void UpdateCompletion()
    {
        var specified = fields.Values.Count(field => field.Value.Length > 0);
        var selectedGenres = genres.Capture();
        completion.Text = $"{specified} / {fields.Count} 項目を指定\n残り {fields.Count - specified} 項目はランダム\n"
            + (selectedGenres.Length == 0 ? "ジャンル: ランダム" : $"ジャンル: {selectedGenres.Length} 種類")
            + (unknownValues.Count > 0 ? $"\n追加項目 {unknownValues.Count} 件を保持" : string.Empty);
    }
}
