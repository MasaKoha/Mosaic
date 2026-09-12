using System;
using System.Linq;
using System.Text.Json;
using GameMockStudio.Brief;
using GameMockStudio.Brief.Planning;
using GameMockStudio.Generation;
using Xunit;

namespace GameMockStudio.Tests.Brief.Planning;

/// <summary>種類の追加で旧企画を失ったり、対象外の体験を要求したりしないことを守る。</summary>
public sealed class PlanningTests
{
    /// <summary>旧版の企画の明示指定と未知の項目をゲームの下書きへ移行する。</summary>
    [Fact]
    public void LegacyGameMigratesWithoutLosingFields()
    {
        var legacy = JsonSerializer.Deserialize<BriefDocument>("""
            {"Version":1,"Format":1,"Genres":["パズル"],"Values":{"title":"旧企画","future_rule":"維持する"}}
            """)!.Normalize();
        var drafts = new BriefDrafts();
        drafts.Restore([], legacy);
        drafts.Remember(new BriefDocument { Kind = MockKind.Service, Values = new() { ["service_title"] = "別の企画" } });
        var restored = drafts.ForKind(MockKind.Game);
        Assert.Equal(BriefDocument.CurrentVersion, restored.Version);
        Assert.Equal(MockFormat.Unity, restored.Format);
        Assert.Equal(legacy.Genres, restored.Genres);
        Assert.Equal("旧企画", restored.Values["title"]);
        Assert.Equal("維持する", restored.Values["future_rule"]);
    }

    /// <summary>サービスにゲーム形式を混ぜた外部企画や不明な種類を拒否する。</summary>
    [Theory]
    [InlineData("{\"Version\":1,\"Kind\":1}")]
    [InlineData("{\"Kind\":1,\"Format\":1}")]
    [InlineData("{\"Kind\":1,\"Genres\":[\"パズル\"]}")]
    [InlineData("{\"Kind\":99}")]
    public void InvalidKindCombinationsAreRejected(string json)
    {
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Deserialize<BriefDocument>(json)!.Normalize());
    }

    /// <summary>戻り先の下書きを書き換えても、他の種類や保存済みの原本を変更しない。</summary>
    [Fact]
    public void DraftsDoNotAliasMutableFields()
    {
        var drafts = new BriefDrafts();
        drafts.Remember(new BriefDocument { Values = new() { ["title"] = "ゲーム" } });
        drafts.Remember(new BriefDocument { Kind = MockKind.Service, Values = new() { ["service_title"] = "サービス" } });
        var captured = drafts.ForKind(MockKind.Game);
        captured.Values["title"] = "編集中";
        Assert.Equal("ゲーム", drafts.ForKind(MockKind.Game).Values["title"]);
        Assert.Equal("サービス", drafts.ForKind(MockKind.Service).Values["service_title"]);
    }

    /// <summary>サービス生成では勝敗やゲームジャンルを要求せず、利用と検証の一巡を要求する。</summary>
    [Fact]
    public void ServicePromptRequiresServiceFlowAndNoGameLoop()
    {
        var prompt = new PromptComposer(new FieldCatalogs()).Compose(new BriefDocument
        {
            Kind = MockKind.Service, Values = new() { ["service_social"] = "なし" }
        }, "service-test");
        Assert.Contains("入力→操作→結果→編集・取消", prompt);
        Assert.Contains("experiment.md", prompt);
        Assert.Contains("Kind=1", prompt);
        Assert.Contains("service_social /", prompt);
        Assert.DoesNotContain("win_condition /", prompt);
        Assert.DoesNotContain("ジャンルは最大3種類", prompt);
        Assert.DoesNotContain("操作・目的・成功/失敗・再挑戦", prompt);
    }

    /// <summary>ゲーミフィケーションはゲームと現実の行動を接続し、比較できるモックを要求する。</summary>
    [Fact]
    public void GamificationPromptIncludesGameAndBehaviorComparison()
    {
        var catalogs = new FieldCatalogs();
        var prompt = new PromptComposer(catalogs).ComposeRefinement(MockKind.Gamification, MockFormat.Browser,
            "通常モードの入力を短くしたい", "refinement-test");
        Assert.Contains("同じ中心行動をゲームなしでも行える通常モード", prompt);
        Assert.Contains("gamification_mapping /", prompt);
        Assert.Contains("win_condition /", prompt);
        Assert.Contains("元のKindとFormatを維持", prompt);
        Assert.Contains("Kind=2", prompt);
        Assert.True(catalogs.ForKind(MockKind.Game).Fields.All(gameField =>
            catalogs.ForKind(MockKind.Gamification).Fields.Contains(gameField)));
    }
}
