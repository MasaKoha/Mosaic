using System;
using System.Collections.Generic;
using System.Linq;
using GameMockStudio.Brief;
using GameMockStudio.Brief.Planning;
using GameMockStudio.Cli.Interview;
using Xunit;

namespace GameMockStudio.Tests.Cli.Interview;

/// <summary>回答状態の区別、カタログ順、未知キーの保持を守る。</summary>
public sealed class InterviewStateTests
{
    private readonly FieldCatalogs catalogs = new();

    /// <summary>飛び飛びに回答しても、各種類のカタログで最初の未回答へ戻る。</summary>
    [Theory]
    [InlineData(MockKind.Game)]
    [InlineData(MockKind.Service)]
    [InlineData(MockKind.Gamification)]
    public void NextUsesCatalogOrder(MockKind kind)
    {
        var fields = catalogs.ForKind(kind).Fields;
        var brief = new BriefDocument
        {
            Kind = kind,
            Values = new() { [fields[2].Identifier] = "先に回答", [fields[0].Identifier] = "回答" }
        };
        var question = new InterviewState(catalogs).FindNext(brief);
        Assert.Equal(fields[1], question!.Field);
        Assert.Equal(catalogs.ForKind(kind).Categories[0], question.Category);
    }

    /// <summary>指定したカテゴリだけを探索し、その範囲の完了を全体と区別する。</summary>
    [Fact]
    public void CategoryLimitsQuestionsAndRejectsUnknownLabels()
    {
        var state = new InterviewState(catalogs);
        var category = catalogs.ForKind(MockKind.Service).Categories[1];
        var brief = new BriefDocument { Kind = MockKind.Service };
        Assert.Equal(category.Fields[0], state.FindNext(brief, category.Label)!.Field);
        var answers = category.Fields.ToDictionary(field => field.Identifier, _ => string.Empty);
        var answered = state.ApplyAnswers(brief, answers);
        Assert.Null(state.FindNext(answered, category.Label));
        Assert.NotNull(state.FindNext(answered));
        Assert.Equal(category.Fields.Count, state.GetProgress(answered, category.Label).Answered);
        Assert.Throws<InvalidOperationException>(() => state.FindNext(brief, "存在しないカテゴリ"));
    }

    /// <summary>空文字と空白も回答済みとし、指定数には不採用だけを含める。</summary>
    [Theory]
    [InlineData("", 0)]
    [InlineData(" \t", 0)]
    [InlineData("なし", 1)]
    public void EmptyAnswersAdvanceWithoutCountingAsSpecified(string value, int specified)
    {
        var fields = catalogs.ForKind(MockKind.Service).Fields;
        var brief = new BriefDocument
        {
            Kind = MockKind.Service,
            Values = new() { [fields[0].Identifier] = value, ["future_rule"] = "未知の既存値" }
        };
        var state = new InterviewState(catalogs);
        var progress = state.GetProgress(brief);
        Assert.Equal(fields[1], state.FindNext(brief)!.Field);
        Assert.Equal(1, progress.Answered);
        Assert.Equal(specified, progress.Specified);
        Assert.Equal(fields.Count, progress.Total);
    }

    /// <summary>1項目の更新で原本や未知の既存値を失わない。</summary>
    [Fact]
    public void AnswersPreserveExistingKeysWithoutMutatingOriginal()
    {
        var original = new BriefDocument
        {
            Kind = MockKind.Service,
            Values = new() { ["service_title"] = "旧名", ["service_type"] = "既存の種類", ["future_rule"] = "保持" }
        };
        var changed = new InterviewState(catalogs).ApplyAnswers(original,
            new Dictionary<string, string> { ["service_title"] = "新名" });
        Assert.Equal("新名", changed.Values["service_title"]);
        Assert.Equal("既存の種類", changed.Values["service_type"]);
        Assert.Equal("保持", changed.Values["future_rule"]);
        Assert.Equal("旧名", original.Values["service_title"]);
    }

    /// <summary>一括回答の途中に未知IDがあっても原本へ一部だけ適用しない。</summary>
    [Theory]
    [InlineData("future_rule")]
    [InlineData("title")]
    public void UnknownOrOtherKindIdentifiersRejectEntireAnswer(string identifier)
    {
        var brief = new BriefDocument { Kind = MockKind.Service, Values = new() { ["service_title"] = "原本" } };
        var answers = new Dictionary<string, string> { ["service_title"] = "書き換え", [identifier] = "拒否" };
        Assert.Throws<InvalidOperationException>(() => new InterviewState(catalogs).ApplyAnswers(brief, answers));
        Assert.Equal("原本", brief.Values["service_title"]);
        Assert.Single(brief.Values);
    }

    /// <summary>未回答だけを埋めて完了し、空ジャンルや未知キーで進捗を誤判定しない。</summary>
    [Fact]
    public void FinishFillsMissingFieldsAndPreservesAnswers()
    {
        var state = new InterviewState(catalogs);
        var original = new BriefDocument { Values = new() { ["title"] = "指定", ["future_rule"] = "保持" } };
        var finished = state.Finish(original);
        Assert.All(catalogs.ForKind(MockKind.Game).Fields, field => Assert.True(finished.Values.ContainsKey(field.Identifier)));
        Assert.Equal("指定", finished.Values["title"]);
        Assert.Equal("保持", finished.Values["future_rule"]);
        Assert.Equal(string.Empty, finished.Values["pitch"]);
        Assert.Null(state.FindNext(finished));
        Assert.Empty(finished.Genres);
        Assert.Equal(1, state.GetProgress(finished).Specified);
        Assert.Equal(2, original.Values.Count);
    }
}
