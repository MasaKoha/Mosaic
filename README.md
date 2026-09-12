# Game Mock Studio

必要な企画だけ指定し、未入力をAIによるランダム決定としてゲームモックを生成するAvaloniaデスクトップアプリ。

公開リポジトリ: [MasaKoha/Mosaic](https://github.com/MasaKoha/Mosaic)。アプリ名はGame Mock Studioです。

- 18カテゴリ・163項目＋ジャンル最大3種。全項目に説明と記入例。
- カテゴリ切り替え、全項目検索、入力済み件数、ジャンル候補と自由入力。
- 企画JSONの保存・読込、自動保存と再起動時の復元。新規作成・読込時は直前の入力をRecoveryへ退避。
- 最近20件の生成履歴、補完済み企画の取り込み、接続設定・出力先の永続化。
- 生成指示のプレビュー、Codexの進行ログ、キャンセル、成果物フォルダを開く。
- ブラウザゲーム / Unityプロジェクト / Avaloniaプロジェクトを生成。初期選択はブラウザ。
- ローカルCodex CLIの `gpt-6-astra` / `model_reasoning_effort="xhigh"` を固定指定。

## 起動

.NET 8以降のSDK、Codex CLI 0.153.2以降、Codex CLIへのログインが必要です。GUIアプリとして起動した場合にCLIが見つからなければ、「Codexの接続設定」で実行ファイルの絶対パスを指定します。

利用者が実行するコマンド:

```sh
git clone https://github.com/MasaKoha/Mosaic.git
cd Mosaic
dotnet run --project GameMockStudio.csproj
```

モデル実行はローカルCLI経由でOpenAIへ接続します。オフライン推論ではありません。ログイン情報はCLIの保存済み認証を使用し、アプリは認証ファイルを読み込みません。APIキーの入力欄はありません。

事前にターミナルで `codex login status` を実行し、ChatGPTでログイン済みであることを確認してください。未ログインなら `codex login` でログインします。アプリは `--ignore-user-config` を指定し、CLIの保存済み認証を使いながら、個人の `config.toml` にある接続先・MCP・追加書き込み先の設定を読み込まずに起動します。

実装状況は [STATUS](docs/STATUS.md)、確認結果は [検証記録](docs/verification.md) を参照してください。

macOSでは `sh tools/package-macos.sh` で `artifacts/Game Mock Studio.app` を作成できます。このアプリには.NETランタイムを同梱します。Codex CLIへのログインは別途必要です。

## 入力の扱い

|入力|動作|
|---|---|
|空欄、空白のみ|今回のゲームに整合する内容をAIがランダムに決定|
|「なし」「不要」|明示的な不採用。AIに勝手に追加させない|
|具体的な指定|生成結果でも指定を維持|
|ジャンル未選択|AIが1〜3種類を決定|
|ジャンル1〜3種類を指定|その集合を維持し、勝手に追加しない|

ランダムは各候補の均等抽選ではなく、AIによる整合性を考慮した企画補完です。同じ入力でも再生成は別案になります。バリエーション識別子は厳密な乱数シードではありません。空欄の機能は採否も含めて決定し、必要のない戦闘や経済などは「なし」にできます。

生成形式・実行ファイル・出力先は実行設定なのでランダムにしません。

全項目は [入力項目一覧](docs/input-catalog.md) にあります。作品固有の項目は「追加の企画項目・特徴」に項目名と値を書きます。元の定義は `Brief/field-catalog.json` で、画面とプロンプトで共有します。

## 生成物

出力先の下に毎回異なる `mock-日時-識別子` フォルダを作ります。

|ファイル|用途|
|---|---|
|request.json|入力時点の企画。空欄を保持|
|prompt.md|実際にCodexに渡した指示|
|events.jsonl|Codexの標準出力イベント全文|
|diagnostics.log|Codexの診断ログ全文|
|result.md|Codexの最終応答|
|resolved-brief.json|全項目についてAIが採用・不採用を決定した企画|
|decisions.md|補完した理由とモックに含めた範囲|
|generation-report.json|実装の完了状態、要約、阻害事項|
|README.md|ゲームの起動手順、操作、実装範囲、未確認事項|
|index.html|ブラウザ形式のゲーム本体|
|Assets/、Packages/、ProjectSettings/|Unity形式のソース|
|GameMock/GameMock.csproj|Avalonia形式のゲームプロジェクト|

「最近の生成・復旧」で履歴を選び「補完済み企画で編集する」を押すか、`resolved-brief.json` を「企画を開く」で読み込むと、AIが決めた項目を固定して編集・再生成できます。別案が欲しい部分だけ空欄へ戻してください。

「生成完了」は終了コード、`turn.completed`、空でない必須ファイル、全項目の補完、明示指定の維持、完了レポートのStatus=completedと阻害事項なしを確認した状態です。プレイテスト・コンパイルの合格を意味しません。ブラウザ形式は「ゲームを開く」から既定ブラウザで開けます。Unity/Avalonia形式はREADMEの手順に従って利用者が起動します。

## 保存と復旧

「企画を保存」で任意のJSONファイルに保存します。新規企画・読込では直前の入力をOSのローカルアプリデータ領域内の `GameMockStudio/Recovery` へ退避し、画面に場所を表示します。OSごとのパスを推測せず、画面に表示された退避先を使用してください。入力・設定の変更から700ms後に自動保存し、終了時にも最後の入力を保存します。次の起動で復元します。保存状態は画面上部へ表示します。書き込みに失敗した場合、編集に戻って手動保存するか、明示的に保存せず終了するかを選べます。

出力先の初期値はユーザーのドキュメントフォルダ内 `GameMockStudio/Generated`。出力先とCLI実行ファイルは `session.json` に保存し、次回も引き継ぎます。認証情報は保存しません。

「最近の生成・復旧」から退避フォルダを開けます。破損した自動保存は `session-unreadable-日時-ID.json` として原本を残します。履歴は最新20件を保持し、成果物自体は自動削除しません。

## 実行境界と失敗

引数には `ProcessStartInfo.ArgumentList`、本文には標準入力を使用します。シェルコマンドへ企画を埋め込みません。`workspace-write` を明示し、承認が必要な処理は非対話実行では拒否します。生成中のシェルのネットワークアクセスを無効にし、環境変数は基本項目だけを引き継ぎ、KEY・SECRET・TOKENを含む変数名の除外も有効にします。モデルとの通信はCodex CLIが担当します。サンドボックスを解除するフラグは使いません。組織の管理設定がさらに制限する場合は、その設定が優先されます。

企画とログの取り扱い、検証範囲、脆弱性の報告先は [SECURITY.md](SECURITY.md) を参照してください。

認証切れ・モデル利用不可・利用量上限・権限制限・CLI不在はログに表示して停止します。別モデルへのフォールバック、自動再試行、Banked resetの使用はしません。上限の復帰時刻がCLIから返れば、実行ログにそのまま残ります。キャンセルとウィンドウ終了ではプロセスツリーを終了し、途中の成果物を残します。CLI実行が20分を超えた場合も停止し、時間切れを表示します。

Windowsでは `.cmd` ラッパーではなく実体の `codex.exe` を指定してください。実装はmacOSを主な対象としており、Windows/Linuxの実機確認は未実施です。

## 構成

MVPとRx.NETを使用します。Viewは入力と描画、Presenterは操作の流れ、Briefは企画、GenerationはCLIと成果物検証、Storageはファイル操作を担当します。入力と操作をObservableへ変換し、購読は画面の寿命で破棄します。シーンやアセットを直接操作するUnity連携はアプリ本体には含みません。

## 開発

既定ブランチは `develop`。変更は作業ブランチからPRを作成し、CIの成功後にsquash mergeします。macOSのCIで次のコマンドを実行します。自動テストは実モデルへ通信しません。

```sh
dotnet test tests/GameMockStudio.Tests.csproj --configuration Release --nologo
```

生成物、ログ、ビルド成果物、ローカル設定はコミット対象外です。開発時の注意は [CLAUDE.md](CLAUDE.md) を参照してください。

NuGetの直接・間接依存を監査し、CIでは脆弱性の検出と監査情報の取得失敗をエラーにします。GitHub Actionsの参照はコミットSHAで固定し、Dependabotで依存とActionsの更新を確認します。

## 参照した仕様

- [OpenAI公式：Codexの非対話実行](https://developers.openai.com/codex/noninteractive)
- [OpenAI公式：Codexの認証](https://learn.chatgpt.com/docs/auth)
- [OpenAI公式：Codexの設定](https://learn.chatgpt.com/docs/config-file/config-reference)
- [OpenAI公式：GPT-6 Astra](https://developers.openai.com/api/docs/models/gpt-6-astra)
- [Avalonia公式：テーマ](https://docs.avaloniaui.net/docs/styling/themes)
- ローカル `codex exec --help`（0.153.2）とモデルカタログのGPT-6 xhigh対応を確認。
- Avalonia 12.1.2、Rx.NET 6.1.0を固定バージョンとして使用。

未実行の受け入れ確認は [確認手順](docs/verification.md) を参照してください。
