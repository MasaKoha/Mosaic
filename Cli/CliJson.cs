using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameMockStudio.Cli;

/// <summary>ターミナルへ日本語を保ったJSONとJSONLを出力する。</summary>
internal sealed class CliJson(TextWriter output)
{
    private readonly JsonSerializerOptions options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // ターミナルとファイルへ出す値でHTMLへ埋め込まないため、< > & をエスケープせず読める形で出す。
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private JsonSerializerOptions? lineOptions;

    /// <summary>通常のコマンド結果を読みやすいJSONとして出す。</summary>
    public void Write(object value)
    {
        output.WriteLine(JsonSerializer.Serialize(value, options));
        output.Flush();
    }

    /// <summary>進捗は改行区切りの契約を守るため1オブジェクトを1行で出す。</summary>
    public void WriteLine(object value)
    {
        lineOptions ??= new JsonSerializerOptions(options) { WriteIndented = false };
        output.WriteLine(JsonSerializer.Serialize(value, lineOptions));
        output.Flush();
    }
}
