using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GameMockStudio.Storage;

/// <summary>保存中断による既存ファイルの欠損を防ぐ。</summary>
public sealed class AtomicTextFile
{
    /// <summary>同じディレクトリの一時ファイルへ書き終えてから置換する。</summary>
    public async Task WriteAsync(string path, string content, CancellationToken cancellationToken)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
