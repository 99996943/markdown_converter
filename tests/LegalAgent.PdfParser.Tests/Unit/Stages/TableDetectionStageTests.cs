using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

/// <summary>
/// T075 — table detection (FR-060 – FR-066). Rows are built directly as lines whose segments are the cells; rows are
/// 20 pt apart and lines of a multi-line cell 12 pt apart, as in the fee schedule of the corpus.
/// </summary>
public sealed class TableDetectionStageTests
{
    private const double C1 = 56;
    private const double C2 = 200;
    private const double C3 = 420;

    /// <summary>A line whose segments are the given cells; words advance 5 pt per character with a 5 pt space.</summary>
    private static LayoutLine Row(double top, params (string Text, double Left)[] cells) => Styled(top, TextStyle.None, cells);

    private static LayoutLine Styled(double top, TextStyle style, params (string Text, double Left)[] cells)
    {
        var words = new List<LayoutWord>();
        var segments = new List<LineSegment>();
        foreach ((string text, double left) in cells)
        {
            var cellWords = new List<LayoutWord>();
            double x = left;
            foreach (string part in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var box = new Rect(x, top, x + (part.Length * LayoutFactory.CharWidth), top + LayoutFactory.LineHeight);
                cellWords.Add(new LayoutWord([], box, part, style));
                x = box.Right + LayoutFactory.CharWidth;
            }

            words.AddRange(cellWords);
            segments.Add(new LineSegment(cellWords, cellWords.Skip(1).Aggregate(cellWords[0].Box, (a, w) => a.Union(w.Box))));
        }

        Rect lineBox = segments.Skip(1).Aggregate(segments[0].Box, (a, s) => a.Union(s.Box));
        var line = new LayoutLine(words, lineBox, top + 8);
        foreach (LineSegment segment in segments)
        {
            line.Segments.Add(segment);
        }

        return line;
    }

    private static LayoutLine[] Tariff(double top = 100) =>
    [
        Styled(top, TextStyle.Bold, ("Usluga", C1), ("Oplata", C2), ("Czestotliwosc", C3)),
        Row(top + 20, ("Prowadzenie rachunku", C1), ("0,00 zl", C2), ("miesiecznie", C3)),
        Row(top + 40, ("Przelew natychmiastowy", C1), ("1,5%", C2), ("za transakcje", C3)),
        Row(top + 60, ("Wyplata z bankomatu", C1), ("min. 10 zl", C2), ("jednorazowo", C3)),
    ];

    private static PipelineContext Run(Action<PdfParserOptions>? configure, params LayoutPage[] pages)
    {
        PipelineContext context = LayoutFactory.Context(pages, configure);
        context.BodyStyle = new BodyStyle(10, 20);
        new TableDetectionStage().Execute(context);
        return context;
    }

    private static PipelineContext Run(params LayoutPage[] pages) => Run(null, pages);

    private static string Text(TableCell cell) => string.Concat(cell.Inlines.OfType<TextRun>().Select(r => r.Text));

    private static string[][] Cells(IEnumerable<TableRow> rows) => rows.Select(r => r.Cells.Select(Text).ToArray()).ToArray();

    private static TableBlock SingleTable(PipelineContext context)
    {
        LayoutBlock block = Assert.Single(context.Tables);
        Assert.Equal(LayoutBlockKind.Table, block.Kind);
        return Assert.IsType<TableBlock>(block.Table);
    }

    [Fact]
    public void Order_IsTableDetection()
    {
        Assert.Equal(StageOrder.TableDetection, new TableDetectionStage().Order);
    }

