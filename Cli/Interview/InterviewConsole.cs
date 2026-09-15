using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Brief;
using GameMockStudio.Storage;

namespace GameMockStudio.Cli.Interview;

/// <summary>差し替え可能な入出力を使い、1回答の保存後に次の入力を受け付ける。</summary>
public sealed class InterviewConsole(InterviewState state, BriefStore store, TextReader input, TextWriter output)
{
    private const string QuitCommand = ":q";

    /// <summary>全項目の回答まで対話し、中断時は保存済みの回答を残してキャンセルを通知する。</summary>
    public async Task<BriefDocument> RunAsync(string path, BriefDocument brief)
    {
        var current = brief;
        while (state.FindNext(current) is { } question)
        {
            WriteQuestion(question, state.GetProgress(current));
            var value = await input.ReadLineAsync(CancellationToken.None);
            if (value is null || value == QuitCommand)
            {
                throw new OperationCanceledException("対話を中断しました。回答済みの内容は保存されています。");
            }
            current = state.ApplyAnswers(current, new Dictionary<string, string> { [question.Field.Identifier] = value });
            // 次の質問を出す前に保存し、どこで中断しても直前の回答までは再開できるようにする。
            await store.SaveAsync(path, current, CancellationToken.None);
        }
        output.WriteLine("全項目の回答を保存しました。");
        return current;
    }

    private void WriteQuestion(InterviewQuestion question, InterviewProgress progress)
    {
        output.WriteLine($"\n【{question.Category.Label}】{question.Category.Description}");
        output.WriteLine($"回答済み {progress.Answered}/{progress.Total}・指定あり {progress.Specified}");
        output.WriteLine($"{question.Field.Label}（{question.Field.Identifier}）");
        output.WriteLine(question.Field.Description);
        output.WriteLine($"記入例: {question.Field.Example}");
        output.Write("回答（空行=AI補完、なし=不採用、:q=中断）: ");
        output.Flush();
    }
}
