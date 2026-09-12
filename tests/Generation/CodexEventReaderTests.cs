using GameMockStudio.Generation;
using Xunit;

namespace GameMockStudio.Tests.Generation;

/// <summary>CLI出力の失敗と未知のイベントを取り違えないことを検証する。</summary>
public sealed class CodexEventReaderTests
{
    /// <summary>完了イベントが後から届いても先行する失敗を消さない。</summary>
    [Fact]
    public void FailureSurvivesLaterCompletedEvent()
    {
        var reader = new CodexEventReader();
        var failure = "{\"type\":\"turn.failed\",\"error\":{\"message\":\"usage limit\"}}";
        reader.Read(failure);
        reader.Read("{\"type\":\"turn.completed\"}");
        Assert.True(reader.TurnCompleted);
        Assert.Equal(failure, reader.Failure);
    }

    /// <summary>未知・非JSON・オブジェクト以外の行でログ読み取りを停止しない。</summary>
    [Theory]
    [InlineData("CLI warning")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"type\":\"future.event\"}")]
    [InlineData("{\"type\":[]}")]
    [InlineData("{\"type\":\"item.completed\",\"item\":null}")]
    [InlineData("{\"type\":\"item.completed\",\"item\":{\"text\":[],\"command\":42}}")]
    public void UnknownOutputDoesNotBecomeSuccess(string line)
    {
        var reader = new CodexEventReader();
        reader.Read(line);
        Assert.False(reader.TurnCompleted);
        Assert.Empty(reader.Failure);
    }

    /// <summary>後続の派生エラーより先に起きた失敗原因を維持する。</summary>
    [Fact]
    public void FirstFailureSurvivesSubsequentErrors()
    {
        var reader = new CodexEventReader();
        const string firstFailure = """{"type":"error","message":"usage limit"}""";

        reader.Read(firstFailure);
        reader.Read("""{"type":"turn.failed","error":{"message":"stream closed"}}""");

        Assert.Equal(firstFailure, reader.Failure);
    }
}