    [Fact]
    public void AlignedMultiSegmentLines_FormATableWithBoldHeader_FR061_FR063()
    {
        LayoutPage page = LayoutFactory.Page(1, [LayoutFactory.Line("Taryfa oplat:", C1, 70), .. Tariff()]);

        PipelineContext context = Run(page);

        TableBlock table = SingleTable(context);
        Assert.False(table.IsFallback);
        Assert.Equal(3, table.ColumnCount);
        Assert.Equal(new PageRange(1, 1), table.Pages);
        Assert.Equal(["Usluga", "Oplata", "Czestotliwosc"], table.Header!.Cells.Select(Text));
        Assert.Equal(
            [
                ["Prowadzenie rachunku", "0,00 zl", "miesiecznie"],
                ["Przelew natychmiastowy", "1,5%", "za transakcje"],
                ["Wyplata z bankomatu", "min. 10 zl", "jednorazowo"],
            ],
            Cells(table.Rows));
        Assert.Equal(LineRole.Unknown, page.Lines[0].Role);
        Assert.All(page.Lines.Skip(1), l => Assert.Equal(LineRole.Table, l.Role));
        Assert.All(page.Lines.Skip(1), l => Assert.Equal("0", l.Annotations[LayoutAnnotations.TableIndex]));
    }

    [Fact]
    public void RegularTopRow_IsNotAHeader()
    {
        LayoutLine[] rows = Tariff();
        rows[0] = Row(100, ("Usluga", C1), ("Oplata", C2), ("Czestotliwosc", C3));

        TableBlock table = SingleTable(Run(LayoutFactory.Page(1, rows)));

        Assert.Null(table.Header);
        Assert.Equal(4, table.Rows.Count);
    }

    [Fact]
    public void TwoAlignedLines_AreNotATable_FR061()
    {
        PipelineContext context = Run(LayoutFactory.Page(1, Tariff().Take(2)));

        Assert.Empty(context.Tables);
        Assert.All(context.Pages[0].Lines, l => Assert.Equal(LineRole.Unknown, l.Role));
    }

    [Fact]
    public void CellsWithSlightBaselineJitter_StayInOneRow_FR030()
    {
        var sketch = new PageSketch();
        for (int i = 0; i < 3; i++)
        {
            double baseline = 100 + (20 * i);
            sketch.Line($"Usluga {i}", C1, baseline).Line($"{i},00 zl", C2, baseline + 0.8).Line("miesiecznie", C3, baseline - 0.6);
        }

        PipelineContext context = PageSketch.Assemble(null, sketch);
        new TableDetectionStage().Execute(context);

        TableBlock table = SingleTable(context);
        Assert.Equal([["Usluga 0", "0,00 zl", "miesiecznie"], ["Usluga 1", "1,00 zl", "miesiecznie"], ["Usluga 2", "2,00 zl", "miesiecznie"]], Cells(table.Rows));
    }

    [Fact]
    public void PartialLineCloseBelow_IsMergedIntoThePreviousRow_FR062()
    {
        PipelineContext context = Run(LayoutFactory.Page(1,
        [
            Row(100, ("1.01", C1), ("Wydanie paszportu osobie maloletniej, ktora w dniu", C2 - 100), ("35", C3)),
            Row(112, ("zlozenia wniosku nie ukonczyla 12 roku zycia", C2 - 100)),
            Row(131, ("1.02", C1), ("Wydanie paszportu tymczasowego", C2 - 100), ("40", C3)),
            Row(154, ("1.03", C1), ("Wydanie drugiego paszportu", C2 - 100), ("220", C3)),
        ]));

        TableBlock table = SingleTable(context);
        Assert.Equal(
            [
                ["1.01", "Wydanie paszportu osobie maloletniej, ktora w dniu zlozenia wniosku nie ukonczyla 12 roku zycia", "35"],
                ["1.02", "Wydanie paszportu tymczasowego", "40"],
                ["1.03", "Wydanie drugiego paszportu", "220"],
            ],
            Cells(table.Rows));
    }

