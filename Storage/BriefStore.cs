using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Brief;

namespace GameMockStudio.Storage;

/// <summary>企画ファイルをJSONとして読み書きする。</summary>
public sealed class BriefStore
{
    private readonly JsonSerializerOptions options = new() { WriteIndented = true };
    private readonly AtomicTextFile file = new();

    /// <summary>書き込み完了後に置換し、中断時も元ファイルを維持する。</summary>
    public async Task SaveAsync(string path, BriefDocument document, CancellationToken cancellationToken)
    {
        var normalized = document.Normalize();
        await file.WriteAsync(path, JsonSerializer.Serialize(normalized, options), cancellationToken);
    }

    /// <summary>読み込みと検証を完了してから企画を返す。</summary>
    public async Task<BriefDocument> LoadAsync(string path, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(path, cancellationToken);
        var document = JsonSerializer.Deserialize<BriefDocument>(content, options)
            ?? throw new InvalidOperationException("企画ファイルが空です。");
        return document.Normalize();
    }
}
