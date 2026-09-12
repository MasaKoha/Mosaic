using System;
using System.Text.Json;
using GameMockStudio.Brief;
using Xunit;

namespace GameMockStudio.Tests.Brief;

/// <summary>外部企画ファイルとランダム指定の境界を検証する。</summary>
public sealed class BriefDocumentTests
{
    /// <summary>空白はランダムとして残し、不採用の指定は維持する。</summary>
    [Fact]
    public void NormalizePreservesRandomAndExplicitExclusion()
    {
        var document = new BriefDocument
        {
            Genres = ["  RPG ", "rpg", " ", "パズル"],
            Values = new() { ["world_theme"] = " \n ", ["combat_style"] = " なし " }
        }.Normalize();
        Assert.Equal(new[] { "RPG", "パズル" }, document.Genres);
        Assert.Equal(string.Empty, document.Values["world_theme"]);
        Assert.Equal("なし", document.Values["combat_style"]);
    }

    /// <summary>画面外から上限超過の企画を持ち込んでも拒否する。</summary>
    [Fact]
    public void NormalizeRejectsMoreThanThreeDistinctGenres()
    {
        var document = new BriefDocument { Genres = ["RPG", "パズル", "アクション", "レース"] };
        Assert.Throws<InvalidOperationException>(() => document.Normalize());
    }

    /// <summary>JSONのnullや将来バージョンを有効な企画と誤認しない。</summary>
    [Theory]
    [InlineData("{\"Genres\":null}")]
    [InlineData("{\"Values\":null}")]
    [InlineData("{\"Version\":99}")]
    [InlineData("{\"Format\":99}")]
    public void NormalizeRejectsInvalidExternalData(string json)
    {
        var document = JsonSerializer.Deserialize<BriefDocument>(json)!;
        Assert.Throws<InvalidOperationException>(() => document.Normalize());
    }
}
