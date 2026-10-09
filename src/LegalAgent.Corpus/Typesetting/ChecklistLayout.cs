using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Truth;

namespace LegalAgent.Corpus.Typesetting;

/// <summary>
/// Checklists in three forms (research R7: the fonts have no ☐ glyph): a vector-drawn empty box before the item, a
/// „□” set in the monospace face, or a grid table „Lp. | Czynność | Wykonano” with an empty last column; and the
/// framed callout box.
/// </summary>
internal static class ChecklistLayout
{
    private const double BoxSize = 8;
    private const double TextIndent = 16;
    private const double CalloutPadding = 8;

    public static void Checklist(PageWriter w, ChecklistElement list)
    {
        LayoutStyle s = w.Style;
        if (list.Form == ChecklistForm.Table)
        {
            var table = new TableElement(
                [new TableColumn("Lp.", 0.7), new TableColumn("Czynność", 6), new TableColumn("Wykonano", 1.4)],
                list.Items.Select((item, i) => (IReadOnlyList<TableCell>)
                    [TableCell.Of((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "."), new TableCell(item), new TableCell([])]).ToList(),
                Grid: true,
                Notes: []);
            TableLayout.Table(w, table);
            return;
        }

        foreach (IReadOnlyList<Inline> item in list.Items)
        {
            if (list.Form == ChecklistForm.Text)
            {
                w.Truth.ListItems.Add(new TruthListItem("□", 0, Typesetter.FirstWords(item)));
            }

            List<SetLine> lines = TextMeasure.Wrap(TextMeasure.Tokenize(item), w.ColumnWidth - TextIndent, s.BodySize);
            for (int i = 0; i < lines.Count; i++)
            {
                double y = w.Place(lines[i].Tokens, s.Leading);
                if (i == 0)
                {
                    double x = w.ColumnLeft;
                    if (list.Form == ChecklistForm.Vector)
                    {
                        double top = y - BoxSize + 0.5;
                        w.Builder.HLine(x, x + BoxSize, top, 0.6);
                        w.Builder.HLine(x, x + BoxSize, top + BoxSize, 0.6);
                        w.Builder.VLine(x, top, top + BoxSize, 0.6);
                        w.Builder.VLine(x + BoxSize, top, top + BoxSize, 0.6);
                    }
                    else
                    {
                        w.Builder.Text(x, y, "□", s.BodySize, mono: true);
                        w.Words("□");
                    }
                }

                w.Draw(lines[i], w.ColumnLeft + TextIndent, y, s.BodySize);
            }

            w.Y += list.Form == ChecklistForm.Vector ? s.ParagraphGap : 3;
        }

        if (list.Form == ChecklistForm.Text)
        {
            w.AfterListItem = true;
        }
        else
        {
            w.Y += s.ParagraphGap;
        }
    }

    public static void Callout(PageWriter w, CalloutElement callout)
    {
        LayoutStyle s = w.Style;
        List<SetLine> lines = TextMeasure.Wrap(TextMeasure.Tokenize(callout.Text), w.ColumnWidth - (2 * CalloutPadding), s.BodySize);
        double height = (lines.Count * s.Leading) + (2 * CalloutPadding) - 2;
        if (!w.AtTopOfColumn)
        {
            w.Y += 4;
        }

        w.Need(height + s.Leading);
        double top = w.Y - s.BodySize - CalloutPadding + 1;
        double left = w.ColumnLeft;
        double right = w.ColumnLeft + w.ColumnWidth;
        w.Builder.HLine(left, right, top, 0.8);
        w.Builder.HLine(left, right, top + height, 0.8);
        w.Builder.VLine(left, top, top + height, 0.8);
        w.Builder.VLine(right, top, top + height, 0.8);
        foreach (SetLine line in lines)
        {
            double y = w.Place(line.Tokens, s.Leading);
            w.Draw(line, left + CalloutPadding, y, s.BodySize);
        }

        w.Y = top + height + s.BodySize + s.ParagraphGap + 6;
    }
}
