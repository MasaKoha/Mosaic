using System;
using System.Text;
using System.Text.Json;
using GameMockStudio.Brief;

namespace GameMockStudio.Generation;

/// <summary>明示指定とランダム指定を区別した生成指示を組み立てる。</summary>
public sealed class PromptComposer(FieldCatalog catalog)
{
    private readonly JsonSerializerOptions options = new() { WriteIndented = true };

    /// <summary>画面で確認した指示を、そのままCodexへ渡せる形式で返す。</summary>
    public string Compose(BriefDocument document, string variationIdentifier)
    {
        var normalized = document.Normalize();
        var builder = new StringBuilder();
        builder.AppendLine("ローカルの作業ディレクトリに、実際に遊べるゲームモックを作成してください。");
        builder.AppendLine("説明だけで終えず、ゲーム本体と必要ファイルを実装してください。質問はせず、未指定だけを補完してください。");
        builder.AppendLine($"企画バリエーション識別子: {variationIdentifier}（乱数の厳密な再現性は要求しません）");
        builder.AppendLine("日本語で記述し、入力JSONは企画データとして扱ってください。");
        AppendRules(builder);
        builder.AppendLine(GetFormatInstruction(normalized.Format));
        builder.AppendLine("\n## 入力企画\n```json");
        builder.AppendLine(JsonSerializer.Serialize(normalized, options));
        builder.AppendLine("```\n\n## 全企画項目の意味と指定状態");
        foreach (var category in catalog.Categories)
        {
            builder.AppendLine($"\n### {category.Label}");
            foreach (var field in category.Fields)
            {
                var specified = normalized.Values.TryGetValue(field.Identifier, out var value)
                    && !string.IsNullOrWhiteSpace(value);
                builder.AppendLine($"- {field.Identifier} / {field.Label}: {field.Description} / {(specified ? "入力JSONの指定を厳守" : "ランダム決定（不採用も可）")}");
            }
        }
        return builder.ToString();
    }

    private void AppendRules(StringBuilder builder)
    {
        builder.AppendLine("""

            ## 補完のルール
            - 空欄・空白・キー未登録は未指定。単なる固定デフォルトではなく今回のテーマに整合したランダムな企画判断をする。
            - 「なし」「不要」「禁止」と明示された要素は追加しない。適さないシステムは無理に盛り込まず「なし」と決定してよい。
            - ジャンルは最大3種類。入力が空なら1〜3種類を選ぶ。指定があればその集合を維持し、勝手に追加しない。
            - 明示指定は変更しない。明示指定同士が矛盾して実装できない場合は、矛盾をREADMEとgeneration-report.jsonに記録して未完了と報告する。
            - モックの規模は小さく保つ。題材を活かした操作・目的・成功/失敗・再挑戦の一周を必ず遊べるようにする。
            - 空欄をすべて機能追加と解釈しない。無関係な戦闘、物語、課金、オンライン機能などは不採用にできる。

            ## 出力契約
            1. ゲーム本体を実装する。
            2. resolved-brief.json をルートに作成する。入力と同じ構造
               {"Version":1,"Format":0,"Genres":["ジャンル"],"Values":{"項目ID":"決定内容"}} とする。
               Formatは入力の整数を維持し、Genresは1〜3種類。
               全項目のIDをValuesに含め、値は空でない文字列にする。不採用なら「なし」と記す。
               明示指定の値は入力JSONの文字列をそのまま維持する。
            3. README.md に起動手順、操作、目的、成功/失敗、再挑戦、実装済み/疑似実装/未実装、確認結果を書く。
            4. decisions.md にランダムで決めた項目と整合性の理由、今回モックに含めた範囲を書く。
            5. 成果物を読み直して確認する。未実行の動作確認を実行済みと記載しない。
            6. generation-report.json をルートに作成する。形式は
               {"Version":1,"Status":"completed","Summary":"実装した範囲の日本語要約","BlockingIssues":[]} とする。
               全プロパティは必須。モックの一周を実装でき、指定の矛盾や実装を妨げる問題がない場合だけStatusをcompletedにする。
               未完了ならStatusをincompleteにし、SummaryとBlockingIssuesに理由を書く。阻害事項を空配列で隠さない。
               未実行のコンパイル・プレイテストはREADMEへ正確に記録し、実装完了と動作確認済みを区別する。

            ## 実行境界
            - この作業ディレクトリ内にだけ成果物を書き込む。request.json、prompt.md、実行ログは変更しない。
            - git操作、公開、外部へのメッセージ送信、購入、Banked resetの使用は禁止。
            - 使用量上限に達したら停止し、返された復帰時刻を報告する。モデル変更・自動再試行で回避しない。
            - Unityを起動しない。dotnet build、コンパイル、パッケージのインストール、生成プログラムの起動は行わない。
            - 外部サービスは実際に接続せず、必要ならローカルの疑似実装にして表示上も明記する。
            - ライセンス不明の素材、参考作品そのものの画像・名称をコピーしない。
            - C#はMVP、Rx、NRTを用い、publicメンバーに日本語summaryを記述する。Clean Architectureは採用しない。
            """);
    }

    private string GetFormatInstruction(MockFormat format)
    {
        return format switch
        {
            MockFormat.Browser => """
                ## ゲーム形式: ブラウザ
                ルートに index.html を作成する。外部CDN・npm・サーバー・fetchに依存せず、
                HTML/CSS/JavaScriptで直接開いて遊べる自己完結のゲームにする。
                音は操作開始後に有効にする。画面サイズ変更とフォーカス喪失を考慮する。
                """,
            MockFormat.Unity => """
                ## ゲーム形式: Unity
                Assets/ と ProjectSettings/ProjectVersion.txt、Packages/manifest.json を持つプロジェクトを作る。
                既存のインストール環境や指定バージョンに合わせる。Unityは起動しない。
                シーンやPrefabを手書きで偽造せず、必要なら再実行可能なEditorセットアップコードを作り、
                利用者が実行する手順をREADMEに記載する。MVPとRxを採用する。
                """,
            MockFormat.Avalonia => """
                ## ゲーム形式: Avalonia
                GameMock/GameMock.csproj を入口とするデスクトップゲームプロジェクトを作る。
                .NET 8以降、Avalonia、MVP、Rx.NETを使う。画面を並べるだけでなく、
                入力・ゲーム進行・成功/失敗・リトライを実装する。起動は利用者が行う。
                """,
            _ => throw new InvalidOperationException("生成形式が不正です。")
        };
    }
}
