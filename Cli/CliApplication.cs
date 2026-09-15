using System;
using System.IO;
using System.Threading.Tasks;
using GameMockStudio.Brief.Planning;
using GameMockStudio.Cli.Generation;
using GameMockStudio.Cli.Interview;
using GameMockStudio.Storage;

namespace GameMockStudio.Cli;

/// <summary>Avaloniaを起動せず、コマンドの振り分けと終了コードを提供する。</summary>
public sealed class CliApplication(TextReader input, TextWriter output, TextWriter error)
{
    /// <summary>同期のMainからCLIを実行し、プロセスの終了コードを返す。</summary>
    public static int Run(string[] arguments)
    {
        var application = new CliApplication(Console.In, Console.Out, Console.Error);
        return (int)application.RunAsync(arguments).GetAwaiter().GetResult();
    }

    /// <summary>差し替えた入出力でコマンドを実行し、想定外の失敗もstderrへまとめる。</summary>
    public async Task<CliExitCode> RunAsync(string[] arguments)
    {
        try
        {
            if (arguments.Length == 0 || arguments is ["--help"] or ["interview", "--help"])
            {
                output.WriteLine(CliHelp.Text);
                return CliExitCode.Success;
            }
            var parsed = CliArguments.Parse(arguments);
            if (parsed.Has("help"))
            {
                output.WriteLine(CliHelp.Text);
                return CliExitCode.Success;
            }
            await ExecuteAsync(parsed);
            return CliExitCode.Success;
        }
        catch (CliUsageException exception)
        {
            error.WriteLine($"引数の誤り: {exception.Message}\n使い方は cli --help を参照してください。");
            return CliExitCode.UsageError;
        }
        catch (OperationCanceledException exception)
        {
            error.WriteLine($"キャンセル: {exception.Message}");
            return CliExitCode.Failure;
        }
        catch (Exception exception)
        {
            error.WriteLine($"実行に失敗しました: {exception.Message}");
            return CliExitCode.Failure;
        }
    }

    private async Task ExecuteAsync(CliArguments arguments)
    {
        var catalogs = new FieldCatalogs();
        var store = new BriefStore();
        if (arguments.Command.StartsWith("interview ", StringComparison.Ordinal))
        {
            await new InterviewCommands(catalogs, store, input, output).ExecuteAsync(arguments);
            return;
        }
        if (arguments.Command is "kinds" or "fields" or "ideas")
        {
            new CatalogCommands(catalogs, new CliJson(output)).Execute(arguments);
            return;
        }
        await new GenerationCommands(catalogs, store, output).ExecuteAsync(arguments);
    }
}
