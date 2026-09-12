using System;
using System.Text.Json;

namespace GameMockStudio.Generation;

/// <summary>JSONLの終了状態と利用者向けメッセージを抽出する。</summary>
public sealed class CodexEventReader
{
    /// <summary>正常なターン終了を受信したか。</summary>
    public bool TurnCompleted { get; private set; }
    /// <summary>最初に受信した失敗内容。</summary>
    public string Failure { get; private set; } = string.Empty;

    /// <summary>未対応イベントを許容しながらログを読み取る。</summary>
    public string Read(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return line;
            }
            var type = ReadString(root, "type");
            if (type == "turn.completed")
            {
                TurnCompleted = true;
                return "Codexのターン完了。成果物を確認しています。";
            }
            if (type is "turn.failed" or "error")
            {
                if (Failure.Length == 0)
                {
                    Failure = line;
                }
                return line;
            }
            if (root.TryGetProperty("item", out var item))
            {
                return ReadItem(item, type);
            }
            return type.Length == 0 ? line : type;
        }
        catch (JsonException)
        {
            return line;
        }
    }

    private string ReadItem(JsonElement item, string eventType)
    {
        var text = ReadString(item, "text");
        if (text.Length > 0)
        {
            return text;
        }
        var command = ReadString(item, "command");
        return command.Length > 0 ? command : $"{eventType}: {ReadString(item, "type")}";
    }

    private string ReadString(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
    }
}
