using System;

namespace GameMockStudio.Workspace;

/// <summary>選択した成果物に対して利用できる操作。</summary>
[Flags]
public enum OutputAvailability
{
    /// <summary>利用できる成果物がない。</summary>
    None = 0,
    /// <summary>成果物フォルダを開ける。</summary>
    OpenDirectory = 1,
    /// <summary>生成したブラウザゲームを開ける。</summary>
    Play = 2,
    /// <summary>補完済み企画を編集へ戻せる。</summary>
    UseResolved = 4
}
