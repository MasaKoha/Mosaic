namespace GameMockStudio.Workspace;

/// <summary>利用者が要求したワークスペース操作。</summary>
public enum WorkspaceAction
{
    /// <summary>新しい企画を作成する。</summary>
    New,
    /// <summary>企画を読み込む。</summary>
    Load,
    /// <summary>企画を保存する。</summary>
    Save,
    /// <summary>出力先を選ぶ。</summary>
    ChooseOutput,
    /// <summary>生成を始める。</summary>
    Generate,
    /// <summary>生成を止める。</summary>
    Cancel,
    /// <summary>成果物フォルダを開く。</summary>
    OpenOutput,
    /// <summary>生成された補完企画を編集へ戻す。</summary>
    UseResolved,
    /// <summary>自動退避した企画の保存先を開く。</summary>
    OpenRecovery,
    /// <summary>ブラウザゲームを開く。</summary>
    Play
}
