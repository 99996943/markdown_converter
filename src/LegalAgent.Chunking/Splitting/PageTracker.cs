using LegalAgent.PdfParser.Model;

namespace LegalAgent.Chunking.Splitting;

/// <summary>
/// Source pages of the pieces of a unit (research R4): a paragraph's block pages; a table row's own page
/// (<see cref="TableRow.Page"/>) or, when unknown, the table's pages; a list item's start page follows the page breaks
/// met so far in its list, its end page the breaks and paragraphs of its own content.
/// </summary>
internal static class PageTracker
{
    /// <summary>Pages of a whole block that is not split.</summary>
    public static (int First, int Last) Block(ContentBlock block) => (block.Pages.First, block.Pages.Last);

    /// <summary>Pages of a body row of <paramref name="table"/>.</summary>
    public static (int First, int Last) Row(TableBlock table, TableRow row) =>
        row.Page is { } page ? (page, page) : (table.Pages.First, table.Pages.Last);

    /// <summary>
    /// Pages of a list item's own content (its text and paragraphs, not its nested lists); <paramref name="page"/> is
    /// the current page of the list flow and is advanced past the item's own content.
    /// </summary>
    public static (int First, int Last) Item(ListItem item, ref int page)
    {
        int i = 0;
        while (i < item.Inlines.Count && item.Inlines[i] is PageBreak leading)
        {
            page = Math.Max(page, leading.PageNumber);
            i++;
        }

        int first = page;
        for (; i < item.Inlines.Count; i++)
        {
            if (item.Inlines[i] is PageBreak pageBreak)
            {
                page = Math.Max(page, pageBreak.PageNumber);
            }
        }

        foreach (ContentBlock child in item.Children)
        {
            if (child is not ListBlock)
            {
                page = Math.Max(page, child.Pages.Last);
            }
        }

        return (first, page);
    }
}
