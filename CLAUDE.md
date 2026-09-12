# CLAUDE.md — Mosaic

Game Mock Studioのソースリポジトリ。.NET 8 / C# 12 / Avalonia 12 / Rx.NET。構成と使い方は [README.md](README.md)、実装上の境界は [docs/STATUS.md](docs/STATUS.md)、検証結果は [docs/verification.md](docs/verification.md) を参照する。

## 規約の入口

メンテナー環境では `~/.claude/rules/coding-principles.md`、git操作では `~/.claude/rules/git-workflow.md` を読む。規約の本文はこのリポジトリへ複製しない。Unity向けの生成コードを変更する場合は `unity-csharp-standards` と `~/.claude/rules/unity-csharp.md` も参照する。

## プロジェクト固有の注意

- 画面・Presenter・企画・CLI実行・保存の責務は [README.mdの構成](README.md#構成) に従う。
- `Brief/field-catalog.json` が画面と生成指示の共通定義。外部JSONの未知キーは破棄しない。
- 生成の完了判定には必須成果物・明示指定の維持・完了レポートが必要。プレイテスト済みとは表示しない。
- モデルは `gpt-6-astra` / `xhigh` 固定。既存CLI認証を使用し、認証情報をアプリへ複製しない。
- `Avalonia.Headless.XUnit` 12.1.2はxunit v3に依存する。xunit v2を混在させない。
- `App` と `WorkspaceWindow` のpartialはAvaloniaの自動生成との連携に必要。
- 生成出力・診断ログ・セッション・配布用アプリはソースへ含めない。
- 個人のCLI設定を読み込まない起動と、シェルの権限・環境変数制限を維持する。公開時の注意は [SECURITY.md](SECURITY.md) を参照する。

## 検証とブランチ

`dotnet test tests/GameMockStudio.Tests.csproj --configuration Release --nologo` を実行する。実モデル生成や実画面の確認は自動テストと区別して記録する。

既定ブランチは保護された `develop`。作業ブランチからPRを作成し、検証後にsquash mergeする。
