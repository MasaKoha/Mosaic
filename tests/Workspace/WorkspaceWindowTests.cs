using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GameMockStudio.Brief;
using GameMockStudio.Generation.History;
using GameMockStudio.Storage;
using GameMockStudio.Workspace;
using Xunit;

namespace GameMockStudio.Tests.Workspace;

/// <summary>実画面の入力・非同期保存・終了・復元が途切れないことを検証する。</summary>
public sealed class WorkspaceWindowTests : IDisposable
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DialogObservationInterval = TimeSpan.FromMilliseconds(25);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "mock-window-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>最後のキー入力と設定、未知の項目を終了直後の再起動へ引き継ぐ。</summary>
    [AvaloniaFact]
    public async Task ImmediateClosePreservesLatestInputSettingsAndUnknownFields()
    {
        using var store = new WorkspaceSessionStore(directory);
        await store.SaveAsync(CreateSession(new BriefDocument { Values = new() { ["future_feature"] = "将来の指定" } }), CancellationToken.None);
        var window = await OpenAsync();
        FirstInput(window).Text = "閉じる直前の日本語\n二行目";
        window.FindControl<TextBox>("ExecutableBox")!.Text = "/custom/path/codex";
        window.FindControl<TextBox>("OutputRootBox")!.Text = Path.Combine(directory, "出力先");
        await CloseAsync(window);

        var reopened = await OpenAsync();
        Assert.Equal("閉じる直前の日本語\n二行目", FirstInput(reopened).Text);
        Assert.Equal("/custom/path/codex", reopened.FindControl<TextBox>("ExecutableBox")!.Text);
        Assert.Equal(Path.Combine(directory, "出力先"), reopened.FindControl<TextBox>("OutputRootBox")!.Text);
        await CloseAsync(reopened);
        Assert.Equal("将来の指定", (await store.LoadAsync(CancellationToken.None))!.Brief.Values["future_feature"]);
    }

    /// <summary>自動保存の間隔を待てば終了前にも編集内容がディスクへ残る。</summary>
    [AvaloniaFact]
    public async Task EditingAutomaticallySavesWithoutClosing()
    {
        var window = await OpenAsync();
        FirstInput(window).Text = "自動保存する企画";
        await WaitForTextAsync(window, "SaveStatusText", "自動保存済み");
        using var store = new WorkspaceSessionStore(directory);
        var saved = await store.LoadAsync(CancellationToken.None);
        Assert.Contains("自動保存する企画", saved!.Brief.Values.Values);
        await CloseAsync(window);
    }

    /// <summary>起動直後に閉じても復元完了まで待ち、終了中の追加入力を許可しない。</summary>
    [AvaloniaFact]
    public async Task ClosingDuringRestoreKeepsWindowDisabledUntilSaved()
    {
        using var store = new WorkspaceSessionStore(directory);
        await store.SaveAsync(CreateSession(new BriefDocument { Values = new() { ["title"] = "前回の企画" } }), CancellationToken.None);
        var window = new WorkspaceWindow(directory);
        window.Show();
        var closed = ObserveClosed(window);
        window.Close();
        var enabledStates = new List<bool>();
        using var observation = window.GetObservable(InputElement.IsEnabledProperty).Subscribe(enabledStates.Add);
        await closed;
        Assert.DoesNotContain(true, enabledStates);
        Assert.Equal("前回の企画", (await store.LoadAsync(CancellationToken.None))!.Brief.Values["title"]);
    }

    /// <summary>破損したセッションを退避し、編集・新規企画・終了が継続できる。</summary>
    [AvaloniaFact]
    public async Task CorruptSessionIsPreservedAndWorkspaceRemainsUsable()
    {
        Directory.CreateDirectory(directory);
        const string brokenContent = "{broken session";
        await File.WriteAllTextAsync(Path.Combine(directory, "session.json"), brokenContent);
        var window = await OpenAsync();
        Assert.Contains("退避先", window.FindControl<TextBlock>("StatusText")!.Text);
        FirstInput(window).Text = "退避する企画";
        Click(window, "NewButton");
        await WaitForTextAsync(window, "StatusText", "企画を切り替えました");
        Assert.True(string.IsNullOrEmpty(FirstInput(window).Text));
        var recovery = Assert.Single(Directory.GetFiles(Path.Combine(directory, "Recovery"), "brief-*.json"));
        var previous = await new BriefStore().LoadAsync(recovery, CancellationToken.None);
        Assert.Contains("退避する企画", previous.Values.Values);
        await CloseAsync(window);
        var preserved = Assert.Single(Directory.GetFiles(directory, "session-unreadable-*.json"));
        Assert.Equal(brokenContent, await File.ReadAllTextAsync(preserved));
    }

    /// <summary>過去の補完企画を取り込む前に、現在の入力を退避する。</summary>
    [AvaloniaFact]
    public async Task CompletedHistoryCanRestoreResolvedBriefWithoutLosingCurrentDraft()
    {
        var outputDirectory = Path.Combine(directory, "completed-game");
        Directory.CreateDirectory(outputDirectory);
        var catalog = new FieldCatalog();
        var firstIdentifier = catalog.Fields[0].Identifier;
        await new BriefStore().SaveAsync(Path.Combine(outputDirectory, "resolved-brief.json"),
            new BriefDocument { Values = new() { [firstIdentifier] = "補完された企画" } }, CancellationToken.None);
        using var store = new WorkspaceSessionStore(directory);
        var session = CreateSession(new BriefDocument { Values = new() { [firstIdentifier] = "編集中の企画" } }) with
        {
            History = [new GenerationHistoryEntry
            {
                StartedAt = DateTimeOffset.Now, OutputDirectory = outputDirectory,
                Format = MockFormat.Browser, Outcome = GenerationOutcome.Completed
            }]
        };
        await store.SaveAsync(session, CancellationToken.None);
        var window = await OpenAsync();
        Assert.True(window.FindControl<Button>("UseResolvedButton")!.IsEnabled);
        Assert.False(window.FindControl<Button>("PlayButton")!.IsEnabled);
        Click(window, "UseResolvedButton");
        await WaitForTextAsync(window, "StatusText", "企画を切り替えました");
        Assert.Equal("補完された企画", FirstInput(window).Text);
        var recovery = Assert.Single(Directory.GetFiles(Path.Combine(directory, "Recovery"), "brief-*.json"));
        Assert.Equal("編集中の企画", (await new BriefStore().LoadAsync(recovery, CancellationToken.None)).Values[firstIdentifier]);
        await CloseAsync(window);
    }

    /// <summary>保存失敗後に編集へ戻り、保存先の問題を解消して正常終了できる。</summary>
    [AvaloniaFact]
    public async Task FailedCloseCanReturnToEditingAndRetry()
    {
        var window = await OpenAsync();
        FirstInput(window).Text = "保存に失敗しても残す企画";
        var blockedPath = Path.Combine(directory, "session.json");
        Directory.CreateDirectory(blockedPath);
        window.Close();
        var dialog = await WaitForDialogAsync(window);
        ClickByContent(dialog, "編集に戻る");
        Assert.True(window.IsVisible);
        Assert.True(window.IsEnabled);
        Assert.Equal("保存に失敗しても残す企画", FirstInput(window).Text);
        Directory.Delete(blockedPath);
        await CloseAsync(window);
        using var store = new WorkspaceSessionStore(directory);
        Assert.Contains("保存に失敗しても残す企画", (await store.LoadAsync(CancellationToken.None))!.Brief.Values.Values);
    }

    /// <summary>復元先にアクセスできなくても企画操作と明示的な終了を継続できる。</summary>
    [AvaloniaFact]
    public async Task UnreadableSessionDoesNotDisableCommandsOrTrapTheWindow()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("読み取り拒否の再現はPOSIXのファイル権限を使用します。");
            return;
        }
        Directory.CreateDirectory(directory);
        var sessionPath = Path.Combine(directory, "session.json");
        await File.WriteAllTextAsync(sessionPath, "preserve original", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(sessionPath, UnixFileMode.None);
        try
        {
            var window = await OpenAsync();
            Assert.Contains("手動保存", window.FindControl<TextBlock>("SaveStatusText")!.Text);
            FirstInput(window).Text = "復元に失敗しても編集できる";
            Click(window, "NewButton");
            await WaitForTextAsync(window, "StatusText", "企画を切り替えました");
            Assert.True(string.IsNullOrEmpty(FirstInput(window).Text));
            var closed = ObserveClosed(window);
            window.Close();
            var dialog = await WaitForDialogAsync(window);
            ClickByContent(dialog, "保存せず閉じる");
            await closed;
        }
        finally
        {
            File.SetUnixFileMode(sessionPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        Assert.Equal("preserve original", await File.ReadAllTextAsync(sessionPath, TestContext.Current.CancellationToken));
    }

    /// <summary>検証専用の保存先だけを削除する。</summary>
    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private async Task<WorkspaceWindow> OpenAsync()
    {
        var window = new WorkspaceWindow(directory);
        window.Show();
        await window.GetObservable(InputElement.IsEnabledProperty).Where(enabled => enabled)
            .Take(1).Timeout(OperationTimeout).ToTask();
        return window;
    }

    private Task CloseAsync(Window window)
    {
        var closed = ObserveClosed(window);
        window.Close();
        return closed;
    }

    private Task ObserveClosed(Window window)
    {
        return Observable.FromEventPattern(handler => window.Closed += handler, handler => window.Closed -= handler)
            .Take(1).Timeout(OperationTimeout).ToTask();
    }

    private Task<Window> WaitForDialogAsync(Window window)
    {
        var scheduler = new SynchronizationContextScheduler(new AvaloniaSynchronizationContext());
        return Observable.Interval(DialogObservationInterval, scheduler)
            .Select(_ => window.OwnedWindows.FirstOrDefault()).Where(dialog => dialog is not null)
            .Select(dialog => dialog!).Take(1).Timeout(OperationTimeout).ToTask();
    }

    private void ClickByContent(Window window, string content)
    {
        window.GetLogicalDescendants().OfType<Button>().Single(button => Equals(button.Content, content))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private Task WaitForTextAsync(Window window, string name, string expected)
    {
        return window.FindControl<TextBlock>(name)!.GetObservable(TextBlock.TextProperty)
            .Where(text => text?.Contains(expected, StringComparison.Ordinal) == true)
            .Take(1).Timeout(OperationTimeout).ToTask();
    }

    private TextBox FirstInput(Window window)
    {
        return window.FindControl<StackPanel>("EditorHost")!.GetLogicalDescendants().OfType<TextBox>()
            .First(input => AutomationProperties.GetName(input) == new FieldCatalog().Fields[0].Label);
    }

    private void Click(Window window, string name)
    {
        window.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private WorkspaceSession CreateSession(BriefDocument document)
    {
        return new WorkspaceSession { Brief = document, Executable = "codex", OutputRoot = directory, History = [] };
    }
}
