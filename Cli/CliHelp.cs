namespace GameMockStudio.Cli;

/// <summary>ターミナルで参照できる全コマンドの書式。</summary>
internal static class CliHelp
{
    /// <summary>引数省略時とヘルプ指定時に表示する使用方法。</summary>
    public const string Text = """
        Mosaic CLI
        起動: dotnet run --project GameMockStudio.csproj -- cli <command> [options]
          または dotnet bin/Release/net8.0/GameMockStudio.dll cli <command> [options]

        kinds
        fields --kind <game|service|gamification>
        ideas --kind <game|service|gamification>
        interview start --brief <path> --kind <kind> [--format <browser|unity|avalonia>] [--idea <number>]
        interview next --brief <path> [--category <label>]
        interview answer --brief <path> --field <identifier> (--value <text> | --random | --none)
        interview answer --brief <path> --from-json <file>
        interview genres --brief <path> --set <a,b,c>
        interview finish --brief <path>
        interview status --brief <path>
        interview ask --brief <path>
        prompt --brief <path>
        generate --brief <path> --output <absolute-directory> [--codex <path>] [--json]
        refine --mock <absolute-directory> --feedback-file <path> --output <absolute-directory> [--codex <path>] [--json]

        種類と形式は大文字小文字を区別しません。形式の既定値は browser です。
        空文字・--random はAI補完、--none は「なし」を回答として保存します。
        ask は空行でAI補完、:q またはEOFで中断します。回答ごとに保存します。
        --json は生成進捗をJSONLで出力します。詳細: docs/cli.md
        終了コード: 0 成功 / 1 実行失敗・キャンセル / 2 引数の誤り
        """;
}
