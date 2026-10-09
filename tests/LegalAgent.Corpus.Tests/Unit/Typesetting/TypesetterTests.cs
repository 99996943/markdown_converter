using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Truth;
using LegalAgent.Corpus.Typesetting;
using UglyToad.PdfPig.Content;
using static LegalAgent.Corpus.Tests.Unit.Typesetting.TypesetFixtures;

namespace LegalAgent.Corpus.Tests.Unit.Typesetting;

public sealed class TypesetterTests
{
    private static double Baseline(Word w) => 842 - w.Letters[0].StartBaseLine.Y;

    /// <summary>Words of the body: page words without the running header/footer lines.</summary>
    private static List<string> BodyWords(byte[] pdf, LayoutStyle style) =>
        PageWords(pdf)
            .SelectMany(page => page.Where(w => Baseline(w) > style.HeaderBaseline + 1 && Baseline(w) < style.FooterBaseline - 1))
            .Select(w => w.Text)
            .ToList();

    [Fact]
    public void Paragraph_IsWrappedInsideTheTextAreaAndKeepsEveryWordInOrder()
    {
        LayoutStyle style = Style();
        TypesetResult result = Typesetter.Typeset(Doc(Paragraphs(3)), style);

        foreach (List<Letter> page in PageLetters(result.Pdf))
        {
            Assert.All(page, l => Assert.True(l.EndBaseLine.X <= style.Right + 0.5, $"'{l.Value}' ends at {l.EndBaseLine.X}"));
        }

        Assert.Equal(result.Truth.Words, BodyWords(result.Pdf, style));
        Assert.Contains("przeznaczeniem", result.Truth.Words);
        Assert.True(PageWords(result.Pdf)[0].Select(Baseline).Distinct().Count() > 6, "the paragraphs are wrapped to several lines");
    }

    [Fact]
    public void Headings_AreRecordedWithLevelsAndLabels()
    {
        Element[] elements =
        [
            new HeadingElement(2, "Rozdział 1", T("Postanowienia ogólne")) { Id = "h1" },
            new HeadingElement(3, "§ 1.", []) { Id = "u1" },
            P("Treść paragrafu pierwszego."),
            new HeadingElement(3, "§ 2.", T("Definicje")) { Id = "u2" },
            P("Treść paragrafu drugiego."),
        ];

        TypesetResult result = Typesetter.Typeset(Doc(elements), Style());

        Assert.Equal(
            [
                new TruthHeading(1, null, "Regulamin rachunku testowego"),
                new TruthHeading(2, "Rozdział 1", "Postanowienia ogólne"),
                new TruthHeading(3, "§ 1.", string.Empty),
                new TruthHeading(3, "§ 2.", "Definicje"),
            ],
            result.Truth.Headings);
        Assert.Equal(result.Truth.Words, BodyWords(result.Pdf, Style()));

        // Headings are bold and larger than the body.
        Word unit = PageWords(result.Pdf)[0].First(w => w.Text == "§");
        Assert.Contains("Bold", unit.Letters[0].FontName, StringComparison.Ordinal);
    }

    [Fact]
    public void List_ThreeLevels_RecordsLabelsDepthsAndIndents()
    {
        Element[] elements =
        [
            new ListItemElement("1.", 0, T("Ilekroć w Regulaminie jest mowa o:")),
            new ListItemElement("1)", 1, T("Banku – należy przez to rozumieć Bank Przykładowy S.A.;")),
            new ListItemElement("a)", 2, T("rachunek podstawowy, służący do przechowywania środków pieniężnych i dokonywania rozliczeń, " + LongSentence)),
            new ListItemElement("2.", 0, T("Umowa zostaje zawarta na czas nieokreślony.")),
        ];

        TypesetResult result = Typesetter.Typeset(Doc(elements), Style());

        Assert.Equal(["1.", "1)", "a)", "2."], result.Truth.ListItems.Select(i => i.Label));
        Assert.Equal([0, 1, 2, 0], result.Truth.ListItems.Select(i => i.Depth));
        Assert.StartsWith("Ilekroć w Regulaminie", result.Truth.ListItems[0].FirstWords, StringComparison.Ordinal);

        List<Word> words = PageWords(result.Pdf)[0];
        double x1 = words.First(w => w.Text == "1.").Letters[0].StartBaseLine.X;
        double x2 = words.First(w => w.Text == "1)").Letters[0].StartBaseLine.X;
        double x3 = words.First(w => w.Text == "a)").Letters[0].StartBaseLine.X;
        Assert.True(x1 < x2 && x2 < x3, $"indents {x1} {x2} {x3}");

        // The wrapped continuation of a) is aligned with its text, not with its label.
        Word continuation = words.First(w => w.Text == "przeznaczeniem");
        Assert.True(continuation.Letters[0].StartBaseLine.X > x3);
        Assert.Equal(result.Truth.Words, BodyWords(result.Pdf, Style()));
    }

