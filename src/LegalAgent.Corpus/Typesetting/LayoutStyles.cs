namespace LegalAgent.Corpus.Typesetting;

/// <summary>The six layout styles of the corpus (research R6); the list is fixed in code.</summary>
public static class LayoutStyles
{
    private static readonly Dictionary<string, LayoutStyle> Styles = new(StringComparer.Ordinal)
    {
        ["jedna-kolumna"] = new LayoutStyle { Id = "jedna-kolumna" },
        ["dwie-kolumny"] = new LayoutStyle
        {
            Id = "dwie-kolumny",
            Left = 50,
            Right = 545,
            Columns = 2,
            ColumnGap = 31,
            BodySize = 9.5,
            Leading = 12.5,
            TitleSize = 16,
            ChapterSize = 12,
            UnitSize = 10.5,
            HeaderFormat = "{tytul}",
            FooterLeftFormat = "{bank}",
            FooterRightFormat = "Strona {n} z {N}",
        },
        ["tabela-dokument"] = new LayoutStyle
        {
            Id = "tabela-dokument",
            Left = 54,
            Right = 541,
            Top = 92,
            BodySize = 10,
            Leading = 15,
            TitleSize = 20,
            HeaderFormat = "{tytul}",
            FooterLeftFormat = null,
            FooterRightFormat = "{n}/{N}",
            TableDocument = true,
        },
        ["taryfa-siatka"] = new LayoutStyle
        {
            Id = "taryfa-siatka",
            Left = 56,
            Right = 539,
            BodySize = 10,
            Leading = 13.5,
            TableGrid = true,
            TableSize = 9,
            ChapterSpaceAbove = 26,
            HeaderFormat = "{tytul} – {bank}",
            FooterLeftFormat = "{oznaczenie}",
            FooterRightFormat = "Strona {n} z {N}",
        },
        ["taryfa-bez-siatki"] = new LayoutStyle
        {
            Id = "taryfa-bez-siatki",
            Left = 64,
            Right = 531,
            BodySize = 10,
            Leading = 13.5,
            TableGrid = false,
            TableSize = 9.5,
            ChapterSpaceAbove = 26,
            TableLeading = 12,
            HeaderFormat = "{bank}",
            FooterLeftFormat = "{tytul}",
            FooterRightFormat = "{n} / {N}",
        },
        ["procedura"] = new LayoutStyle
        {
            Id = "procedura",
            Left = 70,
            Right = 525,
            BodySize = 10.5,
            Leading = 14.5,
            CenterUnits = false,
            ChapterSize = 12.5,
            HeaderFormat = "{oznaczenie} – {tytul}",
            FooterLeftFormat = "Dokument wewnętrzny – {bank}",
            FooterRightFormat = "Strona {n} z {N}",
        },
    };

    /// <summary>Style ids in a stable order.</summary>
    public static IReadOnlyList<string> Ids { get; } = ["jedna-kolumna", "dwie-kolumny", "tabela-dokument", "taryfa-siatka", "taryfa-bez-siatki", "procedura"];

    /// <summary>The style with id <paramref name="id"/>.</summary>
    public static LayoutStyle Get(string id) =>
        Styles.TryGetValue(id, out LayoutStyle? style) ? style : throw new ArgumentException("Nieznany styl układu: " + id, nameof(id));
}
