namespace LegalAgent.Corpus.Typesetting;

/// <summary>
/// Geometry and typography of one layout style (research R6): page size, margins, columns, font sizes,
/// running header and footer. Vertical coordinates are distances from the top edge of the page.
/// </summary>
public sealed record LayoutStyle
{
    /// <summary>Style id (<c>jedna-kolumna</c>, <c>dwie-kolumny</c>, …).</summary>
    public required string Id { get; init; }

    /// <summary>Page width in points.</summary>
    public double PageWidth { get; init; } = 595;

    /// <summary>Page height in points.</summary>
    public double PageHeight { get; init; } = 842;

    /// <summary>Left edge of the text area.</summary>
    public double Left { get; init; } = 72;

    /// <summary>Right edge of the text area.</summary>
    public double Right { get; init; } = 523;

    /// <summary>Baseline of the first body line.</summary>
    public double Top { get; init; } = 92;

    /// <summary>Lowest baseline of body text (footnotes are placed between this and the footer).</summary>
    public double Bottom { get; init; } = 752;

    /// <summary>Number of body text columns (1 or 2).</summary>
    public int Columns { get; init; } = 1;

    /// <summary>Gap between columns.</summary>
    public double ColumnGap { get; init; } = 24;

    /// <summary>Body font size.</summary>
    public double BodySize { get; init; } = 10.5;

    /// <summary>Distance between body baselines.</summary>
    public double Leading { get; init; } = 14;

    /// <summary>Extra space after a paragraph or list item.</summary>
    public double ParagraphGap { get; init; } = 5;

    /// <summary>Indentation step of nested list levels.</summary>
    public double ListIndent { get; init; } = 18;

    /// <summary>Document title size (cover or first page).</summary>
    public double TitleSize { get; init; } = 18;

    /// <summary>Level-2 heading size (chapters, top-level sections).</summary>
    public double ChapterSize { get; init; } = 13;

    /// <summary>Level-3 heading size (units such as "§ N.").</summary>
    public double UnitSize { get; init; } = 11;

    /// <summary>Whether level-3 unit headings are centred on the column.</summary>
    public bool CenterUnits { get; init; } = true;

    /// <summary>Whether a level-2 heading with a label prints the label and the text on two lines.</summary>
    public bool ChapterOnTwoLines { get; init; }

    /// <summary>Footnote font size.</summary>
    public double FootnoteSize { get; init; } = 8;

    /// <summary>Footnote leading.</summary>
    public double FootnoteLeading { get; init; } = 10;

    /// <summary>Running header/footer font size.</summary>
    public double MarginSize { get; init; } = 8;

    /// <summary>Baseline of the running header.</summary>
    public double HeaderBaseline { get; init; } = 42;

    /// <summary>Baseline of the running footer.</summary>
    public double FooterBaseline { get; init; } = 805;

    /// <summary>
    /// Running header; placeholders <c>{tytul}</c>, <c>{bank}</c>, <c>{oznaczenie}</c>, <c>{n}</c>, <c>{N}</c>.
    /// Null = no header.
    /// </summary>
    public string? HeaderFormat { get; init; } = "{tytul} – {bank}";

    /// <summary>Running footer at the left margin (same placeholders); null = none.</summary>
    public string? FooterLeftFormat { get; init; } = "{oznaczenie}";

    /// <summary>Running footer at the right margin (same placeholders); null = none.</summary>
    public string? FooterRightFormat { get; init; } = "Strona {n} z {N}";

    /// <summary>Whether tables are drawn with a grid when the table does not decide itself.</summary>
    public bool TableGrid { get; init; } = true;

    /// <summary>Table font size.</summary>
    public double TableSize { get; init; } = 9;

    /// <summary>Table line leading.</summary>
    public double TableLeading { get; init; } = 11.5;

    /// <summary>Whether the body is set as one two-column table-document (FR-080).</summary>
    public bool TableDocument { get; init; }
}
