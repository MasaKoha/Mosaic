using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Brief;
using GameMockStudio.Brief.Planning;
using GameMockStudio.Generation;
using GameMockStudio.Generation.Refinement;
using GameMockStudio.Storage;

namespace GameMockStudio.Cli.Generation;

/// <summary>既存の生成指示・改善入力・Codex実行へCLIの引数を渡す。</summary>
internal sealed class GenerationCommands(FieldCatalogs catalogs, BriefStore store, TextWriter output)
{
    private readonly PromptComposer composer = new(catalogs);
    private readonly CodexCommand command = new();

    /// <summary>指示の出力、または実モデルによる生成・改善を実行する。</summary>
    public async Task ExecuteAsync(CliArguments arguments)
    {
        if (arguments.Command == "prompt")
        {
            var brief = await store.LoadAsync(arguments.Get("brief"), CancellationToken.None);
            output.Write(composer.Compose(brief, Guid.NewGuid().ToString("N")));
            output.Flush();
            return;
        }
        var request = arguments.Command == "refine"
            ? await CreateRefinementAsync(arguments)
            : await CreateGenerationAsync(arguments);
        var runner = new CodexRunner(command, store, new ArtifactValidator(catalogs, store));
        await new GenerationExecution(runner, output, arguments.Has("json")).RunAsync(request);
    }

    private async Task<GenerationRequest> CreateGenerationAsync(CliArguments arguments)
    {
        var brief = await store.LoadAsync(arguments.Get("brief"), CancellationToken.None);
        return new GenerationRequest
        {
            Brief = brief,
            Prompt = composer.Compose(brief, Guid.NewGuid().ToString("N")),
            Executable = arguments.Get("codex", command.FindExecutable()),
            OutputRoot = arguments.Get("output")
        };
    }

    private async Task<GenerationRequest> CreateRefinementAsync(CliArguments arguments)
    {
        var source = arguments.Get("mock");
        var sourceBrief = await store.LoadAsync(Path.Combine(source, "request.json"), CancellationToken.None);
        // 元の明示値まで再固定すると、感想に沿った変更を成果物検査が拒否するためGUIと同じ入力にする。
        var brief = new BriefDocument { Kind = sourceBrief.Kind, Format = sourceBrief.Format };
        var refinement = new RefinementRequest
        {
            SourceDirectory = source,
            Feedback = await File.ReadAllTextAsync(arguments.Get("feedback-file"))
        };
        refinement.Validate();
        return new GenerationRequest
        {
            Brief = brief,
            Prompt = composer.ComposeRefinement(brief.Kind, brief.Format, refinement.Feedback, Guid.NewGuid().ToString("N")),
            Executable = arguments.Get("codex", command.FindExecutable()),
            OutputRoot = arguments.Get("output"),
            Refinement = refinement
        };
    }
}
