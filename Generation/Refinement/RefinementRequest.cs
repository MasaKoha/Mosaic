using System;
using System.IO;

namespace GameMockStudio.Generation.Refinement;

/// <summary>元のゲームを残して改善版を作るための入力。</summary>
public sealed record RefinementRequest
{
    /// <summary>保存と生成指示へ渡せる感想の文字数上限。</summary>
    public const int MaximumFeedbackCharacters = 10000;

    /// <summary>改善の起点にする完成済みゲームの絶対パス。</summary>
    public required string SourceDirectory { get; init; }
    /// <summary>今回反映する感想と改善要望。</summary>
    public required string Feedback { get; init; }

    /// <summary>コピーやCLI起動より前に利用者の入力を検証する。</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SourceDirectory) || !Path.IsPathFullyQualified(SourceDirectory))
        {
            throw new InvalidOperationException("改善するゲームの成果物フォルダが不正です。");
        }
        if (string.IsNullOrWhiteSpace(Feedback) || Feedback.Length > MaximumFeedbackCharacters)
        {
            throw new InvalidOperationException($"感想・改善要望を1〜{MaximumFeedbackCharacters}文字で入力してください。");
        }
    }
}
