namespace LegalAgent.Corpus.Typesetting;

/// <summary>The six layout styles of the corpus (research R6); the list is fixed in code.</summary>
public static class LayoutStyles
{
    /// <summary>Style ids in a stable order.</summary>
    public static IReadOnlyList<string> Ids { get; } = ["jedna-kolumna", "dwie-kolumny", "tabela-dokument", "taryfa-siatka", "taryfa-bez-siatki", "procedura"];

    /// <summary>The style with id <paramref name="id"/>.</summary>
    public static LayoutStyle Get(string id) => throw new NotImplementedException();
}