    [Fact]
    public void HorizontalRulingBetweenLines_StartsANewRow_FR062()
    {
        LayoutPage page = LayoutFactory.Page(1,
        [
            Row(100, ("A", C1), ("pierwszy", C2), ("1", C3)),
            Row(112, ("drugi", C2)),
            Row(131, ("B", C1), ("trzeci", C2), ("2", C3)),
            Row(154, ("C", C1), ("czwarty", C2), ("3", C3)),
        ]);
        page.Rulings.Add(new Segment(C1 - 5, 111, 560, 111));

        TableBlock table = SingleTable(Run(page));

        Assert.Equal(4, table.Rows.Count);
        Assert.Equal(["", "drugi", ""], table.Rows[1].Cells.Select(Text));
    }

    [Fact]
    public void HorizontalRulings_DefineRowsOfVerticallyCentredCells()
    {
        // A definitions table: the term is centred against a multi-line explanation; rulings separate the rows.
        LayoutPage page = LayoutFactory.Page(1,
        [
            Row(100, ("Definicje", C1), ("Wyjasnienie", C2)),
            Row(130, ("przedsiebiorca, ktory przyjmuje platnosci", C2)),
            Row(146, ("akceptant", C1), ("BLIK na podstawie umowy z agentem", C2)),
            Row(162, ("rozliczeniowym.", C2)),
            Row(190, ("elektroniczne urzadzenie w telefonie,", C2)),
            Row(206, ("antena NFC", C1), ("ktore pozwala placic", C2)),
            Row(222, ("zblizeniowo.", C2)),
        ]);
        foreach (double y in new[] { 95, 122, 178, 236 })
        {
            page.Rulings.Add(new Segment(C2 - 5, y, 560, y));
        }

        TableBlock table = SingleTable(Run(page));

        Assert.Equal(
            [
                ["Definicje", "Wyjasnienie"],
                ["akceptant", "przedsiebiorca, ktory przyjmuje platnosci BLIK na podstawie umowy z agentem rozliczeniowym."],
                ["antena NFC", "elektroniczne urzadzenie w telefonie, ktore pozwala placic zblizeniowo."],
            ],
            Cells(table.Rows));
    }

    [Fact]
    public void VaryingColumnCount_FallsBackWithAWarning_FR064()
    {
        PipelineContext context = Run(LayoutFactory.Page(1,
        [
            Row(100, ("Usluga", C1), ("Oplata", C2), ("Uwagi", C3)),
            Row(120, ("Przelew", C1), ("Kanal", 130), ("2 zl", C2), ("brak", C3)),
            Row(140, ("Wyplata", C1), ("5 zl", C2), ("brak", C3)),
            Row(160, ("Karta", C1), ("Wydanie", 130), ("0 zl", C2), ("brak", C3)),
        ]));

        TableBlock table = SingleTable(context);
        Assert.True(table.IsFallback);
        Assert.Equal(["Przelew", "Kanal", "2 zl", "brak"], table.Rows[1].Cells.Select(Text));
        Assert.Contains(context.Report.Build(1, TimeSpan.Zero).Warnings, w => w.Code == "TBL001_AmbiguousGrid" && w.PageNumber == 1);
    }

    [Fact]
    public void TableContinuedOnTheNextPage_IsMergedAndTheRepeatedHeaderDropped_FR065()
    {
        LayoutPage page1 = LayoutFactory.Page(1, Tariff(top: 680));
        LayoutPage page2 = LayoutFactory.Page(2,
        [
            Styled(80, TextStyle.Bold, ("Usluga", C1), ("Oplata", C2), ("Czestotliwosc", C3)),
            Row(100, ("Zlecenie stale", C1), ("2,00 zl", C2), ("miesiecznie", C3)),
            Row(120, ("Polecenie zaplaty", C1), ("0,00 zl", C2), ("za transakcje", C3)),
            LayoutFactory.Line("Tekst po tabeli zaczyna nowy akapit regulaminu.", C1, 160),
        ]);

        PipelineContext context = Run(page1, page2);

        TableBlock table = SingleTable(context);
        Assert.Equal(new PageRange(1, 2), table.Pages);
        Assert.Equal(5, table.Rows.Count);
        Assert.Equal(["Polecenie zaplaty", "0,00 zl", "za transakcje"], table.Rows[^1].Cells.Select(Text));
        Assert.All(page2.Lines.Take(3), l => Assert.Equal("0", l.Annotations[LayoutAnnotations.TableIndex]));
        Assert.Equal(LineRole.Unknown, page2.Lines[3].Role);
    }

