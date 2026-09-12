using System;
using Avalonia;

namespace GameMockStudio;

/// <summary>デスクトップアプリの起動入口。</summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] arguments)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(arguments);
    }

    /// <summary>デザイナーと実行時で共通のアプリ構成を返す。</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
    }
}
