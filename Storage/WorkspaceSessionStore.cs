using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GameMockStudio.Generation.History;

namespace GameMockStudio.Storage;

/// <summary>編集中の企画と設定を、書き込み順序と破損時の原本を守って保存する。</summary>
public sealed class WorkspaceSessionStore(string directory) : IDisposable
{
    private readonly JsonSerializerOptions options = new() { WriteIndented = true };
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly AtomicTextFile file = new();

    /// <summary>OSが指定したアプリデータ保存先。</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameMockStudio");
    /// <summary>現在の自動保存ファイル。</summary>
    public string SessionPath => Path.Combine(directory, "session.json");

    /// <summary>保存が無ければnull、不正な保存内容なら原本を維持して例外を返す。</summary>
    public async Task<WorkspaceSession?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(SessionPath))
        {
            return null;
        }
        var content = await File.ReadAllTextAsync(SessionPath, cancellationToken);
        var session = JsonSerializer.Deserialize<WorkspaceSession>(content, options)
            ?? throw new InvalidOperationException("自動保存ファイルが空です。");
        return Validate(session);
    }

    /// <summary>復元できないファイルを別名へ退避し、次の保存で原本を失わないようにする。</summary>
    public string PreserveUnreadableSession()
    {
        var backup = Path.Combine(directory, $"session-unreadable-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        File.Move(SessionPath, backup);
        return backup;
    }

    /// <summary>自動保存と終了時の保存を直列化し、最後の入力を残す。</summary>
    public async Task SaveAsync(WorkspaceSession session, CancellationToken cancellationToken)
    {
        var content = JsonSerializer.Serialize(Validate(session), options);
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(directory);
            await file.WriteAsync(SessionPath, content, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>すべての保存が終了した後で同期資源を破棄する。</summary>
    public void Dispose()
    {
        writeLock.Dispose();
    }

    private WorkspaceSession Validate(WorkspaceSession session)
    {
        if (session.Version != WorkspaceSession.CurrentVersion || session.Brief is null || session.Executable is null
            || session.OutputRoot is null || session.History is null)
        {
            throw new InvalidOperationException("対応していない自動保存ファイルです。");
        }
        foreach (var entry in session.History)
        {
            if (entry is null)
            {
                throw new InvalidOperationException("保存された生成履歴が不正です。");
            }
            entry.Validate();
        }
        return session with { Brief = session.Brief.Normalize() };
    }
}
