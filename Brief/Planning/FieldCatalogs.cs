using System;
using System.Collections.Generic;
using System.Linq;

namespace GameMockStudio.Brief.Planning;

/// <summary>企画の種類に対応する項目定義を共有する。</summary>
public sealed class FieldCatalogs
{
    private readonly Dictionary<MockKind, FieldCatalog> catalogs = Enum.GetValues<MockKind>()
        .ToDictionary(kind => kind, kind => new FieldCatalog(kind));

    /// <summary>保存された種類と一致するカタログを返す。</summary>
    public FieldCatalog ForKind(MockKind kind)
    {
        return catalogs.TryGetValue(kind, out var catalog) ? catalog
            : throw new InvalidOperationException("対応していないモックの種類です。");
    }
}
