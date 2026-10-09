using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Typesetting;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using static LegalAgent.Corpus.Tests.Unit.Typesetting.TypesetFixtures;

namespace LegalAgent.Corpus.Tests.Unit.Typesetting;

public sealed class TablesTests
{
    private static readonly TableColumn[] Columns = [new("Lp.", 0.6), new("Usługa", 4), new("Tryb pobierania", 1.6), new("Stawka", 1.4)];

    private static double Baseline(Word w) => 842 - w.Letters[0].StartBaseLine.Y;

    private static TableElement Tariff(int rows, bool? grid, IReadOnlyList<IReadOnlyList<Inline>>? notes = null) =>
        new(
            Columns,
            Enumerable.Range(1, rows).Select(i => (IReadOnlyList<TableCell>)
            [
                TableCell.Of($"{i}."),
                TableCell.Of(i % 3 == 0
                    ? $"Usługa numer {i} wykonywana na wniosek Klienta w placówce Banku lub w systemie bankowości internetowej"
                    : $"Usługa numer {i}"),
                TableCell.Of("za operację"),
                TableCell.Of(i == 2 ? "5,00 zł 1)" : $"{i},00 zł"),
            ]).ToList(),
            grid,
            notes ?? []) { Id = "tab" };

    private static int PathCount(byte[] pdf, int page)
    {
        using PdfDocument doc = PdfDocument.Open(pdf);
        return doc.GetPage(page).Paths.Count;
    }

    [Fact]
    public void GridTableOverThreePages_RepeatsTheHeaderRowButTheTruthHasItOnce()
    {
        TypesetResult result = Typesetter.Typeset(Doc([Tariff(90, grid: true)]), Style());

        PageSpan span = result.ElementPages["tab"];
        Assert.True(span.Last - span.First >= 2, $"table spans pages {span.First}–{span.Last}");
        List<List<Word>> pages = PageWords(result.Pdf);
        for (int p = span.First; p <= span.Last; p++)
        {
            Assert.Contains(pages[p - 1], w => w.Text == "Usługa" && (w.Letters[0].FontName ?? string.Empty).Contains("Bold", StringComparison.Ordinal));
            Assert.True(PathCount(result.Pdf, p) > 10, "grid lines on page " + p);
        }

        Assert.Single(result.Truth.Tables);
        Assert.Equal(["Lp.", "Usługa", "Tryb pobierania", "Stawka"], result.Truth.Tables[0].Header);
        Assert.Equal(90, result.Truth.Tables[0].Rows.Count);
        Assert.Equal(["2.", "Usługa numer 2", "za operację", "5,00 zł 1)"], result.Truth.Tables[0].Rows[1]);
        Assert.Equal(1, result.Truth.Words.Count(w => w == "pobierania"));
        Assert.Contains(result.Truth.Artifacts, a => a.Contains("Tryb pobierania", StringComparison.Ordinal));
    }

    [Fact]
    public void GridlessTable_HasNoLinesAndAlignedColumns()
    {
        TypesetResult result = Typesetter.Typeset(Doc([Tariff(12, grid: false)]), Style());

        Assert.Equal(0, PathCount(result.Pdf, 1));
        List<Word> words = PageWords(result.Pdf)[0];
        double[] modes = words.Where(w => w.Text == "za").Select(w => Math.Round(w.Letters[0].StartBaseLine.X, 1)).Distinct().ToArray();
        Assert.Single(modes);
        Assert.Single(result.Truth.Tables);
    }

    [Fact]
    public void GridDecisionComesFromTheStyleWhenTheTableDoesNotDecide()
    {
        TypesetResult gridless = Typesetter.Typeset(Doc([Tariff(5, grid: null)]), Style(s => s with { TableGrid = false }));
        TypesetResult grid = Typesetter.Typeset(Doc([Tariff(5, grid: null)]), Style(s => s with { TableGrid = true }));

        Assert.Equal(0, PathCount(gridless.Pdf, 1));
        Assert.True(PathCount(grid.Pdf, 1) > 0);
    }

    [Fact]
    public void MultiLineCell_IsWrappedInsideItsColumnAndRowsAreNotSplitAcrossPages()
    {
        LayoutStyle style = Style();
        TypesetResult result = Typesetter.Typeset(Doc([Tariff(90, grid: true)]), style);

        List<List<Word>> pages = PageWords(result.Pdf);
        Word wrapped = pages[0].First(w => w.Text == "internetowej");
        Word mode = pages[0].First(w => w.Text == "pobierania");
        Assert.True(wrapped.BoundingBox.Right < mode.Letters[0].StartBaseLine.X, "service text stays left of the mode column");

        // Each row number is on the same page as the row's rate.
        for (int i = 1; i <= 90; i++)
        {
            string number = $"{i}.";
            string rate = i == 2 ? "5,00" : $"{i},00";
            int pageOfNumber = pages.FindIndex(p => p.Any(w => w.Text == number && w.Letters[0].StartBaseLine.X < 100));
            int pageOfRate = pages.FindIndex(p => p.Any(w => w.Text == rate));
            Assert.Equal(pageOfNumber, pageOfRate);
        }

        Assert.All(pages.SelectMany(p => p).Where(w => Baseline(w) < style.FooterBaseline - 1 && Baseline(w) > style.HeaderBaseline + 1), w => Assert.True(Baseline(w) <= style.Bottom + 0.5));
    }

    [Fact]
    public void FootnoteMarkerInACell_AndItsTextUnderTheTable()
    {
        IReadOnlyList<IReadOnlyList<Inline>> notes = [T("1) Opłata nie jest pobierana od klientów do 26. roku życia.")];
        TypesetResult result = Typesetter.Typeset(Doc([Tariff(6, grid: true, notes), P("Tekst po tabeli.")]), Style());

        List<Word> words = PageWords(result.Pdf)[0];
        Word marker = words.First(w => w.Text == "1)");
        Word note = words.First(w => w.Text == "26.");
        Word lastRate = words.First(w => w.Text == "6,00");
        Word after = words.First(w => w.Text == "tabeli.");
        Assert.True(Baseline(note) > Baseline(lastRate), "the note is under the table");
        Assert.True(Baseline(after) > Baseline(note), "the next paragraph follows the note");
        Assert.True(note.Letters[0].PointSize < marker.Letters[0].PointSize + 0.01);
        Assert.Equal(1, result.Truth.Words.Count(w => w == "26."));
    }

    [Fact]
    public void KeyValueTable_IsAGridWithBoldKeys()
    {
        var card = new KeyValueTableElement(
        [
            new("Oznaczenie", T("BP/PRO/03")),
            new("Wersja", T("2")),
            new("Właściciel", T("Departament Operacji")),
            new("Zatwierdził", T("Zarząd Banku Przykładowego S.A.")),
        ]) { Id = "card" };

        TypesetResult result = Typesetter.Typeset(Doc([card]), Style());

        List<Word> words = PageWords(result.Pdf)[0];
        Assert.Contains("Bold", words.First(w => w.Text == "Właściciel").Letters[0].FontName ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("Bold", words.First(w => w.Text == "Operacji").Letters[0].FontName ?? string.Empty, StringComparison.Ordinal);
        Assert.True(PathCount(result.Pdf, 1) >= 5);
        Assert.Single(result.Truth.Tables);
        Assert.Equal(["Właściciel", "Departament Operacji"], result.Truth.Tables[0].Rows[2]);
        Assert.Equal(Baseline(words.First(w => w.Text == "Właściciel")), Baseline(words.First(w => w.Text == "Departament")), 1);
    }
}
