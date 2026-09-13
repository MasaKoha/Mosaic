using System.Linq;
using GameMockStudio.Brief;
using GameMockStudio.Brief.Planning;

namespace GameMockStudio.Cli.Interview;

/// <summary>回答ロジックから質問と集計を取り出してCLIのJSON契約へ変換する。</summary>
internal sealed class InterviewOutput(FieldCatalogs catalogs, InterviewState state, CliJson output)
{
    /// <summary>次の質問、または対象範囲の完了を出力する。進捗は常に企画全体を示す。</summary>
    public void WriteNext(BriefDocument brief, string? categoryLabel = null)
    {
        var question = state.FindNext(brief, categoryLabel);
        var progress = state.GetProgress(brief);
        if (question is null)
        {
            output.Write(new { Done = true, brief.Kind, Progress = progress });
            return;
        }
        output.Write(new
        {
            Done = false,
            brief.Kind,
            Category = new { question.Category.Label, question.Category.Description },
            question.Field,
            Progress = progress,
            HowToAnswer = $"answer --field {question.Field.Identifier} --value <text> | --random（AI補完） | --none（不採用）"
        });
    }

    /// <summary>企画全体と各カテゴリの回答数・指定数を出力する。</summary>
    public void WriteStatus(BriefDocument brief)
    {
        var categories = catalogs.ForKind(brief.Kind).Categories.Select(category =>
        {
            var progress = state.GetProgress(brief, category.Label);
            return new { category.Label, progress.Answered, progress.Specified, progress.Total };
        }).ToArray();
        output.Write(new { brief.Kind, brief.Format, brief.Genres, Progress = state.GetProgress(brief), Categories = categories });
    }
}
