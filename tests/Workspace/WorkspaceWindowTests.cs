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
using GameMockStudio.Brief.Planning;
using GameMockStudio.Generation.History;
using GameMockStudio.Generation;
using GameMockStudio.Tests.Generation.Fixtures;
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

    /// <summary>古いゲームの感想編集で対象が最新へ飛ばず、切替・即時終了後も下書きを取り違えない。</summary>
    [AvaloniaFact]
    public async Task FeedbackDraftsFollowSelectedGameAcrossAutosaveAndRestart()
    {
        using var store = new WorkspaceSessionStore(directory);
        var entries = new[] { "latest", "older" }.Select((name, position) => new GenerationHistoryEntry
        {
            StartedAt = DateTimeOffset.Now.AddMinutes(-position), OutputDirectory = Path.Combine(directory, name),
            Format = MockFormat.Browser, Outcome = GenerationOutcome.Completed, Feedback = name + "の感想"
        }).ToArray();
        foreach (var entry in entries)
        {
            Directory.CreateDirectory(entry.OutputDirectory);
            await new BriefStore().SaveAsync(Path.Combine(entry.OutputDirectory, "resolved-brief.json"), new BriefDocument(), CancellationToken.None);
        }
        await store.SaveAsync(CreateSession(new BriefDocument()) with { History = entries }, CancellationToken.None);
        var window = await OpenAsync();
        window.FindControl<TabControl>("EditorTabs")!.SelectedIndex = 1;
        var history = window.FindControl<ComboBox>("HistorySelector")!;
        var feedback = window.FindControl<TextBox>("FeedbackBox")!;
        history.SelectedIndex = 1;
        Assert.Equal("olderの感想", feedback.Text);
        feedback.Text = "古いゲームの操作は好き。難易度を下げたい。";
        await WaitForTextAsync(window, "SaveStatusText", "自動保存済み");
        Assert.Equal(1, history.SelectedIndex);
        history.SelectedIndex = 0;
        Assert.Equal("latestの感想", feedback.Text);
        feedback.Text = " ";
        Assert.False(window.FindControl<Button>("RefineButton")!.IsEnabled);
        feedback.Text = "新しいゲームの感想\n終了直前の入力";
        Assert.True(window.FindControl<Button>("RefineButton")!.IsEnabled);
        Assert.False(window.FindControl<Button>("GenerateButton")!.IsVisible);
        await CloseAsync(window);
        var reopened = await OpenAsync();
        Assert.Equal("新しいゲームの感想\n終了直前の入力", reopened.FindControl<TextBox>("FeedbackBox")!.Text);
        reopened.FindControl<ComboBox>("HistorySelector")!.SelectedIndex = 1;
        Assert.Equal("古いゲームの操作は好き。難易度を下げたい。", reopened.FindControl<TextBox>("FeedbackBox")!.Text);
        await CloseAsync(reopened);
    }

    /// <summary>改善は編集中の別企画を維持し、元ゲームの形式と感想を使って完了履歴へ遷移する。</summary>
    [AvaloniaTheory]
    [InlineData(MockKind.Game)]
    [InlineData(MockKind.Service)]
    [InlineData(MockKind.Gamification)]
    public async Task RefinementUsesSelectedKindAndKeepsPlanningDraft(MockKind kind)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("偽CLIはPOSIXシェルを使用します。");
            return;
        }
        using var fake = new FakeCodexProcess("""
            sleep 1
            cp baseline-README.md README.md
            cp baseline-decisions.md decisions.md
            cp baseline-resolved-brief.json resolved-brief.json
            cp baseline-generation-report.json generation-report.json
            if [ -f baseline-experiment.md ]; then cp baseline-experiment.md experiment.md; fi
            printf '<html>improved</html>' > index.html
            printf '{"type":"turn.completed"}\n'
            """, kind: kind);
        var source = Path.Combine(fake.Request.OutputRoot, "original");
        Directory.CreateDirectory(source);
        fake.PrepareArtifacts(new GenerationUpdate { Stage = GenerationStage.Prepared, OutputDirectory = source, Message = "" });
        var firstIdentifier = new FieldCatalog().Fields[0].Identifier;
        using var store = new WorkspaceSessionStore(directory);
        await store.SaveAsync(CreateSession(new BriefDocument
        {
            Format = MockFormat.Unity, Values = new() { [firstIdentifier] = "別の企画の下書き" }
        }) with
        {
            Executable = fake.Request.Executable,
            History = [new GenerationHistoryEntry
            {
                StartedAt = DateTimeOffset.Now, OutputDirectory = source,
                Kind = kind, Format = MockFormat.Browser, Outcome = GenerationOutcome.Completed
            }]
        }, CancellationToken.None);
        var window = await OpenAsync();
        window.FindControl<TabControl>("EditorTabs")!.SelectedIndex = 1;
        window.FindControl<TextBox>("FeedbackBox")!.Text = "ルールを保って操作感を改善してほしい。";
        Assert.False(window.FindControl<ComboBox>("FormatSelector")!.IsEnabled);
        Click(window, "RefineButton");
        await WaitForTextAsync(window, "StatusText", "生成中");
        Assert.False(window.FindControl<TextBox>("FeedbackBox")!.IsEnabled);
        Assert.False(window.FindControl<ComboBox>("HistorySelector")!.IsEnabled);
        Assert.Contains("ルールを保って操作感を改善してほしい。", window.FindControl<TextBox>("PromptBox")!.Text);
        await WaitForTextAsync(window, "StatusText", "生成完了。");
        Assert.Equal("別の企画の下書き", FirstInput(window).Text);
        Assert.Equal((int)MockFormat.Unity, window.FindControl<ComboBox>("FormatSelector")!.SelectedIndex);
        Assert.False(window.FindControl<Button>("RefineButton")!.IsEnabled);
        Assert.True(window.FindControl<Button>("PlayButton")!.IsEnabled);
        await CloseAsync(window);
        var saved = (await store.LoadAsync(CancellationToken.None))!;
        Assert.Equal(2, saved.History.Length);
        Assert.Equal(GenerationOutcome.Completed, saved.History[0].Outcome);
        Assert.Equal(MockFormat.Browser, saved.History[0].Format);
        Assert.Equal(kind, saved.History[0].Kind);
        Assert.Equal(source, saved.History[0].SourceDirectory);
        Assert.Equal(saved.History[1].Feedback, saved.History[0].AppliedFeedback);
        Assert.Contains("ルールを保って", saved.History[0].AppliedFeedback);
        Assert.Contains($"既存の{new FieldCatalog(kind).Label}モック", await File.ReadAllTextAsync(Path.Combine(saved.History[0].OutputDirectory, "observed-prompt.txt")));
    }

    /// <summary>改善が失敗しても元の感想へ戻って再編集でき、失敗版からは改善させない。</summary>
    [AvaloniaFact]
    public async Task FailedRefinementKeepsOriginalFeedbackAvailable()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("偽CLIはPOSIXシェルを使用します。");
            return;
        }
        using var fake = new FakeCodexProcess("exit 9");
        var source = Path.Combine(fake.Request.OutputRoot, "original");
        Directory.CreateDirectory(source);
        fake.PrepareArtifacts(new GenerationUpdate { Stage = GenerationStage.Prepared, OutputDirectory = source, Message = "" });
        using var store = new WorkspaceSessionStore(directory);
        await store.SaveAsync(CreateSession(new BriefDocument()) with
        {
            Executable = fake.Request.Executable,
            History = [new GenerationHistoryEntry
            {
                StartedAt = DateTimeOffset.Now, OutputDirectory = source,
                Format = MockFormat.Browser, Outcome = GenerationOutcome.Completed, Feedback = "残しておく改善要望"
            }]
        }, CancellationToken.None);
        var window = await OpenAsync();
        window.FindControl<TabControl>("EditorTabs")!.SelectedIndex = 1;
        Click(window, "RefineButton");
        await WaitForTextAsync(window, "StatusText", "終了コード 9");
        Assert.False(window.FindControl<Button>("RefineButton")!.IsEnabled);
        Assert.False(window.FindControl<Button>("CancelButton")!.IsVisible);
        window.FindControl<ComboBox>("HistorySelector")!.SelectedIndex = 1;
        Assert.Equal("残しておく改善要望", window.FindControl<TextBox>("FeedbackBox")!.Text);
        Assert.True(window.FindControl<Button>("RefineButton")!.IsEnabled);
        await CloseAsync(window);
        Assert.Equal(GenerationOutcome.Failed, (await store.LoadAsync(CancellationToken.None))!.History[0].Outcome);
    }

    /// <summary>種類を往復しても下書きと形式が混ざらず、終了直前の入力まで復元する。</summary>
    [AvaloniaFact]
    public async Task KindSwitchingPreservesIndependentDraftsAcrossRestart()
    {
        var window = await OpenAsync();
        FirstInput(window).Text = "元のゲーム";
        window.FindControl<ComboBox>("FormatSelector")!.SelectedIndex = (int)MockFormat.Unity;
        await ChangeKindAsync(window, MockKind.Service);
        Assert.False(window.FindControl<ComboBoxItem>("UnityFormatItem")!.IsEnabled);
        Assert.Equal((int)MockFormat.Browser, window.FindControl<ComboBox>("FormatSelector")!.SelectedIndex);
        InputByLabel(window, "サービス名").Text = "生活の道具";
        await ChangeKindAsync(window, MockKind.Gamification);
        Assert.True(window.FindControl<ComboBoxItem>("UnityFormatItem")!.IsEnabled);
        InputByLabel(window, "掛け合わせる領域").Text = "片付け";
        await ChangeKindAsync(window, MockKind.Game);
        Assert.Equal("元のゲーム", FirstInput(window).Text);
        Assert.Equal((int)MockFormat.Unity, window.FindControl<ComboBox>("FormatSelector")!.SelectedIndex);
        await CloseAsync(window);

        var reopened = await OpenAsync();
        await ChangeKindAsync(reopened, MockKind.Service);
        Assert.Equal("生活の道具", InputByLabel(reopened, "サービス名").Text);
        await ChangeKindAsync(reopened, MockKind.Gamification);
        Assert.Equal("片付け", InputByLabel(reopened, "掛け合わせる領域").Text);
        InputByLabel(reopened, "対象とする具体的な場面").Text = "終了直前の追記";
        await CloseAsync(reopened);
        using var store = new WorkspaceSessionStore(directory);
        var saved = (await store.LoadAsync(CancellationToken.None))!;
        Assert.Equal(3, saved.Drafts.Length);
        Assert.Equal("終了直前の追記", saved.Brief.Values["gamification_moment"]);
        Assert.DoesNotContain("service_title", saved.Drafts.Single(brief => brief.Kind == MockKind.Game).Values.Keys);
    }

    /// <summary>案の採用前に現在の入力を退避し、選んだ案を種類に合う編集画面へ反映する。</summary>
    [AvaloniaTheory]
    [InlineData(MockKind.Service, "サービス名", "service_title")]
    [InlineData(MockKind.Gamification, "掛け合わせる領域", "gamification_domain")]
    public async Task AdoptingIdeaBacksUpDraftAndOpensEditablePlan(MockKind kind, string label, string identifier)
    {
        var window = await OpenAsync();
        await ChangeKindAsync(window, kind);
        InputByLabel(window, label).Text = "置き換え前の入力";
        var status = window.FindControl<TextBlock>("StatusText")!;
        status.Text = string.Empty;
        Click(window, "ApplyIdeaButton");
        await WaitForTextAsync(window, "StatusText", "企画を切り替えました");
        Assert.Equal(0, window.FindControl<TabControl>("EditorTabs")!.SelectedIndex);
        Assert.NotEqual("置き換え前の入力", InputByLabel(window, label).Text);
        Assert.False(string.IsNullOrWhiteSpace(InputByLabel(window, label).Text));
        Assert.Equal((int)kind, window.FindControl<ComboBox>("KindSelector")!.SelectedIndex);
        var backups = Directory.GetFiles(Path.Combine(directory, "Recovery"), "brief-*.json");
        var documents = await Task.WhenAll(backups.Select(path => new BriefStore().LoadAsync(path, CancellationToken.None)));
        Assert.Contains(documents, document => document.Kind == kind
            && document.Values.GetValueOrDefault(identifier) == "置き換え前の入力");
        await CloseAsync(window);
    }

    /// <summary>別種類の補完企画を開くとき、現在企画と置換先の以前の下書きの両方を救出できる。</summary>
    [AvaloniaFact]
    public async Task ImportingOtherKindPreservesBothPreviousDraftsInRecovery()
    {
        var source = Path.Combine(directory, "completed-service");
        Directory.CreateDirectory(source);
        await new BriefStore().SaveAsync(Path.Combine(source, "resolved-brief.json"), new BriefDocument
        {
            Kind = MockKind.Service, Values = new() { ["service_title"] = "取り込むサービス" }
        }, CancellationToken.None);
        using var store = new WorkspaceSessionStore(directory);
        await store.SaveAsync(CreateSession(new BriefDocument { Values = new() { ["title"] = "編集中のゲーム" } }) with
        {
            Drafts = [new BriefDocument { Kind = MockKind.Service, Values = new() { ["service_title"] = "以前のサービス" } }],
            History = [new GenerationHistoryEntry
            {
                StartedAt = DateTimeOffset.Now, OutputDirectory = source,
                Kind = MockKind.Service, Format = MockFormat.Browser, Outcome = GenerationOutcome.Completed
            }]
        }, CancellationToken.None);
        var window = await OpenAsync();
        Click(window, "UseResolvedButton");
        await WaitForTextAsync(window, "StatusText", "企画を切り替えました");
        Assert.Equal("取り込むサービス", InputByLabel(window, "サービス名").Text);
        var paths = Directory.GetFiles(Path.Combine(directory, "Recovery"), "brief-*.json");
        var backups = await Task.WhenAll(paths.Select(path => new BriefStore().LoadAsync(path, CancellationToken.None)));
        Assert.Contains(backups, brief => brief.Values.GetValueOrDefault("title") == "編集中のゲーム");
        Assert.Contains(backups, brief => brief.Values.GetValueOrDefault("service_title") == "以前のサービス");
        await ChangeKindAsync(window, MockKind.Game);
        Assert.Equal("編集中のゲーム", FirstInput(window).Text);
        await CloseAsync(window);
    }

    private async Task ChangeKindAsync(Window window, MockKind kind)
    {
        window.FindControl<TextBlock>("StatusText")!.Text = string.Empty;
        window.FindControl<ComboBox>("KindSelector")!.SelectedIndex = (int)kind;
        await WaitForTextAsync(window, "StatusText", "企画を切り替えました");
    }

    private TextBox InputByLabel(Window window, string label)
    {
        return window.FindControl<StackPanel>("EditorHost")!.GetLogicalDescendants().OfType<TextBox>()
            .Single(input => AutomationProperties.GetName(input) == label);
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
