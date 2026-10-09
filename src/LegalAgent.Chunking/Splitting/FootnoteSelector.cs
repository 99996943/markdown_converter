using LegalAgent.PdfParser.Model;

namespace LegalAgent.Chunking.Splitting;

/// <summary>
/// Chooses the footnote definitions of a part (research R5, FR-232): those referenced from the part's content, and —
/// in the part holding the unit's last atom — those the unit never references. Ordered by number.
/// </summary>
internal static class FootnoteSelector
{
    /// <summary>Footnotes of <paramref name="unitFootnotes"/> for a part made of <paramref name="blocks"/>.</summary>
    /// <param name="blocks">Content of the part.</param>
    /// <param name="unitFootnotes">Footnote definitions of the unit.</param>
    /// <param name="unitReferences">Footnote numbers referenced anywhere in the unit.</param>
    /// <param name="withUnreferenced">True for the part holding the unit's last atom.</param>
    public static IReadOnlyList<Footnote> Select(
        IReadOnlyList<ContentBlock> blocks,
        IReadOnlyList<Footnote> unitFootnotes,
        IReadOnlySet<int> unitReferences,
        bool withUnreferenced)
    {
        if (unitFootnotes.Count == 0)
        {
            return [];
        }

        HashSet<int> references = References(blocks);
        return unitFootnotes
            .Where(f => references.Contains(f.Number) || (withUnreferenced && !unitReferences.Contains(f.Number)))
            .OrderBy(f => f.Number)
            .ToList();
    }

    /// <summary>Footnote numbers referenced from <paramref name="blocks"/>: paragraphs, list items at any depth, table cells.</summary>
    public static HashSet<int> References(IEnumerable<ContentBlock> blocks)
    {
        var numbers = new HashSet<int>();
        foreach (ContentBlock block in blocks)
        {
            Collect(block, numbers);
        }

        return numbers;
    }

    private static void Collect(ContentBlock block, HashSet<int> numbers)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                Collect(paragraph.Inlines, numbers);
                break;

            case ListBlock list:
                foreach (ListItem item in list.Items)
                {
                    Collect(item.Inlines, numbers);
                    foreach (ContentBlock child in item.Children)
                    {
                        Collect(child, numbers);
                    }
                }

                break;

            case TableBlock table:
                foreach (TableRow row in table.Header is null ? table.Rows : [table.Header, .. table.Rows])
                {
                    foreach (TableCell cell in row.Cells)
                    {
                        Collect(cell.Inlines, numbers);
                    }
                }

                break;
        }
    }

    private static void Collect(IReadOnlyList<Inline> inlines, HashSet<int> numbers)
    {
        foreach (Inline inline in inlines)
        {
            if (inline is FootnoteRef reference)
            {
                numbers.Add(reference.FootnoteNumber);
            }
        }
    }
}
