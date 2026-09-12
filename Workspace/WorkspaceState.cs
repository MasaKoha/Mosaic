namespace GameMockStudio.Workspace;

/// <summary>生成操作を多重実行しないための画面状態。</summary>
public enum WorkspaceState
{
    /// <summary>企画を編集できる状態。</summary>
    Editing,
    /// <summary>ゲームモックを生成している状態。</summary>
    Generating,
    /// <summary>自動保存を完了して終了を待つ状態。</summary>
    Closing
}