    [Fact]
    public void CellCrossingColumnBoundaries_GoesToTheFirstColumnWithASpan_FR066()
    {
        PipelineContext context = Run(LayoutFactory.Page(1,
        [
            Row(100, ("1.01", C1), ("Wydanie paszportu", C2), ("110", C3)),
            Row(120, ("II. Czynnosci w sprawach obywatelstwa polskiego", 120)),
            Row(140, ("2.01", C1), ("Przyjecie wniosku", C2), ("360", C3)),
            Row(160, ("2.02", C1), ("Przywrocenie obywatelstwa", C2), ("40", C3)),
        ]));

        TableBlock table = SingleTable(context);
        TableRow section = table.Rows[1];
        Assert.Equal("II. Czynnosci w sprawach obywatelstwa polskiego", Text(section.Cells[0]));
        Assert.Equal(2, section.Cells[0].ColumnSpan);
        Assert.Equal(3, section.Cells.Sum(c => c.ColumnSpan));
    }

    [Fact]
    public void TwoColumnText_IsNotATable()
    {
        string left = "Tekst lewej kolumny regulaminu ciagnie sie";
        string right = "Tekst prawej kolumny regulaminu ciagnie sie";
        PipelineContext context = Run(LayoutFactory.Page(1,
            Enumerable.Range(0, 5).Select(i => Row(100 + (14 * i), (left, 50), (right, 320)))));

        Assert.Empty(context.Tables);
    }

    [Fact]
    public void BulletLinesWithAGapAfterTheBullet_AreNotATable()
    {
        PipelineContext context = Run(LayoutFactory.Page(1,
            Enumerable.Range(0, 4).Select(i => Row(100 + (14 * i), ("•", 85), ($"pozycja listy numer {i}", 100)))));

        Assert.Empty(context.Tables);
    }

    [Fact]
    public void Disabled_DetectsNothing()
    {
        PipelineContext context = Run(o => o.Tables.Enabled = false, LayoutFactory.Page(1, Tariff()));

        Assert.Empty(context.Tables);
        Assert.All(context.Pages[0].Lines, l => Assert.Equal(LineRole.Unknown, l.Role));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(16)]
    public void JustifiedTextSplitIntoWords_IsNotATable(double stretchedSpace)
    {
        // Justification widens spaces past the cell threshold, so every word is its own segment: slightly (0.6 em) or,
        // in sparse ISAP lines, even beyond 1 em — but then all gaps of the line are alike.
        LayoutLine Justified(double top, params string[] words)
        {
            var cells = new List<(string, double)>();
            double x = 71;
            foreach (string word in words)
            {
                cells.Add((word, x));
                x += (word.Length * LayoutFactory.CharWidth) + stretchedSpace;
            }

            return Row(top, cells.ToArray());
        }

        PipelineContext context = Run(LayoutFactory.Page(1,
        [
            Justified(100, "W", "celu", "zapewnienia", "zawodowego,", "rzetelnego,"),
            Justified(120, "i", "politycznie", "neutralnego", "wykonywania", "zadan"),
            Justified(140, "cywilna", "oraz", "okresla", "zasady", "dostepu"),
            Justified(160, "funkcjonowania", "i", "rozwoju", "tej", "sluzby"),
        ]));

        Assert.Empty(context.Tables);
    }

