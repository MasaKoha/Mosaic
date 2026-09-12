using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GameMockStudio.Generation.Refinement;

/// <summary>秘密情報・キャッシュ・リンクを持ち込まず、ゲームの独立した作業コピーを作る。</summary>
public sealed class GameSnapshot
{
    private const int MaximumFiles = 10000;
    private const long MaximumBytes = 256L * 1024 * 1024;
    private const int CopyBufferBytes = 81920;
    private readonly HashSet<string> excludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", "Library", "Temp", "Logs", "Recovery", "UserSettings",
        "auth.json", "credentials.json", "secrets.json", "session.json", "request.json", "prompt.md", "result.md", "events.jsonl",
        "diagnostics.log", "feedback.md", "refinement-request.json"
    };
    private readonly HashSet<string> excludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pem", ".key", ".p12", ".pfx", ".log"
    };

    /// <summary>リンク経由も含めて、元のゲーム配下を出力先として使うことを拒否する。</summary>
    public void ValidateDestination(string sourceDirectory, string outputRoot)
    {
        RequireRegularEntry(new DirectoryInfo(sourceDirectory));
        var source = ResolveDirectory(sourceDirectory);
        var destination = ResolveDirectory(outputRoot);
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (destination.Equals(source, comparison)
            || destination.StartsWith(Path.TrimEndingDirectorySeparator(source) + Path.DirectorySeparatorChar, comparison))
        {
            throw new InvalidOperationException("元のゲームを保護するため、出力先はその成果物フォルダの外に指定してください。");
        }
    }

    /// <summary>サイズと件数を制限してコピーし、途中取消でも元のファイルを変更しない。</summary>
    public async Task CopyAsync(string sourceDirectory, string destination, CancellationToken cancellationToken)
    {
        ValidateDestination(sourceDirectory, destination);
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(sourceDirectory));
        var fileCount = 0;
        long totalBytes = 0;
        while (pending.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireRegularEntry(directory);
            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsExcluded(entry.Name))
                {
                    continue;
                }
                RequireRegularEntry(entry);
                var target = Path.Combine(destination, Path.GetRelativePath(sourceDirectory, entry.FullName));
                if (entry is DirectoryInfo child)
                {
                    pending.Push(child);
                    continue;
                }
                fileCount++;
                if (fileCount > MaximumFiles || ((FileInfo)entry).Length > MaximumBytes - totalBytes)
                {
                    throw new InvalidOperationException("改善用コピーの上限（10,000ファイル・256MiB）を超えました。不要な生成物を除いてください。");
                }
                totalBytes += await CopyFileAsync(entry.FullName, target, MaximumBytes - totalBytes, cancellationToken);
            }
        }
    }

    private bool IsExcluded(string name)
    {
        return name.StartsWith('.') || name.StartsWith("baseline-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("session-unreadable-", StringComparison.OrdinalIgnoreCase)
            || name.Contains(".secret.", StringComparison.OrdinalIgnoreCase)
            || excludedNames.Contains(name) || excludedExtensions.Contains(Path.GetExtension(name));
    }

    private void RequireRegularEntry(FileSystemInfo entry)
    {
        entry.Refresh();
        if (!entry.Exists || entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidOperationException($"コピー元が存在しないか、シンボリックリンクを含んでいます: {entry.Name}");
        }
    }

    private string ResolveDirectory(string path)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(path));
        if (directory.Parent is null)
        {
            return directory.FullName;
        }
        var parent = ResolveDirectory(directory.Parent.FullName);
        var resolved = new DirectoryInfo(Path.Combine(parent, directory.Name));
        return resolved.Exists && resolved.Attributes.HasFlag(FileAttributes.ReparsePoint)
            ? ResolveDirectory(resolved.ResolveLinkTarget(returnFinalTarget: true)!.FullName) : resolved.FullName;
    }

    private async Task<long> CopyFileAsync(string source, string destination, long remainingBytes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[CopyBufferBytes];
        long copiedBytes = 0;
        int readBytes;
        while ((readBytes = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            copiedBytes += readBytes;
            // コピー中に別のプロセスが元ファイルを拡張しても、保存容量の上限を超えさせない。
            if (copiedBytes > remainingBytes)
            {
                throw new InvalidOperationException("改善用コピーの容量上限（256MiB）を超えました。");
            }
            await output.WriteAsync(buffer.AsMemory(0, readBytes), cancellationToken);
        }
        if (!OperatingSystem.IsWindows())
        {
            // 起動スクリプトの実行権限だけを引き継ぎ、setuidなどの特権はコピーしない。
            var executablePermissions = File.GetUnixFileMode(source)
                & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
            File.SetUnixFileMode(destination, File.GetUnixFileMode(destination) | executablePermissions);
        }
        return copiedBytes;
    }
}
