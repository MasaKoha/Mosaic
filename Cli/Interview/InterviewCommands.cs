using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Brief;
using GameMockStudio.Brief.Discovery;
using GameMockStudio.Brief.Planning;
using GameMockStudio.Storage;

namespace GameMockStudio.Cli.Interview;

/// <summary>企画ファイルの読み書きと回答操作を結び付ける。</summary>
internal sealed class InterviewCommands(FieldCatalogs catalogs, BriefStore store, TextReader input, TextWriter output)
{
    private readonly InterviewState state = new(catalogs);

    /// <summary>1回の呼び出し分の企画操作を実行する。</summary>
    public async Task ExecuteAsync(CliArguments arguments)
    {
        var path = arguments.Get("brief");
        var presentation = new InterviewOutput(catalogs, state, new CliJson(output));
        if (arguments.Command == "interview start")
        {
            var created = CreateBrief(arguments);
            await SaveNewAsync(path, created);
            presentation.WriteNext(created);
            return;
        }
        var brief = await store.LoadAsync(path, CancellationToken.None);
        switch (arguments.Command)
        {
            case "interview next":
                presentation.WriteNext(brief, arguments.Has("category") ? arguments.Get("category") : null);
                break;
            case "interview answer":
                var answered = state.ApplyAnswers(brief, await ReadAnswersAsync(arguments));
                await store.SaveAsync(path, answered, CancellationToken.None);
                presentation.WriteNext(answered);
                break;
            case "interview genres":
                var selected = (brief with { Genres = arguments.Get("set").Split(',') }).Normalize();
                await store.SaveAsync(path, selected, CancellationToken.None);
                presentation.WriteStatus(selected);
                break;
            case "interview finish":
                var finished = state.Finish(brief);
                await store.SaveAsync(path, finished, CancellationToken.None);
                presentation.WriteStatus(finished);
                break;
            case "interview status":
                presentation.WriteStatus(brief);
                break;
            case "interview ask":
                var completed = await new InterviewConsole(state, store, input, output).RunAsync(path, brief);
                presentation.WriteStatus(completed);
                break;
        }
    }

    private BriefDocument CreateBrief(CliArguments arguments)
    {
        var kind = arguments.GetKind();
        var format = arguments.GetFormat();
        if (!arguments.Has("idea"))
        {
            return new BriefDocument { Kind = kind, Format = format }.Normalize();
        }
        var ideas = new IdeaCatalog(catalogs).ForKind(kind);
        var number = arguments.GetIdeaNumber();
        if (number > ideas.Count)
        {
            throw new CliUsageException($"--idea はこの種類の1〜{ideas.Count}を指定してください。");
        }
        return ideas[number - 1].CreateBrief(format);
    }

    private async Task SaveNewAsync(string path, BriefDocument brief)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            throw new InvalidOperationException($"既に存在するため作成できません: {path}");
        }
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".new";
        try
        {
            await store.SaveAsync(temporaryPath, brief, CancellationToken.None);
            // 存在確認の後に別の呼び出しが作成しても、既存企画を上書きしない。
            File.Move(temporaryPath, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async Task<Dictionary<string, string>> ReadAnswersAsync(CliArguments arguments)
    {
        if (!arguments.Has("from-json"))
        {
            var value = arguments.Has("none") ? "なし" : arguments.Get("value");
            return new Dictionary<string, string> { [arguments.Get("field")] = value };
        }
        var content = await File.ReadAllTextAsync(arguments.Get("from-json"));
        var answers = JsonSerializer.Deserialize<Dictionary<string, string>>(content)
            ?? throw new InvalidOperationException("回答JSONは項目IDと文字列のオブジェクトで指定してください。");
        if (answers.Values.Any(value => value is null))
        {
            throw new InvalidOperationException("回答JSONの値は文字列で指定してください。AI補完は空文字です。");
        }
        return answers;
    }
}