    [Fact]
    public void BulletInsideACell_StaysWithItsText_FR061()
    {
        PipelineContext context = Run(LayoutFactory.Page(1,
        [
            Row(100, ("Definicje", C1), ("Wyjasnienie", C2)),
            Row(120, ("przelew", C1), ("•", C2), ("do banku lub", C2 + 15)),
            Row(140, ("saldo", C1), ("ilosc pieniedzy na rachunku", C2)),
            Row(160, ("regulamin", C1), ("ten regulamin", C2)),
        ]));

        TableBlock table = SingleTable(context);
        Assert.False(table.IsFallback);
        Assert.Equal(["przelew", "• do banku lub"], table.Rows[1].Cells.Select(Text));
    }

    [Fact]
    public void LoneDashCell_MeaningNoFee_StaysItsOwnCell()
    {
        PipelineContext context = Run(LayoutFactory.Page(1,
        [
            Row(100, ("Usluga", C1), ("Oplata", C2), ("Uwagi", C3)),
            Row(120, ("Przelew", C1), ("–", C2), ("bez oplat", C3)),
            Row(140, ("Wyplata", C1), ("5 zl", C2), ("bankomat", C3)),
        ]));

        Assert.Equal(["Przelew", "–", "bez oplat"], SingleTable(context).Rows[1].Cells.Select(Text));
    }

    [Fact]
    public void RuledGridWithVerticallyCentredCells_IsNotAmbiguous_FR064()
    {
        // Lines of one ruled row carry 2 or 3 cells because each cell is centred against the tallest one.
        LayoutPage page = LayoutFactory.Page(1,
        [
            Row(100, ("rodzaj wplaty", C1), ("kiedy przyjmujemy", C2), ("kiedy uznamy", C3)),
            Row(130, ("wplata karta", C1), ("w godzinach dostepnosci", C2)),
            Row(142, ("we wplatomacie", C1), ("wplatomatow", C2), ("natychmiast", C3)),
            Row(172, ("wplata w placowce", C1), ("w godzinach pracy", C2), ("natychmiast", C3)),
            Row(202, ("inny rodzaj", C1), ("okreslamy", C3)),
            Row(214, ("wplaty", C1), ("w regulaminach", C2), ("w regulaminach", C3)),
        ]);
        foreach (double y in new[] { 95, 120, 160, 190, 226 })
        {
            // Each row border is drawn in short pieces, none of them spanning a good part of the table.
            foreach (double x in new double[] { C1 - 5, 150, 250, 350, 450 })
            {
                page.Rulings.Add(new Segment(x, y, x + 99, y));
            }
        }

        TableBlock table = SingleTable(Run(page));

        Assert.False(table.IsFallback);
        Assert.Equal(
            [
                ["rodzaj wplaty", "kiedy przyjmujemy", "kiedy uznamy"],
                ["wplata karta we wplatomacie", "w godzinach dostepnosci wplatomatow", "natychmiast"],
                ["wplata w placowce", "w godzinach pracy", "natychmiast"],
                ["inny rodzaj wplaty", "w regulaminach", "okreslamy w regulaminach"],
            ],
            Cells(table.Rows));
    }

    private static void Grid(LayoutPage page, double top, double bottom, params double[] rowBorders)
    {
        foreach (double y in rowBorders)
        {
            page.Rulings.Add(new Segment(C1 - 5, y, 560, y));
        }

        foreach (double x in new double[] { C1 - 5, C2 - 5, C3 - 5, 560 })
        {
            page.Rulings.Add(new Segment(x, top, x, bottom));
        }
    }

    [Fact]
    public void CellLineAboveTheFirstMultiCellLine_BelongsToTheHeaderRow()
    {
        LayoutPage page = LayoutFactory.Page(1,
        [
            Row(92, ("kiedy przyjmujemy", C2)),
            Row(100, ("rodzaj wplaty", C1), ("kiedy uznamy", C3)),
            Row(108, ("wplaty?", C2)),
            Row(130, ("wplata w placowce", C1), ("w godzinach pracy", C2), ("natychmiast", C3)),
            Row(160, ("wplata karta", C1), ("przez cala dobe", C2), ("natychmiast", C3)),
        ]);
        Grid(page, 80, 175, 80, 120, 150, 175);

        TableBlock table = SingleTable(Run(page));

        Assert.Equal(["rodzaj wplaty", "kiedy przyjmujemy wplaty?", "kiedy uznamy"], table.Rows[0].Cells.Select(Text));
    }

