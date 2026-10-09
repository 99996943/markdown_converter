using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Truth;
using LegalAgent.Corpus.Typesetting;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using static LegalAgent.Corpus.Tests.Unit.Typesetting.TypesetFixtures;

namespace LegalAgent.Corpus.Tests.Unit.Typesetting;

public sealed class SpecialLayoutsTests
{
    private static double Baseline(Word w) => 842 - w.Letters[0].StartBaseLine.Y;

    private static double X(Word w) => w.Letters[0].StartBaseLine.X;

    private static bool IsBold(Word w) => (w.Letters[0].FontName ?? string.Empty).Contains("Bold", StringComparison.Ordinal);

    private static List<string> BodyWords(byte[] pdf, LayoutStyle style) =>
        PageWords(pdf)
            .SelectMany(page => page.Where(w => Baseline(w) > style.HeaderBaseline + 1 && Baseline(w) < style.FooterBaseline - 1))
            .Select(w => w.Text)
            .ToList();

    private static List<PdfSubpath> Lines(byte[] pdf, int page)
    {
        using PdfDocument doc = PdfDocument.Open(pdf);
        return doc.GetPage(page).Paths.SelectMany(p => p).ToList();
    }

    private static int ImageCount(byte[] pdf, int page)
    {
        using PdfDocument doc = PdfDocument.Open(pdf);
        return doc.GetPage(page).GetImages().Count();
    }

    // ------------------------------------------------------------------ two columns

    [Fact]
    public void TwoColumns_FillTheLeftColumnFirstAndKeepReadingOrder()
    {
        LayoutStyle style = Style(s => s with { Columns = 2, ColumnGap = 30 });
        var elements = new List<Element>();
        for (int i = 1; i <= 8; i++)
        {
            elements.Add(new HeadingElement(3, $"§ {i}.", []));
            elements.AddRange(Paragraphs(1).Select(p => (Element)((ParagraphElement)p with { Id = "p" + i })));
        }

        TypesetResult result = Typesetter.Typeset(Doc(elements), style);

        List<Word> page1 = PageWords(result.Pdf)[0];
        double middle = (style.Left + style.Right) / 2;
        Word[] units = page1.Where(w => w.Text == "§").ToArray();
        Assert.Contains(units, w => X(w) < middle);
        Assert.Contains(units, w => X(w) > middle);

        // Reading order of the truth = left column top to bottom, then the right column.
        // The title block spans both columns; the columns start at the first unit heading.
        double columnsTop = units.Min(Baseline);
        int titleWords = result.Truth.Words.Count - result.Truth.Words.SkipWhile(w => w != "§").Count();
        var body = page1.Where(w => Baseline(w) >= columnsTop - 1 && Baseline(w) < style.FooterBaseline - 1).ToList();
        var ordered = body.Where(w => X(w) < middle).Concat(body.Where(w => X(w) >= middle)).Select(w => w.Text).ToList();
        Assert.Equal(ordered, result.Truth.Words.Skip(titleWords).Take(ordered.Count));
        Assert.All(body, w => Assert.False(w.BoundingBox.Left < middle && w.BoundingBox.Right > middle, $"'{w.Text}' crosses the gutter"));
        Assert.Equal(8, result.Truth.Headings.Count(h => h.Label?.StartsWith('§') == true));
    }

    // ------------------------------------------------------------------ table-document (FR-080)

