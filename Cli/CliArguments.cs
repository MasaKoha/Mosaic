using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GameMockStudio.Brief;
using GameMockStudio.Brief.Planning;

namespace GameMockStudio.Cli;

/// <summary>コマンドごとの許可オプションと値の形式を検証する。</summary>
internal sealed class CliArguments
{
    private readonly Dictionary<string, string> options = new(StringComparer.Ordinal);

    private CliArguments(string command)
    {
        Command = command;
    }

    /// <summary>サブコマンドを含む操作名。</summary>
    public string Command { get; }

    /// <summary>必要なオプションが検証済みの引数を返す。</summary>
    public static CliArguments Parse(string[] arguments)
    {
        var command = arguments[0];
        var optionOffset = 1;
        if (command == "interview")
        {
            if (arguments.Length < 2 || arguments[1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new CliUsageException("interview のサブコマンドを指定してください。");
            }
            command += " " + arguments[1];
            optionOffset++;
        }
        var parsed = new CliArguments(command);
        var allowedOptions = (AllowedOptions(command) + " help").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        parsed.ReadOptions(arguments[optionOffset..], allowedOptions);
        if (!parsed.Has("help"))
        {
            parsed.Validate();
        }
        return parsed;
    }

    /// <summary>オプションが明示されたか返す。空文字の指定も存在として扱う。</summary>
    public bool Has(string name) => options.ContainsKey(name);

    /// <summary>指定された文字列を返し、未指定の場合だけ既定値を使用する。</summary>
    public string Get(string name, string defaultValue = "") => options.GetValueOrDefault(name, defaultValue);

    /// <summary>必須の種類を名前で解析する。数値の列挙値は受け付けない。</summary>
    public MockKind GetKind() => Get("kind").ToLowerInvariant() switch
    {
        "game" => MockKind.Game,
        "service" => MockKind.Service,
        "gamification" => MockKind.Gamification,
        _ => throw new CliUsageException("--kind は game / service / gamification を指定してください。")
    };

    /// <summary>省略時はブラウザ形式を選択する。</summary>
    public MockFormat GetFormat() => Get("format", "browser").ToLowerInvariant() switch
    {
        "browser" => MockFormat.Browser,
        "unity" => MockFormat.Unity,
        "avalonia" => MockFormat.Avalonia,
        _ => throw new CliUsageException("--format は browser / unity / avalonia を指定してください。")
    };

    /// <summary>内蔵案の番号を1始まりの正整数として読む。</summary>
    public int GetIdeaNumber()
    {
        if (!int.TryParse(Get("idea"), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
        {
            throw new CliUsageException("--idea は1以上の整数を指定してください。");
        }
        return number;
    }

    private static string AllowedOptions(string command) => command switch
    {
        "kinds" => "",
        "fields" or "ideas" => "kind",
        "interview start" => "brief kind format idea",
        "interview next" => "brief category",
        "interview answer" => "brief field value random none from-json",
        "interview genres" => "brief set",
        "interview finish" or "interview status" or "interview ask" or "prompt" => "brief",
        "generate" => "brief output codex json",
        "refine" => "mock feedback-file output codex json",
        _ => throw new CliUsageException($"未知のコマンドです: {command}")
    };

    private void ReadOptions(string[] arguments, string[] allowedOptions)
    {
        for (var position = 0; position < arguments.Length; position++)
        {
            var argument = arguments[position];
            if (!argument.StartsWith("--", StringComparison.Ordinal) || !allowedOptions.Contains(argument[2..]))
            {
                throw new CliUsageException($"未対応の引数です: {argument}");
            }
            var name = argument[2..];
            if (Has(name))
            {
                throw new CliUsageException($"--{name} は重複して指定できません。");
            }
            options.Add(name, ReadValue(name, arguments, ref position));
        }
    }

    private string ReadValue(string name, string[] arguments, ref int position)
    {
        if (name is "random" or "none" or "json" or "help")
        {
            return string.Empty;
        }
        if (position + 1 >= arguments.Length || arguments[position + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new CliUsageException($"--{name} の値を指定してください。");
        }
        position++;
        var value = arguments[position];
        if (name is not ("value" or "set") && string.IsNullOrWhiteSpace(value))
        {
            throw new CliUsageException($"--{name} に空の値は指定できません。");
        }
        return value;
    }

    private void Validate()
    {
        var requiredOptions = Command switch
        {
            "kinds" => "",
            "fields" or "ideas" => "kind",
            "interview start" => "brief kind",
            "interview genres" => "brief set",
            "generate" => "brief output",
            "refine" => "mock feedback-file output",
            _ => "brief"
        };
        foreach (var name in requiredOptions.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Has(name))
            {
                throw new CliUsageException($"--{name} は必須です。");
            }
        }
        ValidateValueFormats();
        if (Command == "interview answer")
        {
            ValidateAnswerOptions();
        }
    }

    private void ValidateValueFormats()
    {
        if (Has("kind"))
        {
            GetKind();
        }
        if (Has("format"))
        {
            GetFormat();
        }
        if (Has("idea"))
        {
            GetIdeaNumber();
        }
        foreach (var name in new[] { "output", "mock" })
        {
            if (Has(name) && !Path.IsPathFullyQualified(Get(name)))
            {
                throw new CliUsageException($"--{name} は絶対パスで指定してください。");
            }
        }
    }

    private void ValidateAnswerOptions()
    {
        var answerCount = new[] { "value", "random", "none" }.Count(Has);
        if (Has("from-json"))
        {
            if (Has("field") || answerCount > 0)
            {
                throw new CliUsageException("--from-json と --field / --value / --random / --none は併用できません。");
            }
            return;
        }
        if (!Has("field") || answerCount != 1)
        {
            throw new CliUsageException("--field と、--value / --random / --none のいずれか1つが必須です。");
        }
    }
}
