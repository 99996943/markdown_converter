using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Truth;

namespace LegalAgent.Corpus.Typesetting;

/// <summary>First and last page (1-based) on which an element is printed.</summary>
public readonly record struct PageSpan(int First, int Last);

/// <summary>Result of typesetting one composed document.</summary>
/// <param name="Pdf">PDF bytes (with a deterministic /ID).</param>
/// <param name="PageCount">Number of pages.</param>
/// <param name="Truth">Reference truth recorded while typesetting.</param>
/// <param name="ElementPages">Pages of every element with a non-empty id.</param>
/// <param name="BlockWordCounts">Number of printed words per source block.</param>
public sealed record TypesetResult(
    byte[] Pdf,
    int PageCount,
    DocumentTruth Truth,
    IReadOnlyDictionary<string, PageSpan> ElementPages,
    IReadOnlyDictionary<string, int> BlockWordCounts);

/// <summary>
/// Lays out a <see cref="ComposedDocument"/> on pages with <see cref="Pdf.SyntheticPdfBuilder"/> according to a
/// <see cref="LayoutStyle"/> (research R6) and records the reference truth (FR-106). Two passes: the first counts
/// the pages so the footer can state "Strona n z N".
/// </summary>
public static class Typesetter
{
    private const int FirstWordsInTruth = 6;
    private const double ItemGap = 3;

    /// <summary>Typesets <paramref name="document"/> in <paramref name="style"/>.</summary>
    public static TypesetResult Typeset(ComposedDocument document, LayoutStyle style)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(style);