    [Fact]
    public void TableDocument_SectionNamesInTheLeftCellContentOnTheRightInsideAFrame()
    {
        LayoutStyle style = Style(s => s with { TableDocument = true });
        var elements = new List<Element>();
        foreach (string section in new[] { "Organizator promocji", "Uczestnik promocji", "Warunki promocji", "Reklamacje" })
        {
            elements.Add(new HeadingElement(2, null, T(section)));
            elements.AddRange(Paragraphs(3));
            elements.Add(new ListItemElement("1)", 0, T("pierwszy warunek promocji;")));
            elements.Add(new ListItemElement("2)", 0, T("drugi warunek promocji.")));
        }

        TypesetResult result = Typesetter.Typeset(Doc(elements), style);

        Assert.True(result.PageCount >= 3);
        Assert.Equal(["Organizator promocji", "Uczestnik promocji", "Warunki promocji", "Reklamacje"], result.Truth.Headings.Where(h => h.Level == 2).Select(h => h.Text));
        List<List<Word>> pages = PageWords(result.Pdf);
        Word name = pages.SelectMany(p => p).First(w => w.Text == "Organizator");
        Assert.True(IsBold(name));
        Assert.InRange(X(name), 55, 70);
        Word content = pages.SelectMany(p => p).First(w => w.Text == "Akapit");
        Assert.InRange(X(content), 182, 200);

        // Frame: vertical rulings at 54, 181 and 541 on every body page; horizontal rulings in two pieces.
        for (int p = 1; p <= result.PageCount; p++)
        {
            List<PdfSubpath> lines = Lines(result.Pdf, p);
            foreach (double x in new[] { 54.0, 181, 541 })
            {
                Assert.Contains(lines, l => l.GetBoundingRectangle() is { } r && Math.Abs(r.Left - x) < 0.6 && r.Height > 50);
            }

            Assert.Contains(lines, l => l.GetBoundingRectangle() is { } r && Math.Abs(r.Right - 181) < 1.5 && r.Width > 100 && r.Height < 1);
            Assert.Contains(lines, l => l.GetBoundingRectangle() is { } r && Math.Abs(r.Left - 181) < 0.6 && r.Width > 300 && r.Height < 1);
        }

        // The column-name row stands on every page and is not content.
        Assert.All(pages, p => Assert.Contains(p, w => w.Text == "Postanowienia" && IsBold(w) && X(w) > 180));
        Assert.DoesNotContain("Zagadnienie", result.Truth.Words);
        Assert.Equal(result.Truth.Words.Order(StringComparer.Ordinal), BodyWords(result.Pdf, style).Where(w => w is not ("Zagadnienie" or "Postanowienia")).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TableDocument_PrintsSectionNamesWithoutChapterLabelsAndNoUnitLabels()
    {
        LayoutStyle style = Style(s => s with { TableDocument = true });
        Element[] elements =
        [
            new HeadingElement(2, "Rozdział 1", T("Organizator promocji")),
            new HeadingElement(3, "§ 1.", []) { Unit = "§ 1" },
            P("Promocję organizuje Bank Przykładowy S.A."),
            new HeadingElement(2, "Rozdział 2", T("Uczestnik promocji")),
            new HeadingElement(3, "§ 2.", []),
            P("W promocji mogą uczestniczyć konsumenci."),
        ];

        TypesetResult result = Typesetter.Typeset(Doc(elements), style);

        string text = string.Join(' ', PageWords(result.Pdf).SelectMany(p => p).Select(w => w.Text));
        Assert.DoesNotContain("Rozdział", text, StringComparison.Ordinal);
        Assert.DoesNotContain("§", text, StringComparison.Ordinal);
        Assert.Equal(
            [new TruthHeading(2, null, "Organizator promocji"), new TruthHeading(2, null, "Uczestnik promocji")],
            result.Truth.Headings.Skip(1));
        Assert.DoesNotContain("Rozdział", result.Truth.Words);
    }

    [Fact]
    public void TableDocument_RowCrossingAPage_ContinuesWithAnEmptyLeftCell()
    {
        LayoutStyle style = Style(s => s with { TableDocument = true });
        var elements = new List<Element> { new HeadingElement(2, null, T("Korzyści promocji")) };
        elements.AddRange(Paragraphs(20));

        TypesetResult result = Typesetter.Typeset(Doc(elements), style);

        Assert.True(result.PageCount >= 2);
        List<Word> page2 = PageWords(result.Pdf)[1];
        Assert.DoesNotContain(page2, w => X(w) < 175 && Baseline(w) > 100 && Baseline(w) < style.FooterBaseline - 1);
        Assert.Contains(page2, w => w.Text == "Akapit");
    }

    // ------------------------------------------------------------------ step scheme (FR-067)

    [Fact]
    public void StepScheme_GrayBoxesWithNamesArrowsAndExplanationsOnTheRight()
    {
        var scheme = new StepSchemeElement(
        [
            new SchemeStep("Przyjęcie zgłoszenia", [T("Pracownik przyjmuje zgłoszenie klienta i rejestruje je w systemie.")]),
            new SchemeStep("Weryfikacja tożsamości", [T("Pracownik potwierdza tożsamość klienta na podstawie dokumentu."), T("W razie wątpliwości stosuje dodatkową weryfikację.")]),
            new SchemeStep("Decyzja", [T("Kierownik zatwierdza decyzję.")]),
        ]) { Id = "scheme" };

        LayoutStyle style = Style();
        TypesetResult result = Typesetter.Typeset(Doc([P("Przebieg procedury przedstawia schemat."), scheme, P("Tekst po schemacie.")]), style);

        List<Word> words = PageWords(result.Pdf)[0];
        Word name = words.First(w => w.Text == "Weryfikacja");
        Word explanation = words.First(w => w.Text == "potwierdza");
        Assert.True(IsBold(name));
        Assert.True(X(explanation) > X(name) + 100);

        // Gray boxes ≤ 50% of the page width, left and right edges aligned, one arrow image between consecutive boxes.
        using (PdfDocument doc = PdfDocument.Open(result.Pdf))
        {
            var boxes = doc.GetPage(1).Paths.Where(p => p.IsFilled).Select(p => p.GetBoundingRectangle()!.Value).Where(r => r.Height > 10).ToList();
            Assert.Equal(3, boxes.Count);
            Assert.All(boxes, b => Assert.True(b.Width <= 595 / 2.0));
            Assert.All(boxes, b => Assert.Equal(boxes[0].Left, b.Left, 1));
            Assert.All(boxes, b => Assert.Equal(boxes[0].Right, b.Right, 1));
        }

        Assert.Equal(2, ImageCount(result.Pdf, 1));

        // Step names are not headings; the column-name row is not content.
        Assert.DoesNotContain(result.Truth.Headings, h => h.Text.Contains("Decyzja", StringComparison.Ordinal));
        Assert.Contains(words, w => w.Text == "Kolejność" && IsBold(w));
        Assert.DoesNotContain("Kolejność", result.Truth.Words);
        Assert.Contains("Weryfikacja", result.Truth.Words);
    }

    // ------------------------------------------------------------------ checklists and callout

    [Fact]
    public void Checklist_VectorForm_DrawsBoxesThatAreNotWords()
    {
        var list = new ChecklistElement(ChecklistForm.Vector, [T("Sprawdzono dokument tożsamości."), T("Zarejestrowano wniosek w systemie.")]);
        TypesetResult result = Typesetter.Typeset(Doc([list]), Style());

        Assert.Equal(["Sprawdzono", "dokument", "tożsamości.", "Zarejestrowano", "wniosek", "w", "systemie."], result.Truth.Words.TakeLast(7));
        Assert.Equal(result.Truth.Words, BodyWords(result.Pdf, Style()));
        Assert.True(Lines(result.Pdf, 1).Count >= 2, "a vector box per item");
    }

    [Fact]
    public void Checklist_TextForm_UsesTheMonospaceBox()
    {
        var list = new ChecklistElement(ChecklistForm.Text, [T("Sprawdzono dokument tożsamości."), T("Zarejestrowano wniosek.")]);
        TypesetResult result = Typesetter.Typeset(Doc([list]), Style());

        Word box = PageWords(result.Pdf)[0].First(w => w.Text == "□");
        Assert.Contains("Mono", box.Letters[0].FontName ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal([new TruthListItem("□", 0, "Sprawdzono dokument tożsamości."), new TruthListItem("□", 0, "Zarejestrowano wniosek.")], result.Truth.ListItems);
    }

    [Fact]
    public void Checklist_TableForm_HasLpActivityAndDoneColumns()
    {
        var list = new ChecklistElement(ChecklistForm.Table, [T("Sprawdzono dokument tożsamości."), T("Zarejestrowano wniosek.")]);
        TypesetResult result = Typesetter.Typeset(Doc([list]), Style());

        TruthTable table = Assert.Single(result.Truth.Tables);
        Assert.Equal(["Lp.", "Czynność", "Wykonano"], table.Header);
        Assert.Equal(["2.", "Zarejestrowano wniosek.", string.Empty], table.Rows[1]);
    }

    [Fact]
    public void Callout_IsFramedAndKeepsItsWords()
    {
        var callout = new CalloutElement(T("Uwaga: zmiana stawek obowiązuje od 1 stycznia 2027 r. " + LongSentence)) { Id = "box" };
        TypesetResult result = Typesetter.Typeset(Doc([P("Przed ramką."), callout, P("Po ramce.")]), Style());

        Assert.Equal(result.Truth.Words, BodyWords(result.Pdf, Style()));
        Assert.True(Lines(result.Pdf, 1).Count >= 4, "a frame around the text");
        Assert.Equal(new PageSpan(1, 1), result.ElementPages["box"]);
    }
}
