using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Brief;
using GameMockStudio.Storage;

namespace GameMockStudio.Generation.Refinement;

/// <summary>前回の完成物を検査し、改善前の記録と今回の成果物を区別する。</summary>
public sealed class RefinementPreparation(ArtifactValidator validator, BriefStore store)
{
    private readonly GameSnapshot snapshot = new();

    /// <summary>独立したコピーにだけ改善用の資料を用意する。</summary>
    public async Task<BriefDocument> PrepareAsync(RefinementRequest request, BriefDocument brief, string directory,
        CancellationToken cancellationToken)
    {
        await snapshot.CopyAsync(request.SourceDirectory, directory, cancellationToken);
        await validator.ValidateAsync(directory, brief, cancellationToken);
        var baseline = await store.LoadAsync(Path.Combine(directory, "resolved-brief.json"), cancellationToken);
        // 前回の完了レポートや企画が、新しい改善の成功判定に混入することを防ぐ。
        foreach (var name in new[] { "resolved-brief.json", "README.md", "decisions.md", "generation-report.json" })
        {
            File.Move(Path.Combine(directory, name), Path.Combine(directory, "baseline-" + name));
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "feedback.md"), request.Feedback, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "refinement-request.json"),
            JsonSerializer.Serialize(request), cancellationToken);
        return baseline;
    }
}
