namespace LegalAgent.Corpus.Composition;

/// <summary>Typographic style of an inline run.</summary>
public enum InlineStyle
{
    /// <summary>Regular face.</summary>
    Regular,

    /// <summary>Bold face.</summary>
    Bold,

    /// <summary>Italic face.</summary>
    Italic,
}

/// <summary>What an inline run stands for.</summary>
public enum InlineKind
{
    /// <summary>Plain text.</summary>
    Text,

    /// <summary>
    /// Footnote reference; <see cref="Inline.Text"/> is the footnote key (in a block: the local key from
    /// <c>[^n]</c>; in a composed document: the document-wide number). Rendered as superscript digits glued to
    /// the preceding text.
    /// </summary>
    FootnoteRef,

    /// <summary>
    /// Deferred cross reference (<c>{{ref:…}}</c>); <see cref="Inline.Text"/> is the target (e.g. <c>blok:reklamacje</c>).
    /// Resolved by the composer to plain text after units are numbered.
    /// </summary>
    Reference,
}

/// <summary>A run of text with one style.</summary>
/// <param name="Text">The text (or footnote key / reference target, see <see cref="Kind"/>).</param>
/// <param name="Style">Typographic style.</param>
/// <param name="Kind">Plain text, footnote reference or deferred reference.</param>
public sealed record Inline(string Text, InlineStyle Style = InlineStyle.Regular, InlineKind Kind = InlineKind.Text)
{
    /// <summary>Concatenated plain text of <paramref name="runs"/> (footnote references as superscript digits).</summary>
    public static string PlainText(IEnumerable<Inline> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var sb = new System.Text.StringBuilder();
        foreach (Inline run in runs)
        {
            sb.Append(run.Kind == InlineKind.FootnoteRef ? Superscript(run.Text) : run.Text);
        }

        return sb.ToString();
    }

    /// <summary>Superscript form of a footnote number, e.g. "12" → "¹²".</summary>
    public static string Superscript(string number)
    {
        ArgumentNullException.ThrowIfNull(number);
        var sb = new System.Text.StringBuilder(number.Length);
        foreach (char c in number)
        {
            sb.Append(c switch
            {
                '0' => '⁰',
                '1' => '¹',
                '2' => '²',
                '3' => '³',
                >= '4' and <= '9' => (char)('⁴' + (c - '4')),
                _ => c,
            });
        }

        return sb.ToString();
    }
}

/// <summary>Base of the composed document tree.</summary>
public abstract record Element
{
    /// <summary>Identifier stable within the document (used for page tracking in the manifest).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Source block, when the element comes from a content block.</summary>
    public string? BlockId { get; init; }

    /// <summary>Unit designation for citations, e.g. "§ 12 ust. 3" or "poz. 4.7"; null when not citable.</summary>
    public string? Unit { get; init; }

    /// <summary>True when the element was inserted or changed by a poison operation.</summary>
    public bool Poison { get; init; }
}

/// <summary>
/// Heading. <see cref="Level"/> 2 = chapter / top-level section, 3 = paragraph unit ("§ N.") or subsection
/// (1 is reserved for the document title, which comes from <see cref="FrontMatter"/>). <see cref="Label"/> is
/// the original designation ("Rozdział 3", "§ 12.", "4.", "Załącznik nr 1"), printed before or above the text.
/// </summary>
public sealed record HeadingElement(int Level, string? Label, IReadOnlyList<Inline> Text) : Element;

/// <summary>Body paragraph.</summary>
public sealed record ParagraphElement(IReadOnlyList<Inline> Text) : Element;

/// <summary>List item with its original label ("1.", "1)", "a)", "4.1.") and nesting depth (0 = top).</summary>
public sealed record ListItemElement(string Label, int Depth, IReadOnlyList<Inline> Text) : Element;

/// <summary>A table cell.</summary>
public sealed record TableCell(IReadOnlyList<Inline> Text)
{
    /// <summary>A cell holding plain text.</summary>
    public static TableCell Of(string text) => new([new Inline(text)]);
}

