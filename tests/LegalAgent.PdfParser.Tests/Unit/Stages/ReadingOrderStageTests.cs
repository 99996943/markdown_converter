using System.Globalization;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using static LegalAgent.PdfParser.Tests.Fixtures.LayoutFactory;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

public sealed class ReadingOrderStageTests
{
    // "L1 " + 44 characters → 235 pt wide (39 % of an A4 page width); columns at 72–307 and 330–565.
    private const string ColumnText = "Tekst kolumny o długości mniej więcej stałej";

    private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);

    private static LayoutLine Left(int i, double top) => Line($"L{N(i)} {ColumnText}", 72, top);

    private static LayoutLine Right(int i, double top) => Line($"R{N(i)} {ColumnText}", 330, top);

    private static IEnumerable<string> Run(PipelineContext context)
    {
        new ReadingOrderStage().Execute(context);
        return context.Pages[0].Lines.Select(l => l.Text.Split(' ')[0]);
    }

    private static IEnumerable<LayoutLine> TwoColumns(int from, int count, double firstTop)
    {
        for (int i = 0; i < count; i++)
        {
            double top = firstTop + (i * 14);
            yield return Left(from + i, top);
            yield return Right(from + i, top);
        }
    }

    [Fact]
    public void Order_IsReadingOrder()
    {
        Assert.Equal(StageOrder.ReadingOrder, new ReadingOrderStage().Order);
    }

    [Fact]
    public void TwoColumnPage_IsReadLeftColumnThenRightColumn()
    {
        var lines = new List<LayoutLine> { Line("TYTUŁ dokumentu rozciągnięty na całą szerokość strony tekstu", 72, 60) };
        lines.AddRange(TwoColumns(1, 8, 100));

        IEnumerable<string> order = Run(Context([Page(1, lines)]));

        Assert.Equal(
            ["TYTUŁ", "L1", "L2", "L3", "L4", "L5", "L6", "L7", "L8", "R1", "R2", "R3", "R4", "R5", "R6", "R7", "R8"],
            order);
    }

    [Fact]
    public void SpanningLineInTheMiddle_SeparatesColumnBands()
    {
        var lines = new List<LayoutLine>();
        lines.AddRange(TwoColumns(1, 4, 100));
        lines.Add(Line("ŚRÓDTYTUŁ obejmujący obie kolumny tekstu na stronie", 72, 170));
        lines.AddRange(TwoColumns(5, 4, 190));

        IEnumerable<string> order = Run(Context([Page(1, lines)]));

        Assert.Equal(
            ["L1", "L2", "L3", "L4", "R1", "R2", "R3", "R4", "ŚRÓDTYTUŁ", "L5", "L6", "L7", "L8", "R5", "R6", "R7", "R8"],
            order);
    }

    [Fact]
    public void SingleColumnPage_IsUntouched()
    {
        var lines = new List<LayoutLine>();
        for (int i = 1; i <= 8; i++)
        {
            // Every third line is a short paragraph ending.
            string text = i % 3 == 0
                ? $"P{N(i)} koniec."
                : $"P{N(i)} {ColumnText} {ColumnText}";
            lines.Add(Line(text, 72, 100 + (i * 14)));
        }

        IEnumerable<string> order = Run(Context([Page(1, lines)]));

        Assert.Equal(Enumerable.Range(1, 8).Select(i => $"P{N(i)}"), order);
    }

    [Fact]
    public void ShortCellsSideBySide_AreNotTreatedAsColumns()
    {
        // Tariff-like rows emitted as separate short lines: too short to be text columns (< 30 % width).
        var lines = new List<LayoutLine>();
        for (int i = 1; i <= 6; i++)
        {
            double top = 100 + (i * 14);
            lines.Add(Line($"U{N(i)} Usługa", 72, top));
            lines.Add(Line($"C{N(i)} 0 zł", 400, top));
        }

        IEnumerable<string> order = Run(Context([Page(1, lines)]));

        Assert.Equal(
            Enumerable.Range(1, 6).SelectMany(i => new[] { $"U{N(i)}", $"C{N(i)}" }),
            order);
    }

    [Fact]
    public void TableLines_KeepTheirPosition_AndAreNotSplit()
    {
        var lines = new List<LayoutLine>();
        lines.AddRange(TwoColumns(1, 4, 100));
        LayoutLine table = Line("T1 Prowadzenie rachunku 0 zł miesięcznie", 72, 170);
        table.Role = LineRole.Table;
        lines.Add(table);
        lines.AddRange(TwoColumns(5, 4, 190));

        IEnumerable<string> order = Run(Context([Page(1, lines)]));

        Assert.Equal(
            ["L1", "L2", "L3", "L4", "R1", "R2", "R3", "R4", "T1", "L5", "L6", "L7", "L8", "R5", "R6", "R7", "R8"],
            order);
    }

    [Fact]
    public void DetectColumnsDisabled_LeavesOrderUnchanged()
    {
        IEnumerable<string> order = Run(Context([Page(1, TwoColumns(1, 4, 100))], o => o.Layout.DetectColumns = false));

        Assert.Equal(["L1", "R1", "L2", "R2", "L3", "R3", "L4", "R4"], order);
    }

    [Fact]
    public void NarrowGap_BelowMinimumGutterWidth_IsNotAColumnBreak()
    {
        // Gap of 8 pt (1.3 % of page width) is below GutterMinWidthRatio = 2 %.
        var lines = new List<LayoutLine>();
        for (int i = 1; i <= 6; i++)
        {
            double top = 100 + (i * 14);
            lines.Add(Line($"L{N(i)} {ColumnText}", 72, top));
            lines.Add(Line($"R{N(i)} {ColumnText}", 307 + 8, top));
        }

        IEnumerable<string> order = Run(Context([Page(1, lines)]));

        Assert.Equal(Enumerable.Range(1, 6).SelectMany(i => new[] { $"L{N(i)}", $"R{N(i)}" }), order);
    }

    /// <summary>A bullet line whose bullet is a separate narrow segment, as line assembly splits it on the gap.</summary>
    private static LayoutLine BulletLine(string id, string text, double left, double top)
    {
        LayoutLine textLine = Line($"{id} {text}", left + 12, top);
        var bullet = new LayoutWord([], new Rect(left, top, left + 2, top + LineHeight), "•", LegalAgent.PdfParser.Model.TextStyle.None);
        var line = new LayoutLine([bullet, .. textLine.Words], bullet.Box.Union(textLine.Box), textLine.Baseline);
        line.Segments.Add(new LineSegment([bullet], bullet.Box));
        line.Segments.Add(textLine.Segments[0]);
        return line;
    }

    [Fact]
    public void RaggedColumnsWithBulletSegments_AreStillTwoColumns()
    {
        // Left-aligned (ragged) columns: lines of 150–200 pt in 240-pt columns, bullets as separate 2-pt segments and
        // the right column's baselines offset from the left one's.
        string[] lengths = ["Tekst lewej kolumny regulaminu ciagnie", "dalej krotsza linia akapitu", "Regulamin okresla zasady korzystania z"];
        var lines = new List<LayoutLine>();
        for (int i = 0; i < 9; i++)
        {
            string text = lengths[i % 3];
            lines.Add(i % 3 == 1 ? BulletLine($"L{N(i)}", text, 72, 100 + (14 * i)) : Line($"L{N(i)} {text}", 72, 100 + (14 * i)));
            lines.Add(i % 3 == 2 ? BulletLine($"R{N(i)}", text, 345, 107 + (14 * i)) : Line($"R{N(i)} {text}", 345, 107 + (14 * i)));
        }

        PipelineContext context = Context([Page(1, lines)]);
        new ReadingOrderStage().Execute(context);
        string[] order = context.Pages[0].Lines.Select(l => l.Words.First(w => w.Text != "•").Text).ToArray();

        Assert.Equal(
            Enumerable.Range(0, 9).Select(i => $"L{N(i)}").Concat(Enumerable.Range(0, 9).Select(i => $"R{N(i)}")),
            order);
    }
}
