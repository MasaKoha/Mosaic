using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GameMockStudio.Brief;

namespace GameMockStudio.Workspace.Editing;

/// <summary>一つの企画項目の入力とランダム状態を表示する。</summary>
public sealed class FieldEditor : IDisposable
{
    private const double FieldSpacing = 8;
    private const double InputMinimumHeight = 64;
    private readonly TextBox input;
    private readonly CompositeDisposable subscriptions = new();

    /// <summary>項目定義に対応する入力カードを作る。</summary>
    public FieldEditor(FieldDefinition definition)
    {
        Definition = definition;
        var state = new TextBlock { Text = "ランダム", Classes = { "caption" }, VerticalAlignment = VerticalAlignment.Center };
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new TextBlock { Text = definition.Label, FontWeight = FontWeight.SemiBold });
        Grid.SetColumn(state, 1);
        heading.Children.Add(state);
        input = new TextBox
        {
            PlaceholderText = definition.Example,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = InputMinimumHeight
        };
        AutomationProperties.SetName(input, definition.Label);
        var panel = new StackPanel { Spacing = FieldSpacing };
        panel.Children.Add(heading);
        panel.Children.Add(new TextBlock { Text = definition.Description, TextWrapping = TextWrapping.Wrap, Classes = { "muted" } });
        panel.Children.Add(input);
        Control = new Border { Child = panel, Classes = { "card" } };
        Changes = input.GetObservable(TextBox.TextProperty).Skip(1).Select(_ => Unit.Default);
        subscriptions.Add(input.GetObservable(TextBox.TextProperty).Subscribe(value =>
        {
            state.Text = string.IsNullOrWhiteSpace(value) ? "ランダム" : "指定済み";
        }));
    }

    /// <summary>このカードの企画定義。</summary>
    public FieldDefinition Definition { get; }
    /// <summary>画面へ配置するコントロール。</summary>
    public Border Control { get; }
    /// <summary>入力内容の変更通知。</summary>
    public IObservable<Unit> Changes { get; }
    /// <summary>保存する入力値。</summary>
    public string Value => input.Text?.Trim() ?? string.Empty;

    /// <summary>保存された指定を表示する。</summary>
    public void SetValue(string value)
    {
        input.Text = value;
    }

    /// <summary>検索語が説明・例・指定内容に含まれるか判定する。</summary>
    public bool Matches(string query)
    {
        var searchable = $"{Definition.Label} {Definition.Description} {Definition.Example} {Value}";
        return searchable.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>入力状態への購読を解除する。</summary>
    public void Dispose()
    {
        subscriptions.Dispose();
    }
}