    [Fact]
    public void ParagraphBetweenTwoRuledGrids_SeparatesTwoTables()
    {
        LayoutPage page = LayoutFactory.Page(1,
        [
            Row(100, ("wplata w placowce", C1), ("w godzinach pracy", C2), ("natychmiast", C3)),
            Row(120, ("wplata karta", C1), ("przez cala dobe", C2), ("natychmiast", C3)),
            Row(140, ("wplata BLIK", C1), ("przez cala dobe", C2), ("natychmiast", C3)),
            LayoutFactory.Line("2) Rodzaje wyplat znajdziesz w tabeli:", C1, 160),
            Row(180, ("wyplata w placowce", C1), ("w godzinach pracy", C2), ("natychmiast", C3)),
            Row(200, ("wyplata karta", C1), ("przez cala dobe", C2), ("natychmiast", C3)),
            Row(220, ("wyplata BLIK", C1), ("przez cala dobe", C2), ("natychmiast", C3)),
        ]);
        Grid(page, 92, 152, 92, 112, 132, 152);
        Grid(page, 172, 232, 172, 192, 212, 232);

        PipelineContext context = Run(page);

        Assert.Equal(2, context.Tables.Count);
        Assert.Equal(LineRole.Unknown, page.Lines[3].Role);
    }

    [Fact]
    public void ContinuationWhoseBandStartsLaterWithinTheSameColumn_IsMerged_FR065()
    {
        // Page 1: the header of the amount column starts left of the amounts and a ruling snaps the band to 404;
        // page 2 has no header, so its amount band starts at the amounts (425) — still the same column.
        LayoutPage page1 = LayoutFactory.Page(1,
        [
            Row(680, ("Poz.", C1), ("Rodzaj czynnosci", C2), ("Wysokosc", C3 - 8)),
            Row(700, ("1.01", C1), ("Wydanie paszportu", C2), ("110", C3 + 5)),
            Row(720, ("1.02", C1), ("Wydanie wizy", C2), ("35", C3 + 5)),
        ]);
        page1.Rulings.Add(new Segment(C3 - 16, 670, C3 - 16, 730));
        LayoutPage page2 = LayoutFactory.Page(2,
        [
            Row(100, ("1.03", C1), ("Wydanie zaswiadczenia", C2), ("40", C3 + 5)),
            Row(120, ("1.04", C1), ("Legalizacja dokumentu", C2), ("25", C3 + 5)),
            Row(140, ("1.05", C1), ("Przyjecie oswiadczenia", C2), ("30", C3 + 5)),
        ]);

        TableBlock table = SingleTable(Run(page1, page2));

        Assert.Equal(new PageRange(1, 2), table.Pages);
    }

    [Fact]
    public void SectionRowAboveTheFirstMultiCellLineInsideTheGrid_BelongsToTheTable()
    {
        LayoutPage page = LayoutFactory.Page(1,
        [
            Row(100, ("VII. Czynnosci w sprawach stanu cywilnego", C2)),
            Row(125, ("7.01", C1), ("Przygotowanie dokumentacji", C2), ("500", C3)),
            Row(145, ("7.02", C1), ("Przyjecie i przekazanie", C2), ("50", C3)),
            Row(165, ("7.03", C1), ("Wydanie zaswiadczenia", C2), ("50", C3)),
        ]);
        Grid(page, 92, 175, 92, 117, 137, 157, 175);

        TableBlock table = SingleTable(Run(page));

        Assert.Equal("VII. Czynnosci w sprawach stanu cywilnego", Text(table.Rows[0].Cells[1]));
        Assert.All(page.Lines, l => Assert.Equal(LineRole.Table, l.Role));
    }

