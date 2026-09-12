using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GameMockStudio.Workspace;

namespace GameMockStudio;

/// <summary>テーマとウィンドウのライフサイクルを構成する。</summary>
public sealed partial class App : Application
{
    /// <summary>共通テーマを読み込む。</summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>デスクトップのメインウィンドウを作成する。</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new WorkspaceWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