/// <summary>A column of a table: header text and relative width (weights are normalised to the text width).</summary>
public sealed record TableColumn(string Header, double Weight);

/// <summary>
/// Table (tariff grid, ordinary table). <see cref="Grid"/> null = the layout style decides.
/// <see cref="Notes"/> are footnotes printed under the table (each starts with its own marker, e.g. "1) …").
/// Rows whose first cell is the only non-empty one and <see cref="TableCell"/> spans are not supported: a
/// segment name is a heading outside the table.
/// </summary>
public sealed record TableElement(
    IReadOnlyList<TableColumn> Columns,
    IReadOnlyList<IReadOnlyList<TableCell>> Rows,
    bool? Grid,
    IReadOnlyList<IReadOnlyList<Inline>> Notes) : Element;

/// <summary>Key–value table (procedure record card / metryczka) drawn with a grid.</summary>
public sealed record KeyValueTableElement(IReadOnlyList<KeyValuePair<string, IReadOnlyList<Inline>>> Pairs) : Element;

/// <summary>One step of a step scheme (FR-067): gray box with the name, explanation on the right.</summary>
public sealed record SchemeStep(string Name, IReadOnlyList<IReadOnlyList<Inline>> Explanation);

/// <summary>Step scheme with arrows between steps (FR-067).</summary>
public sealed record StepSchemeElement(IReadOnlyList<SchemeStep> Steps) : Element;

/// <summary>Form of a checklist.</summary>
public enum ChecklistForm
{
    /// <summary>Vector-drawn empty box before the item text.</summary>
    Vector,

    /// <summary>"□" set in the monospace face before the item text.</summary>
    Text,

    /// <summary>Grid table "Lp. | Czynność | Wykonano" with empty boxes.</summary>
    Table,
}

/// <summary>Checklist.</summary>
public sealed record ChecklistElement(ChecklistForm Form, IReadOnlyList<IReadOnlyList<Inline>> Items) : Element;

/// <summary>Framed box (callout) with a paragraph of text.</summary>
public sealed record CalloutElement(IReadOnlyList<Inline> Text) : Element;

/// <summary>Forces the following content onto a new page (e.g. before an annex).</summary>
public sealed record PageBreakElement : Element;

/// <summary>A row of the change history of a procedure record card.</summary>
public sealed record HistoryEntry(string Version, string Date, string Description);

/// <summary>
/// Front matter shown on the cover and/or record card (FR-113). Dates are already formatted
/// ("1 stycznia 2027 r."). Fields not used by the template are null.
/// </summary>
public sealed record FrontMatter(string Bank, string Title, string Designation, int Version, string ValidFrom, string? ValidTo)
{
    /// <summary>Whether a cover page is printed.</summary>
    public bool Cover { get; init; } = true;

    /// <summary>Whether a record card (metryczka) is printed.</summary>
    public bool RecordCard { get; init; }

    /// <summary>Owner unit (metryczka).</summary>
    public string? Owner { get; init; }

    /// <summary>Who approved (metryczka).</summary>
    public string? ApprovedBy { get; init; }

    /// <summary>Approval date (metryczka).</summary>
    public string? ApprovalDate { get; init; }

    /// <summary>Change history (metryczka).</summary>
    public IReadOnlyList<HistoryEntry> History { get; init; } = [];

    /// <summary>Text inserted on the cover by a poison operation (FR-132), shown as an ordinary cover line.</summary>
    public IReadOnlyList<Inline>? CoverNote { get; init; }
}

/// <summary>Composed document ready for typesetting.</summary>
/// <param name="Id">Document id ("REG-03").</param>
/// <param name="Layout">Layout style id (<c>jedna-kolumna</c>, …).</param>
/// <param name="Front">Front matter.</param>
/// <param name="Elements">Body elements in reading order.</param>
/// <param name="Footnotes">Footnote texts by document-wide number.</param>
public sealed record ComposedDocument(
    string Id,
    string Layout,
    FrontMatter Front,
    IReadOnlyList<Element> Elements,
    IReadOnlyDictionary<int, IReadOnlyList<Inline>> Footnotes);