    [Fact]
    public void RuledFragmentEndingThePage_JoinsTheTableOnTheNextPage_FR061_FR065()
    {
        // Bottom of page 1: the header and one row whose term is centred between the lines of its explanation.
        LayoutPage page1 = LayoutFactory.Page(1,
        [
            LayoutFactory.Line("To jest spis okreslen:", C1, 650),
            Row(684, ("Definicje", C1), ("Wyjasnienie", C2)),
            Row(706, ("bank, ktory zawiera umowy", C2)),
            Row(722, ("z akceptantami", C2)),
            Row(729, ("agent rozliczeniowy", C1)),
            Row(737, ("transakcji BLIK.", C2)),
        ]);
        page1.Rulings.Add(new Segment(C1 - 5, 670, 560, 670));
        page1.Rulings.Add(new Segment(C1 - 5, 694, 560, 694));
        page1.Rulings.Add(new Segment(C1 - 5, 760, 560, 760));
        page1.Rulings.Add(new Segment(C2 - 5, 670, C2 - 5, 760));
        LayoutPage page2 = LayoutFactory.Page(2,
        [
            Row(60, ("Definicje", C1), ("Wyjasnienie", C2)),
            Row(90, ("akceptant", C1), ("przedsiebiorca przyjmujacy platnosci", C2)),
            Row(120, ("antena NFC", C1), ("urzadzenie w telefonie", C2)),
            Row(150, ("autoryzacja", C1), ("zgoda na transakcje", C2)),
        ]);

        TableBlock table = SingleTable(Run(page1, page2));

        Assert.Equal(new PageRange(1, 2), table.Pages);
        Assert.Equal(
            [
                ["Definicje", "Wyjasnienie"],
                ["agent rozliczeniowy", "bank, ktory zawiera umowy z akceptantami transakcji BLIK."],
                ["akceptant", "przedsiebiorca przyjmujacy platnosci"],
                ["antena NFC", "urzadzenie w telefonie"],
                ["autoryzacja", "zgoda na transakcje"],
            ],
            Cells(table.Rows));
        Assert.Equal(LineRole.Unknown, page1.Lines[0].Role);
    }

    [Fact]
    public void HangingIndentNumberedParagraphs_AreNotATable()
    {
        // „1.” in its own column, text 20 pt to the right, wrapped lines aligned to the text.
        PipelineContext context = Run(LayoutFactory.Page(1,
        [
            Row(100, ("1.", 72), ("Regulamin okresla zasady otwierania i prowadzenia rachunkow", 95)),
            Row(114, ("oszczednosciowo-rozliczeniowych dla konsumentow.", 95)),
            Row(128, ("2.", 72), ("Ilekroc w Regulaminie jest mowa o Banku, nalezy przez to", 95)),
            Row(142, ("rozumiec Bank Przykladowy S.A. z siedziba w Warszawie.", 95)),
            Row(156, ("3.", 72), ("Bank moze odmowic zawarcia umowy bez podania przyczyny.", 95)),
            Row(170, ("4.", 72), ("Regulamin wchodzi w zycie z dniem ogloszenia.", 95)),
        ]));

        Assert.Empty(context.Tables);
    }

    [Fact]
    public void TwoTextColumnsWithShortLastLines_AreNotATable()
    {
        string left = "Tekst lewej kolumny regulaminu ciagnie sie";
        string right = "Tekst prawej kolumny regulaminu ciagnie sie";
        var lines = new List<LayoutLine>();
        for (int i = 0; i < 9; i++)
        {
            // Every third line ends a paragraph in one of the columns, so one segment is short.
            (string, double) l = i % 3 == 2 ? ("koniec akapitu.", 50) : (left, 50);
            (string, double) r = i % 3 == 1 ? ("koniec.", 320) : (right, 320);
            lines.Add(Row(100 + (14 * i), l, r));
        }

        PipelineContext context = Run(LayoutFactory.Page(1, lines));

        Assert.Empty(context.Tables);
    }
}
