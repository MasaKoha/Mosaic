using System;
using System.Linq;
using GameMockStudio.Brief;
using GameMockStudio.Brief.Discovery;
using GameMockStudio.Brief.Planning;

namespace GameMockStudio.Cli;

/// <summary>GUIと共通の種類・項目・内蔵案をターミナルへ公開する。</summary>
internal sealed class CatalogCommands(FieldCatalogs catalogs, CliJson output)
{
    /// <summary>選択したカタログ情報をJSONで出力する。</summary>
    public void Execute(CliArguments arguments)
    {
        switch (arguments.Command)
        {
            case "kinds":
                output.Write(Enum.GetValues<MockKind>().Select(DescribeKind).ToArray());
                break;
            case "fields":
                output.Write(catalogs.ForKind(arguments.GetKind()).Categories);
                break;
            case "ideas":
                output.Write(new IdeaCatalog(catalogs).ForKind(arguments.GetKind()).Select((idea, position) => new
                {
                    Number = position + 1, idea.Title, idea.Description, idea.Genres, idea.Values
                }).ToArray());
                break;
        }
    }

    private object DescribeKind(MockKind kind)
    {
        var catalog = catalogs.ForKind(kind);
        var formats = Enum.GetValues<MockFormat>()
            .Where(format => kind != MockKind.Service || format != MockFormat.Unity).ToArray();
        return new { Kind = kind, catalog.Label, CategoryCount = catalog.Categories.Count, FieldCount = catalog.Fields.Count, Formats = formats };
    }
}
