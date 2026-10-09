using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Truth;

namespace LegalAgent.Corpus.Typesetting;

/// <summary>
/// Tables (tariff grids, gridless tariffs, key–value record cards): cells wrapped on measured widths, the column-name
/// row repeated on every page the table spans (only the first one is content in the truth), rows never split across
/// pages, notes printed under the last row. Geometry follows the banking test generator the parser is tuned on: grid
/// cells with a 5-pt padding; gridless rows separated by about two leadings so lines of one row merge and rows do not.
/// </summary>
internal static class TableLayout
{
    private const double Padding = 5;
    private const double GridRowExtra = 7;
    private const double NoteSize = 8.5;
    private const double NoteLeading = 11;

    public static void Table(PageWriter w, TableElement table)
    {
        LayoutStyle s = w.Style;
        bool grid = table.Grid ?? s.TableGrid;
        string[] header = table.Columns.Select(c => c.Header).ToArray();
        var rows = table.Rows.Select(r => r.Select(c => (IReadOnlyList<Inline>)c.Text).ToList()).ToList();
        w.Truth.Tables.Add(new TruthTable(header, rows.Select(r => (IReadOnlyList<string>)r.Select(Inline.PlainText).ToList()).ToList()));

        double[] weights = table.Columns.Select(c => c.Weight).ToArray();
        IReadOnlyList<Inline>[] headerCells = header.Select(h => (IReadOnlyList<Inline>)[new Inline(h, InlineStyle.Bold)]).ToArray();
        Grid(w, weights, headerCells, rows, grid, boldFirstColumn: false);

        foreach (IReadOnlyList<Inline> note in table.Notes)
        {
            foreach (SetLine line in TextMeasure.Wrap(TextMeasure.Tokenize(note), w.ColumnWidth, NoteSize))
            {
                double y = w.Place(line.Tokens, NoteLeading);
                w.Draw(line, w.ColumnLeft, y, NoteSize);
            }
        }

        w.Y += s.ParagraphGap;
    }

    public static void KeyValue(PageWriter w, KeyValueTableElement card)
    {
        var rows = card.Pairs.Select(p => new List<IReadOnlyList<Inline>> { new[] { new Inline(p.Key) }, p.Value }).ToList();
        w.Truth.Tables.Add(new TruthTable([], rows.Select(r => (IReadOnlyList<string>)r.Select(Inline.PlainText).ToList()).ToList()));
        Grid(w, [1, 2.6], null, rows, grid: true, boldFirstColumn: true);
        w.Y += w.Style.ParagraphGap;
    }

