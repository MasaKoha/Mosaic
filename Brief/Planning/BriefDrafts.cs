using System;
using System.Collections.Generic;
using System.Linq;

namespace GameMockStudio.Brief.Planning;

/// <summary>対象の切り替えで別領域の企画を失わないよう、種類ごとの下書きを保持する。</summary>
public sealed class BriefDrafts
{
    private readonly Dictionary<MockKind, BriefDocument> documents = new();

    /// <summary>編集中の企画を独立した下書きとして記憶する。</summary>
    public void Remember(BriefDocument document)
    {
        documents[document.Kind] = document.Normalize();
    }

    /// <summary>その種類の下書きがなければ空の企画から始める。</summary>
    public BriefDocument ForKind(MockKind kind)
    {
        return documents.GetValueOrDefault(kind, new BriefDocument { Kind = kind }).Normalize();
    }

    /// <summary>最新の入力を含めた全下書きを保存用に取り出す。</summary>
    public BriefDocument[] Capture(BriefDocument active)
    {
        Remember(active);
        return documents.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray();
    }

    /// <summary>旧セッションの現在企画も含めて、重複なく復元する。</summary>
    public void Restore(IEnumerable<BriefDocument> saved, BriefDocument active)
    {
        documents.Clear();
        foreach (var document in saved)
        {
            if (document is null || documents.ContainsKey(document.Kind))
            {
                throw new InvalidOperationException("保存された種類別の下書きが不正です。");
            }
            Remember(document);
        }
        Remember(active);
    }
}
