using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Brief;
using GameMockStudio.Generation.Completion;
using GameMockStudio.Storage;

namespace GameMockStudio.Generation;

/// <summary>終了コードだけで成功とせず、生成物と補完企画を検査する。</summary>
public sealed class ArtifactValidator(FieldCatalog catalog, BriefStore store)
{
    private readonly JsonSerializerOptions reportOptions = new()
    {
        Converters = { new JsonStringEnumConverter<GenerationOutcome>(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    /// <summary>必須ファイル、全項目の補完、明示指定の維持を確認する。</summary>
    public async Task ValidateAsync(string directory, BriefDocument requested, CancellationToken cancellationToken)
    {
        requested = requested.Normalize();
        await ValidateCompletionAsync(directory, cancellationToken);
        RequireFile(directory, "README.md");
        RequireFile(directory, "decisions.md");
        RequireFile(directory, "resolved-brief.json");
        RequireGameFiles(directory, requested.Format);
        var resolved = await store.LoadAsync(Path.Combine(directory, "resolved-brief.json"), cancellationToken);
        if (resolved.Format != requested.Format || resolved.Genres.Length == 0)
        {
            throw new InvalidOperationException("補完企画の形式またはジャンルが不正です。成果物フォルダを確認してください。");
        }
        if (requested.Genres.Length > 0 && !requested.Genres.ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(resolved.Genres))
        {
            throw new InvalidOperationException("指定したジャンルが生成結果で変更されています。");
        }
        foreach (var field in catalog.Fields)
        {
            RequireResolvedField(field, resolved);
        }
        ValidateSpecifiedValues(requested, resolved);
    }

    private async Task ValidateCompletionAsync(string directory, CancellationToken cancellationToken)
    {
        RequireFile(directory, GenerationReport.FileName);
        await using var stream = File.OpenRead(Path.Combine(directory, GenerationReport.FileName));
        var report = await JsonSerializer.DeserializeAsync<GenerationReport>(stream, reportOptions, cancellationToken)
            ?? throw new InvalidOperationException("生成結果レポートが空です。");
        report.EnsureCompleted();
    }

    private void RequireResolvedField(FieldDefinition field, BriefDocument resolved)
    {
        if (!resolved.Values.TryGetValue(field.Identifier, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"企画の補完が未完了です: {field.Label}");
        }
    }

    private void ValidateSpecifiedValues(BriefDocument requested, BriefDocument resolved)
    {
        foreach (var specified in requested.Values.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
        {
            if (!resolved.Values.TryGetValue(specified.Key, out var value) || value != specified.Value)
            {
                var label = catalog.Fields.FirstOrDefault(field => field.Identifier == specified.Key)?.Label ?? specified.Key;
                throw new InvalidOperationException($"明示指定が変更されています: {label}");
            }
        }
    }

    private void RequireGameFiles(string directory, MockFormat format)
    {
        switch (format)
        {
            case MockFormat.Browser:
                RequireFile(directory, "index.html");
                break;
            case MockFormat.Unity:
                RequireFile(directory, "ProjectSettings/ProjectVersion.txt");
                RequireFile(directory, "Packages/manifest.json");
                if (!Directory.Exists(Path.Combine(directory, "Assets"))
                    || !Directory.EnumerateFiles(Path.Combine(directory, "Assets"), "*.cs", SearchOption.AllDirectories).Any(HasContent))
                {
                    throw new InvalidOperationException("Unityの実装ソースがありません。");
                }
                break;
            case MockFormat.Avalonia:
                RequireFile(directory, "GameMock/GameMock.csproj");
                if (!Directory.EnumerateFiles(Path.Combine(directory, "GameMock"), "*.cs", SearchOption.AllDirectories).Any(HasContent))
                {
                    throw new InvalidOperationException("Avaloniaの実装ソースがありません。");
                }
                break;
            default:
                throw new InvalidOperationException("生成形式が不正です。");
        }
    }

    private void RequireFile(string directory, string relativePath)
    {
        var path = Path.Combine(directory, relativePath);
        if (!File.Exists(path) || !HasContent(path))
        {
            throw new InvalidOperationException($"生成ファイルが不足しています: {relativePath}");
        }
    }

    private bool HasContent(string path)
    {
        using var reader = File.OpenText(path);
        while (reader.Read() is var character && character != -1)
        {
            if (!char.IsWhiteSpace((char)character))
            {
                return true;
            }
        }
        return false;
    }
}
