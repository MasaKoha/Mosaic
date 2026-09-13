using System;
using Avalonia;
using GameMockStudio.Cli;

namespace GameMockStudio;

/// <summary>デスクトップアプリとCLIの起動入口。</summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] arguments)
    {
        if (arguments.Length > 0 && arguments[0] == "cli")
        {
            return CliApplication.Run(arguments[1..]);
        }
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(arguments);
    }

    /// <summary>デザイナーと実行時で共通のアプリ構成を返す。</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
    }
}
