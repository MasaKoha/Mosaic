# Game Mock Studio — 作業再開メモ

更新: 2026-09-12

公開リポジトリ: [MasaKoha/Mosaic](https://github.com/MasaKoha/Mosaic)。作業ディレクトリはこのファイルのあるリポジトリルート。

## まず読むもの

1. [README.md](README.md) — 起動と使い方
2. [docs/STATUS.md](docs/STATUS.md) — 洗い出した課題・実装状況・境界
3. [docs/verification.md](docs/verification.md) — 検証結果と受け入れ手順
4. [CLAUDE.md](CLAUDE.md) — 開発規約の入口とプロジェクト固有の注意

Unityの生成コードを変更する場合はunity-csharp.md、Codex連携を変更する場合はOpenAI Docsも参照する。

## 維持する仕様

- Avaloniaのデスクトップアプリ。MVP + Rx.NET + NRT、日本語のコメントとpublic summary。
- 18カテゴリ・163項目。ジャンルは最大3種類。空欄はAI補完、「なし」は不採用指定。
- 生成形式はブラウザ・Unity・Avalonia。初期値はブラウザ。
- Codex CLIのgpt-6-astra / highを使用。モデル変更・自動再試行・Banked resetは行わない。
- 生成物は毎回別フォルダ。明示指定と完了レポートを検査する。ゲームの動作確認済みとは表示しない。
- AppとWorkspaceWindowのpartialはAvaloniaの自動生成連携のための例外。

## 依頼元の扱い

直接依頼では、必要なビルド・テスト・起動を実施する。過去メモに記載されていた一律のコンパイル禁止は適用しない。Claude Codeからの委譲時だけ、ユーザーのAGENTS.mdにある操作制限を適用する。

## 確認コマンド

```sh
dotnet test tests/GameMockStudio.Tests.csproj
dotnet run --project GameMockStudio.csproj
sh tools/package-macos.sh
```

UIテストはAvalonia.Headless.XUnit 12.1.2がxunit.v3へ依存するため、テスト側をxunit.v3 3.2.2に統一している。xunit v2を混在させない。