    private static void Grid(PageWriter w, double[] weights, IReadOnlyList<Inline>[]? header, List<List<IReadOnlyList<Inline>>> rows, bool grid, bool boldFirstColumn)
    {
        LayoutStyle s = w.Style;
        double size = s.TableSize;
        double lead = s.TableLeading;
        double rowGap = grid ? 0 : lead;
        double firstBaseline = grid ? Padding + size : size;

        // Each column is at least as wide as its longest word (otherwise the word would run into the next cell).
        var minimum = new double[weights.Length];
        void Need(IReadOnlyList<IReadOnlyList<Inline>> cells, bool bold)
        {
            for (int c = 0; c < cells.Count && c < minimum.Length; c++)
            {
                foreach (Token token in TextMeasure.Tokenize(cells[c]))
                {
                    InlineStyle style = bold || (boldFirstColumn && c == 0) ? InlineStyle.Bold : token.Style;
                    minimum[c] = Math.Max(minimum[c], TextMeasure.Width(token.Text, size, style) + (2 * Padding) + 1);
                }
            }
        }

        if (header is not null)
        {
            Need(header, bold: true);
        }

        foreach (List<IReadOnlyList<Inline>> row in rows)
        {
            Need(row, bold: false);
        }

        weights = Fit(weights, minimum, w.ColumnWidth);
        double[] x = Edges(w, weights);
        double top = w.Y - size;
        double segmentTop = top;

        List<SetLine>[] Cells(IReadOnlyList<IReadOnlyList<Inline>> cells, bool bold) =>
            cells.Select((c, i) => TextMeasure.Wrap(
                TextMeasure.Tokenize(bold || (boldFirstColumn && i == 0) ? c.Select(r => r with { Style = InlineStyle.Bold }) : c),
                x[i + 1] - x[i] - (2 * Padding),
                size)).ToArray();

        double Height(List<SetLine>[] cells) => (Math.Max(1, cells.Max(c => c.Count)) * lead) + (grid ? GridRowExtra : 0);

        void DrawRow(List<SetLine>[] cells, bool record)
        {
            for (int c = 0; c < cells.Length; c++)
            {
                for (int k = 0; k < cells[c].Count; k++)
                {
                    w.Draw(cells[c][k], x[c] + (grid ? Padding : 0), top + firstBaseline + (k * lead), size, record);
                }
            }

            if (!record)
            {
                w.Truth.Artifacts.Add(string.Join(" ", cells.SelectMany(c => c).Select(l => l.Text)));
            }

            w.Track();
            top += Height(cells);
            if (grid)
            {
                w.Builder.HLine(x[0], x[^1], top);
            }
        }

        void CloseSegment()
        {
            if (grid)
            {
                foreach (double vx in x)
                {
                    w.Builder.VLine(vx, segmentTop, top);
                }
            }
        }

        List<SetLine>[]? headerLines = header is null ? null : Cells(header, bold: true);
        void Header(bool repeat)
        {
            if (grid)
            {
                w.Builder.HLine(x[0], x[^1], top);
            }

            if (headerLines is not null)
            {
                DrawRow(headerLines, record: !repeat);
                top += rowGap;
            }
        }

        // The column-name row and the first row stay together.
        List<SetLine>[][] bodies = rows.Select(r => Cells(r, bold: false)).ToArray();
        w.Need((headerLines is null ? 0 : Height(headerLines) + rowGap) + (bodies.Length > 0 ? Height(bodies[0]) : 0) + size);
        top = w.Y - size;
        segmentTop = top;
        Header(repeat: false);

        foreach (List<SetLine>[] cells in bodies)
        {
            double h = Height(cells);
            if (top + h - (grid ? GridRowExtra : 0) > w.Limit + size)
            {
                CloseSegment();
                w.Y = top + size;
                w.Break();
                x = Edges(w, weights);
                top = w.Y - size;
                segmentTop = top;
                Header(repeat: true);
            }

            DrawRow(cells, record: true);
            top += rowGap;
        }

        CloseSegment();
        w.Y = top - rowGap + size + s.Leading;
    }

    /// <summary>Weights adjusted so no column is narrower than its minimum width (the others give up space proportionally).</summary>
    private static double[] Fit(double[] weights, double[] minimum, double total)
    {
        double sum = weights.Sum();
        double[] widths = weights.Select(v => total * v / sum).ToArray();
        for (int round = 0; round < weights.Length; round++)
        {
            double deficit = 0;
            for (int i = 0; i < widths.Length; i++)
            {
                if (widths[i] < minimum[i])
                {
                    deficit += minimum[i] - widths[i];
                    widths[i] = minimum[i];
                }
            }

            if (deficit <= 0)
            {
                break;
            }

            double spare = widths.Select((v, i) => Math.Max(0, v - minimum[i])).Sum();
            if (spare <= 0)
            {
                break;
            }

            for (int i = 0; i < widths.Length; i++)
            {
                widths[i] -= deficit * Math.Max(0, widths[i] - minimum[i]) / spare;
            }
        }

        return widths;
    }

    private static double[] Edges(PageWriter w, double[] weights)
    {
        double total = weights.Sum();
        var x = new double[weights.Length + 1];
        x[0] = w.ColumnLeft;
        for (int i = 0; i < weights.Length; i++)
        {
            x[i + 1] = x[i] + (w.ColumnWidth * weights[i] / total);
        }

        return x;
    }
}
