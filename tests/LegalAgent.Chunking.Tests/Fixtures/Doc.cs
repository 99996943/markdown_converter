using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Rendering;

namespace LegalAgent.Chunking.Tests.Fixtures;

/// <summary>
/// Builds <see cref="LegalDocument"/> models in code so unit tests do not need PDFs. <see cref="Document"/>
/// recomputes every <see cref="Section.Path"/> from the heading texts, so tests only state the tree.
/// </summary>
public static class Doc
{
    /// <summary>A plain text run.</summary>
    public static TextRun Text(string text, TextStyle style = TextStyle.None) => new(text, style);

    /// <summary>A footnote reference.</summary>
    public static FootnoteRef Ref(int number) => new(number);

    /// <summary>A page break before the following text.</summary>
    public static PageBreak Break(int page) => new(page);

    /// <summary>A paragraph on one page.</summary>
    public static ParagraphBlock Para(string text, int page = 1) => new(new PageRange(page, page), [Text(text)]);

    /// <summary>A paragraph spanning pages, with explicit inlines.</summary>
    public static ParagraphBlock Para(int first, int last, params Inline[] inlines) => new(new PageRange(first, last), inlines);

    /// <summary>A list item; children are nested lists or paragraphs.</summary>
    public static ListItem Item(string label, ListLabelKind kind, string text, params ContentBlock[] children) =>
        new(label, kind, [Text(text)], children);

    /// <summary>A list item with explicit inlines.</summary>
    public static ListItem Item(string label, ListLabelKind kind, IReadOnlyList<Inline> inlines, params ContentBlock[] children) =>
        new(label, kind, inlines, children);

    /// <summary>A list.</summary>
    public static ListBlock List(int first, int last, params ListItem[] items) => new(new PageRange(first, last), items);

    /// <summary>A table row of plain-text cells.</summary>
    public static TableRow Row(params string[] cells) => new(cells.Select(c => new TableCell(c.Length == 0 ? [] : [Text(c)])).ToList());

    /// <summary>A GFM table.</summary>
    public static TableBlock Table(int first, int last, TableRow? header, params TableRow[] rows) =>
        new(new PageRange(first, last), header, rows, (header ?? rows[0]).Cells.Count, IsFallback: false);

    /// <summary>A fallback table.</summary>
    public static TableBlock FallbackTable(int first, int last, params TableRow[] rows) =>
        new(new PageRange(first, last), null, rows, rows[0].Cells.Count, IsFallback: true);

    /// <summary>A footnote definition.</summary>
    public static Footnote Footnote(int number, string text, int page = 1) =>
        new(number, number.ToString(System.Globalization.CultureInfo.InvariantCulture), [Text(text)], page, IsOrphan: false);

    /// <summary>A section; <paramref name="heading"/> is the full heading text.</summary>
    public static Section Section(
        int level,
        SectionKind kind,
        string? designation,
        string heading,
        int first,
        int last,
        IReadOnlyList<ContentBlock>? blocks = null,
        IReadOnlyList<Footnote>? footnotes = null,
        IReadOnlyList<Section>? children = null) =>
        new(
            level,
            kind,
            designation,
            designation?.Split(' ').Last(),
            null,
            heading,
            [],
            new PageRange(first, last),
            blocks ?? [],
            footnotes ?? [],
            children ?? []);

    /// <summary>A paragraph unit "§ N." at level 3 on the given pages.</summary>
    public static Section Paragraph(int number, int first, int last, params ContentBlock[] blocks) =>
        Section(3, SectionKind.Paragraph, $"§ {number}", $"§ {number}.", first, last, blocks);

    /// <summary>A document with paths recomputed from the heading texts.</summary>
    public static LegalDocument Document(
        string? title,
        IReadOnlyList<ContentBlock>? preamble,
        params Section[] sections) =>
        new(
            new SourceInfo("test.pdf", Math.Max(1, MaxPage(sections, preamble ?? [])), title, 1, new string('0', 64)),
            title,
            preamble ?? [],
            [],
            sections.Select(s => WithPaths(s, [])).ToList());

    /// <summary>A complete parser result for <paramref name="document"/>, with the Markdown of the parser's renderer.</summary>
    public static PdfConversionResult Result(LegalDocument document, IReadOnlyList<SkippedPage>? skipped = null) =>
        new(
            document,
            new MarkdownRenderer().Render(document),
            new ConversionReport(
                document.Source.PageCount,
                skipped ?? [],
                [],
                new Dictionary<int, int>(),
                0,
                0,
                0,
                0,
                0,
                [],
                TimeSpan.Zero),
            IsComplete: skipped is null || skipped.Count == 0);

    private static Section WithPaths(Section section, IReadOnlyList<string> parent)
    {
        List<string> path = [.. parent, section.HeadingText];
        return section with { Path = path, Children = section.Children.Select(c => WithPaths(c, path)).ToList() };
    }

    private static int MaxPage(IEnumerable<Section> sections, IEnumerable<ContentBlock> preamble) =>
        Math.Max(
            preamble.Select(b => b.Pages.Last).DefaultIfEmpty(1).Max(),
            sections.Select(s => Math.Max(s.Pages.Last, MaxPage(s.Children, []))).DefaultIfEmpty(1).Max());
}