    [Fact]
    public void Footnote_MarkerInTextAndTextAtTheBottomOfTheSamePage()
    {
        IReadOnlyList<Inline> text = [new Inline("Stawki określa Tabela oprocentowania."), new Inline("1", Kind: InlineKind.FootnoteRef), new Inline(" Dalszy tekst.")];
        var footnotes = new Dictionary<int, IReadOnlyList<Inline>> { [1] = T("Tabela jest dostępna w placówkach Banku.") };
        var elements = new List<Element>(Paragraphs(4)) { new ParagraphElement(text) { Id = "fn" } };
        elements.AddRange(Paragraphs(2));

        LayoutStyle style = Style();
        TypesetResult result = Typesetter.Typeset(Doc(elements, footnotes), style);

        int page = result.ElementPages["fn"].First;
        List<Word> words = PageWords(result.Pdf)[page - 1];
        Word marker = words.First(w => w.Text.StartsWith("oprocentowania", StringComparison.Ordinal));
        Assert.Equal("oprocentowania.¹", marker.Text);

        Word note = words.First(w => w.Text == "placówkach");
        Assert.True(Baseline(note) > style.Bottom - 40, "footnote at the bottom of the page");
        Assert.True(Baseline(note) < style.FooterBaseline, "footnote above the footer");
        Assert.True(Baseline(note) > words.Where(w => w.Text != "placówkach" && Baseline(w) < style.FooterBaseline - 1).Where(w => w.Letters[0].PointSize > 9).Max(Baseline));
        Assert.Contains("placówkach", result.Truth.Words);
        Assert.Equal(result.Truth.Words.Order(StringComparer.Ordinal), BodyWords(result.Pdf, style).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void RunningHeaderFooterAndPageNumbers_AreArtifactsWithTheTotalPageCount()
    {
        LayoutStyle style = Style();
        TypesetResult result = Typesetter.Typeset(Doc(Paragraphs(14)), style);

        Assert.True(result.PageCount >= 3);
        List<List<Word>> pages = PageWords(result.Pdf);
        Assert.Equal(result.PageCount, pages.Count);
        for (int n = 1; n <= result.PageCount; n++)
        {
            string footer = string.Join(' ', pages[n - 1].Where(w => Baseline(w) >= style.FooterBaseline - 1).Select(w => w.Text));
            Assert.EndsWith($"Strona {n} z {result.PageCount}", footer, StringComparison.Ordinal);
            Assert.Contains($"Strona {n} z {result.PageCount}", result.Truth.Artifacts);
        }

        Assert.Contains("Regulamin rachunku testowego – Bank Przykładowy S.A.", result.Truth.Artifacts);
        Assert.DoesNotContain("Strona", result.Truth.Words);
    }

    [Fact]
    public void ParagraphAcrossPages_LosesNoWords()
    {
        string huge = string.Join(' ', Enumerable.Repeat(LongSentence, 40));
        TypesetResult result = Typesetter.Typeset(Doc([P(huge, "big")]), Style());

        Assert.Equal(new PageSpan(1, result.PageCount), result.ElementPages["big"]);
        Assert.True(result.PageCount >= 2);
        Assert.Equal(result.Truth.Words, BodyWords(result.Pdf, Style()));
    }

    [Fact]
    public void Cover_HasBankTitleDesignationAndValidity_AndTheBodyStartsOnPageTwo()
    {
        TypesetResult result = Typesetter.Typeset(Doc(Paragraphs(1), cover: true), Style());

        string cover = string.Join(' ', PageWords(result.Pdf)[0].Select(w => w.Text));
        Assert.Contains("Bank Przykładowy S.A.", cover, StringComparison.Ordinal);
        Assert.Contains("Regulamin rachunku testowego", cover, StringComparison.Ordinal);
        Assert.Contains("BP/REG/01", cover, StringComparison.Ordinal);
        Assert.Contains("1 stycznia 2027 r.", cover, StringComparison.Ordinal);
        Assert.Equal(new PageSpan(2, 2), result.ElementPages["p1"]);
        Assert.Equal(new TruthHeading(1, null, "Regulamin rachunku testowego"), result.Truth.Headings[0]);
    }

    [Fact]
    public void BlockWordCounts_CountPrintedWordsPerBlock()
    {
        Element[] elements =
        [
            new ParagraphElement(T("jeden dwa trzy")) { BlockId = "a" },
            new ListItemElement("1.", 0, T("cztery pięć")) { BlockId = "a" },
            new ParagraphElement(T("sześć")) { BlockId = "b" },
        ];

        TypesetResult result = Typesetter.Typeset(Doc(elements), Style());

        Assert.Equal(6, result.BlockWordCounts["a"]); // the label "1." is a word of the block
        Assert.Equal(1, result.BlockWordCounts["b"]);
    }

    [Fact]
    public void SameDocument_GivesIdenticalBytes()
    {
        ComposedDocument doc = Doc(Paragraphs(8));
        Assert.Equal(Typesetter.Typeset(doc, Style()).Pdf, Typesetter.Typeset(doc, Style()).Pdf);
    }
}
