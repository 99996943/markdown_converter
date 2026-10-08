namespace LegalAgent.PdfParser.Model;

/// <summary>Kind of a section.</summary>
public enum SectionKind
{
    /// <summary>Document title.</summary>
    DocumentTitle,

    /// <summary>Book (Ksiega).</summary>
    Book,

    /// <summary>Part (Czesc).</summary>
    Part,

    /// <summary>Division (Dzial).</summary>
    Division,

    /// <summary>Chapter (Rozdzial).</summary>
    Chapter,

    /// <summary>Subchapter (Oddzial).</summary>
    Subchapter,

    /// <summary>Article (Art.).</summary>
    Article,

    /// <summary>Paragraph (par.).</summary>
    Paragraph,

    /// <summary>Heading recognised only typographically.</summary>
    Typographic,

    /// <summary>
    /// Section of a table-document (spec 002, FR-083): the heading is the name in the left cell of a two-column bordered
    /// table that makes up the document, the content the right cells, merged across rows and pages.
    /// </summary>
    TableDocumentSection,
}

/// <summary>A section of the document with its heading, content and subsections.</summary>
/// <param name="Level">Markdown heading level, 1-6.</param>
/// <param name="Kind">Kind of the section.</param>
/// <param name="Designation">Literal designation such as "Rozdzial 3" or "Art. 12a"; null for typographic headings.</param>
/// <param name="Number">The number alone, such as "3", "12a", "II".</param>
/// <param name="Title">Unit title; null when absent.</param>
/// <param name="HeadingText">Full heading text rendered after the hash marks.</param>
/// <param name="Path">Heading texts of the ancestors and the section itself.</param>
/// <param name="Pages">Source pages from the heading to the end of the content (including subsections).</param>
/// <param name="Blocks">Own content, before subsections.</param>
/// <param name="Footnotes">Footnote definitions assigned to this section.</param>
/// <param name="Children">Subsections.</param>
public sealed record Section(
    int Level,
    SectionKind Kind,
    string? Designation,
    string? Number,
    string? Title,
    string HeadingText,
    IReadOnlyList<string> Path,
    PageRange Pages,
    IReadOnlyList<ContentBlock> Blocks,
    IReadOnlyList<Footnote> Footnotes,
    IReadOnlyList<Section> Children);
