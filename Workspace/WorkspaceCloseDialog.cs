using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace GameMockStudio.Workspace;

/// <summary>保存できない場合に、編集を続けるか未保存のまま終了するかを選ぶ。</summary>
public sealed class WorkspaceCloseDialog
{
    private const double DialogWidth = 480;
    private const double PanelSpacing = 16;

    /// <summary>明示的に選ばれた場合だけ未保存の終了を許可する。</summary>
    public async Task<bool> ConfirmDiscardAsync(Window owner, string error)
    {
        var dialog = new Window
        {
            Title = "企画を保存できませんでした",
            Width = DialogWidth,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var continueEditing = new Button { Content = "編集に戻る" };
        var discard = new Button { Content = "保存せず閉じる" };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = PanelSpacing };
        buttons.Children.Add(continueEditing);
        buttons.Children.Add(discard);
        var panel = new StackPanel { Margin = new Thickness(PanelSpacing), Spacing = PanelSpacing };
        panel.Children.Add(new TextBlock
        {
            Text = $"{error}\n\n編集に戻って「企画を保存」で別の場所へ保存できます。保存せず閉じると、最後の保存以降の変更は失われます。",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(buttons);
        dialog.Content = panel;
        using var subscriptions = new CompositeDisposable(
            continueEditing.GetObservable(Button.ClickEvent).Subscribe(_ => dialog.Close(false)),
            discard.GetObservable(Button.ClickEvent).Subscribe(_ => dialog.Close(true)));
        return await dialog.ShowDialog<bool>(owner);
    }
}
