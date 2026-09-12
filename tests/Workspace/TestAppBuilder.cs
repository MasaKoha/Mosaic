using Avalonia;
using Avalonia.Headless;
using GameMockStudio.Tests.Workspace;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace GameMockStudio.Tests.Workspace;

/// <summary>実際の画面とテーマを、OS操作やネット通信なしで検証する。</summary>
public sealed class TestAppBuilder
{
    /// <summary>ワークスペース用のヘッドレスアプリを構成する。</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
