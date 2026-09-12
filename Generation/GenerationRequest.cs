using System;
using GameMockStudio.Brief;
using GameMockStudio.Generation.Refinement;

namespace GameMockStudio.Generation;

/// <summary>一回の生成で固定する入力と実行条件。</summary>
public sealed record GenerationRequest
{
    /// <summary>応答が止まった生成を終了する既定の実行時間上限。</summary>
    public static readonly TimeSpan DefaultExecutionTimeout = TimeSpan.FromMinutes(30);

    /// <summary>利用者が指定した企画。</summary>
    public required BriefDocument Brief { get; init; }
    /// <summary>確認画面と一致する生成指示。</summary>
    public required string Prompt { get; init; }
    /// <summary>Codexの実行ファイル名または絶対パス。</summary>
    public required string Executable { get; init; }
    /// <summary>生成ごとの子フォルダを作る親ディレクトリ。</summary>
    public required string OutputRoot { get; init; }
    /// <summary>既存モックの改善時にだけ指定する起点と感想。</summary>
    public RefinementRequest? Refinement { get; init; }
    /// <summary>CLIの起動から終了までを許容する時間。0以下や無期限は指定できない。</summary>
    public TimeSpan ExecutionTimeout { get; init; } = DefaultExecutionTimeout;
}