        int pages = Pass(document, style, 0).Page;
        PageWriter writer = Pass(document, style, pages);
        return new TypesetResult(writer.Builder.Build(), writer.Page, writer.Truth, writer.ElementPages, writer.BlockWords);
    }

    private static PageWriter Pass(ComposedDocument document, LayoutStyle style, int totalPages)
    {
        var w = new PageWriter(style, document.Front, document.Footnotes, totalPages);
        FrontPages(w, document.Front);
        if (document.Front.RecordCard)
        {
            RecordCard(w, document.Front);
        }

        TableDocumentLayout? tableDocument = style.TableDocument ? new TableDocumentLayout(w) : null;
        w.StartColumns(tableDocument is null ? style.Columns : 1);
        foreach (Element element in document.Elements)
        {
            w.Begin(element);
            Element(w, element, tableDocument);
        }

        tableDocument?.FlushNames();
        w.Finish();
        return w;
    }

    private static void FrontPages(PageWriter w, FrontMatter front)
    {
        LayoutStyle s = w.Style;
        double width = s.Right - s.Left;
        w.Truth.Headings.Add(new TruthHeading(1, null, front.Title));
        string validity = "obowiązuje od " + front.ValidFrom + (front.ValidTo is null ? string.Empty : " do " + front.ValidTo);

        if (front.Cover)
        {
            // The title is the first line of the document (the parser takes the first enlarged line as the title).
            w.NewPage(margins: false);
            double y = 230;
            foreach (SetLine line in TextMeasure.Wrap(Tokens(front.Title, InlineStyle.Bold), width, s.TitleSize + 4))
            {
                w.Draw(line, s.Left, y, s.TitleSize + 4);
                y += (s.TitleSize + 4) * 1.3;
            }

            y += 40;
            foreach (string text in new[] { front.Bank, front.Designation, "Wersja " + Number(front.Version), Capitalize(validity) })
            {
                w.Text(s.Left, y, text, s.BodySize + 1);
                y += s.Leading + 2;
            }

            if (front.CoverNote is { } note)
            {
                y += 20;
                foreach (SetLine line in TextMeasure.Wrap(TextMeasure.Tokenize(note), width, s.BodySize))
                {
                    w.Draw(line, s.Left, y, s.BodySize);
                    y += s.Leading;
                }
            }

            w.NewPage();
            return;
        }

        w.NewPage();
        foreach (SetLine line in TextMeasure.Wrap(Tokens(front.Title, InlineStyle.Bold), width, s.TitleSize))
        {
            w.Draw(line, s.Left, w.Y, s.TitleSize);
            w.Y += s.TitleSize * 1.3;
        }

        w.Y += 4;
        string meta = front.Bank + ", " + front.Designation + ", wersja " + Number(front.Version) + ", " + validity;
        foreach (SetLine line in TextMeasure.Wrap(Tokens(meta, InlineStyle.Italic), width, s.BodySize))
        {
            w.Draw(line, s.Left, w.Y, s.BodySize);
            w.Y += s.Leading;
        }

        w.Y += 16;
    }

    /// <summary>The record card (metryczka) of a procedure: a key–value grid and the change history.</summary>
    private static void RecordCard(PageWriter w, FrontMatter front)
    {
        var pairs = new List<KeyValuePair<string, IReadOnlyList<Inline>>>();
        void Add(string key, string? value)
        {
            if (value is not null)
            {
                pairs.Add(new(key, [new Inline(value)]));
            }
        }

        Add("Oznaczenie", front.Designation);
        Add("Wersja", Number(front.Version));
        Add("Właściciel", front.Owner);
        Add("Zatwierdził", front.ApprovedBy);
        Add("Data zatwierdzenia", front.ApprovalDate);
        Add("Obowiązuje od", front.ValidFrom);
        Add("Obowiązuje do", front.ValidTo);
        w.Begin(new KeyValueTableElement(pairs) { Id = "metryczka" });
        TableLayout.KeyValue(w, new KeyValueTableElement(pairs));

        if (front.History.Count > 0)
        {
            // An isolated bold line: a heading of the record card (the parser makes it one, so does the truth).
            w.Truth.Headings.Add(new TruthHeading(2, null, "Historia zmian"));
            Paragraph(w, [new Inline("Historia zmian", InlineStyle.Bold)]);
            TableLayout.Table(w, new TableElement(
                [new TableColumn("Wersja", 0.8), new TableColumn("Data", 1.6), new TableColumn("Opis zmian", 5)],
                front.History.Select(h => (IReadOnlyList<TableCell>)[TableCell.Of(h.Version), TableCell.Of(h.Date), TableCell.Of(h.Description)]).ToList(),
                Grid: true,
                Notes: []));
        }

        w.Y += 10;
    }

    private static string Number(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static void Element(PageWriter w, Element element, TableDocumentLayout? tableDocument)
    {
        // Items of one list are close together; anything else after a list keeps a paragraph gap from it.
        if (w.AfterListItem && element is not ListItemElement)
        {
            w.Y += w.Style.ParagraphGap - ItemGap;
        }

        w.AfterListItem = false;
        switch (element)
        {
            case HeadingElement { Level: <= 2 } h when tableDocument is not null:
                // A table-document row is named by its section title alone (no „Rozdział N”).
                tableDocument.Section(h.Text.Count > 0 ? h with { Label = null } : h);
                break;
            case HeadingElement h when tableDocument is not null:
                // Units („§ N.”) are not printed in a table-document; a titled subheading is a bold paragraph.
                if (h.Text.Count > 0)
                {
                    Paragraph(w, [new Inline(Inline.PlainText(h.Text), InlineStyle.Bold)]);
                }

                break;
            case HeadingElement h:
                Heading(w, h);
                break;
            case ParagraphElement p:
                Paragraph(w, p.Text);
                break;
            case ListItemElement li:
                ListItem(w, li);
                break;
            case TableElement t:
                TableLayout.Table(w, t);
                break;
            case KeyValueTableElement kv:
                TableLayout.KeyValue(w, kv);
                break;
            case StepSchemeElement scheme:
                StepSchemeLayout.Scheme(w, scheme);
                break;
            case ChecklistElement list:
                ChecklistLayout.Checklist(w, list);
                break;
            case CalloutElement callout:
                ChecklistLayout.Callout(w, callout);
                break;
            case PageBreakElement:
                if (!(w.AtTopOfColumn && w.Column == 0))
                {
                    int columns = w.Columns;
                    w.NewPage();
                    w.StartColumns(columns);
                }

                break;
            default:
                throw new NotSupportedException("Element not supported yet: " + element.GetType().Name);
        }
    }

    private static void Heading(PageWriter w, HeadingElement h)
    {
        LayoutStyle s = w.Style;
        string text = Inline.PlainText(h.Text);
        w.Truth.Headings.Add(new TruthHeading(h.Level, h.Label, text));
        bool unit = h.Level >= 3;
        double size = unit ? s.UnitSize : s.ChapterSize;
        double leading = size * 1.35;

        var lines = new List<SetLine>();
        if (h.Label is { Length: > 0 } label && text.Length > 0 && !label.EndsWith('.'))
        {
            lines.AddRange(TextMeasure.Wrap(Tokens(label, InlineStyle.Bold), w.ColumnWidth, size));
            lines.AddRange(TextMeasure.Wrap(TextMeasure.Tokenize(Bold(h.Text)), w.ColumnWidth, size));
        }
        else
        {
            var runs = new List<Inline>();
            if (h.Label is { Length: > 0 })
            {
                runs.Add(new Inline(h.Label + (text.Length > 0 ? " " : string.Empty), InlineStyle.Bold));
            }

            runs.AddRange(Bold(h.Text));
            lines.AddRange(TextMeasure.Wrap(TextMeasure.Tokenize(runs), w.ColumnWidth, size));
        }

        if (!w.AtTopOfColumn)
        {
            w.Y += unit ? 4 : s.ChapterSpaceAbove;
        }

        // Keep the heading with at least two lines of what follows.
        w.Need((lines.Count * leading) + (2 * s.Leading));
        foreach (SetLine line in lines)
        {
            double y = w.Place(line.Tokens, leading);
            double x = w.ColumnLeft;
            if (unit && s.CenterUnits)
            {
                x += (w.ColumnWidth - TextMeasure.LineWidth(line.Tokens, size)) / 2;
            }

            w.Draw(line, x, y, size);
        }

        w.Y += 6;
    }

    private static void Paragraph(PageWriter w, IReadOnlyList<Inline> text)
    {
        LayoutStyle s = w.Style;
        foreach (SetLine line in TextMeasure.Wrap(TextMeasure.Tokenize(text), w.ColumnWidth, s.BodySize))
        {
            double y = w.Place(line.Tokens, s.Leading);
            w.Draw(line, w.ColumnLeft, y, s.BodySize);
        }

        w.Y += s.ParagraphGap;
    }

    private static void ListItem(PageWriter w, ListItemElement item)
    {
        LayoutStyle s = w.Style;
        double labelX = item.Depth * s.ListIndent;
        double textX = labelX + Math.Max(s.ListIndent, TextMeasure.Width(item.Label, s.BodySize) + 5);
        w.Truth.ListItems.Add(new TruthListItem(item.Label, item.Depth, FirstWords(item.Text)));

        List<SetLine> lines = TextMeasure.Wrap(TextMeasure.Tokenize(item.Text), w.ColumnWidth - textX, s.BodySize);
        for (int i = 0; i < lines.Count; i++)
        {
            double y = w.Place(lines[i].Tokens, s.Leading);
            if (i == 0)
            {
                w.Text(w.ColumnLeft + labelX, y, item.Label, s.BodySize);
            }

            w.Draw(lines[i], w.ColumnLeft + textX, y, s.BodySize);
        }

        w.Y += ItemGap;
        w.AfterListItem = true;
    }

    internal static string FirstWords(IReadOnlyList<Inline> text) =>
        string.Join(' ', Inline.PlainText(text.Where(r => r.Kind != InlineKind.FootnoteRef)).Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(FirstWordsInTruth));

    private static List<Token> Tokens(string text, InlineStyle style) => TextMeasure.Tokenize([new Inline(text, style)]);

    private static IEnumerable<Inline> Bold(IReadOnlyList<Inline> text) =>
        text.Select(r => r.Kind == InlineKind.Text ? r with { Style = InlineStyle.Bold } : r);

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
