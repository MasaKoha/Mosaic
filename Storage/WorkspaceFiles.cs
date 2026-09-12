using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GameMockStudio.Brief;

namespace GameMockStudio.Storage;

/// <summary>ローカル企画の選択・退避と成果物を開く操作を担当する。</summary>
public sealed class WorkspaceFiles(Window window, BriefStore store, string dataDirectory)
{
    private readonly FilePickerFileType briefType = new("ゲーム企画 JSON") { Patterns = ["*.json"] };

    /// <summary>生成物の標準保存先。</summary>
    public static string DefaultOutputRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GameMockStudio", "Generated");

    /// <summary>直前の企画を退避したフォルダを開く。</summary>
    public void OpenRecovery()
    {
        var directory = Path.Combine(dataDirectory, "Recovery");
        Directory.CreateDirectory(directory);
        Open(directory);
    }

    /// <summary>選択済みの生成フォルダから補完企画を読み込む。</summary>
    public Task<BriefDocument> LoadResolvedAsync(string directory, CancellationToken cancellationToken)
    {
        return store.LoadAsync(Path.Combine(directory, "resolved-brief.json"), cancellationToken);
    }

    /// <summary>画面を置き換える前に、現在の企画を別ファイルに退避する。</summary>
    public async Task<string> SaveRecoveryAsync(BriefDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(dataDirectory, "Recovery");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"brief-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        await store.SaveAsync(path, document, cancellationToken);
        return path;
    }

    /// <summary>保存先を選択し、キャンセルされなければ企画を保存する。</summary>
    public async Task<string?> SaveBriefAsync(BriefDocument document, CancellationToken cancellationToken)
    {
        using var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "ゲーム企画を保存",
            SuggestedFileName = "game-brief.json",
            DefaultExtension = "json",
            FileTypeChoices = [briefType]
        });
        if (file is null)
        {
            return null;
        }
        var path = RequireLocalPath(file);
        await store.SaveAsync(path, document, cancellationToken);
        return path;
    }

    /// <summary>選択された企画を検証し、読み込みキャンセルならnullを返す。</summary>
    public async Task<BriefDocument?> LoadBriefAsync(CancellationToken cancellationToken)
    {
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "ゲーム企画を開く（補完済み企画も読み込めます）",
            AllowMultiple = false,
            FileTypeFilter = [briefType]
        });
        if (files.Count == 0)
        {
            return null;
        }
        using var file = files[0];
        return await store.LoadAsync(RequireLocalPath(file), cancellationToken);
    }

    /// <summary>生成フォルダの親ディレクトリを選択する。</summary>
    public async Task<string?> ChooseOutputAsync()
    {
        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "モックの出力先を選択",
            AllowMultiple = false
        });
        if (folders.Count == 0)
        {
            return null;
        }
        using var folder = folders[0];
        return RequireLocalPath(folder);
    }

    /// <summary>利用者が選んだ成果物をOSの既定アプリで開く。</summary>
    public void Open(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException("成果物が見つかりません。移動・削除されていないか確認してください。", path);
        }
        var target = File.Exists(path) ? new Uri(path).AbsoluteUri : path;
        using var process = Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
    }

    private string RequireLocalPath(IStorageItem item)
    {
        return item.TryGetLocalPath()
            ?? throw new InvalidOperationException("ローカルPC上のファイルまたはフォルダを選択してください。");
    }
}
