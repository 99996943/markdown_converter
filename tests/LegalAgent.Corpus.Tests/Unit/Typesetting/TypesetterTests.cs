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
    public void ChapterHeading_KeepsTheStylesSpaceAbove()
    {
        Element[] elements = [P("Akapit przed nagłówkiem."), new HeadingElement(2, "II.", T("Karty płatnicze")), P("Akapit po nagłówku.")];
        LayoutStyle style = Style(s => s with { ChapterSpaceAbove = 30 });

        TypesetResult result = Typesetter.Typeset(Doc(elements), style);

        List<Word> words = PageWords(result.Pdf)[0];
        double before = Baseline(words.First(w => w.Text == "nagłówkiem."));
        double heading = Baseline(words.First(w => w.Text == "II."));
        Assert.True(heading - before >= style.Leading + style.ParagraphGap + 30 - 0.5, $"gap {heading - before}");
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
        List<Letter> letters = PageLetters(result.Pdf)[page - 1];

        // The reference marker is a smaller, raised digit glued to the word (the parser's convention, FR-026).
        int at = letters.FindIndex(l => l.Value == "1" && l.PointSize < style.BodySize * 0.85 && 842 - l.StartBaseLine.Y < style.Bottom);
        Assert.True(at > 0, "a small marker digit in the body");
        int dot = at - 1;
        Letter marker = letters[at];
        Assert.Equal(".", letters[dot].Value);
        Assert.Equal("a", letters[dot - 1].Value);
        Assert.True(marker.PointSize < letters[dot].PointSize * 0.85, "marker is smaller");
        Assert.True(marker.StartBaseLine.Y > letters[dot].StartBaseLine.Y + 1, "marker is raised");

        // The footnote starts with its plain label at the bottom of the page, in a small font.
        List<Word> words = PageWords(result.Pdf)[page - 1];
        Word note = words.First(w => w.Text == "placówkach");
        double noteLine = Baseline(words.First(x => x.Text == "Tabela" && Baseline(x) > style.Bottom - 40));
        Word label = words.Where(w => Math.Abs(Baseline(w) - noteLine) < 0.5).OrderBy(w => w.Letters[0].StartBaseLine.X).First();
        Assert.Equal("1)", label.Text);
        Assert.True(Baseline(note) > style.Bottom - 40, "footnote at the bottom of the page");
        Assert.True(Baseline(note) < style.FooterBaseline, "footnote above the footer");

        // Truth words: no marker digits and no footnote labels (the Markdown has [^1] for both).
        Assert.Contains("oprocentowania.", result.Truth.Words);
        Assert.Contains("placówkach", result.Truth.Words);
        Assert.Equal(1, result.Truth.Words.Count(w => w == "1")); // only „1 stycznia” of the front matter
    }

    [Fact]
    public void TwoDigitFootnoteLabel_IsSeparatedFromItsText()
    {
        IReadOnlyList<Inline> text = [new Inline("Zdanie z przypisem."), new Inline("10", Kind: InlineKind.FootnoteRef)];
        var footnotes = new Dictionary<int, IReadOnlyList<Inline>> { [10] = T("Treść dziesiątego przypisu.") };

        TypesetResult result = Typesetter.Typeset(Doc([new ParagraphElement(text)], footnotes), Style());

        List<Word> words = PageWords(result.Pdf)[0];
        Word label = words.First(w => w.Text.StartsWith("10)", StringComparison.Ordinal));
        Assert.Equal("10)", label.Text);
        Word first = words.First(w => w.Text == "Treść");
        Assert.True(first.BoundingBox.Left - label.BoundingBox.Right >= 2, "a gap between the label and the text");
    }

    [Fact]
    public void TwoFootnotesOnOnePage_AreSeparateLabelledLines()
    {
        IReadOnlyList<Inline> text =
        [
            new Inline("Pierwsze zdanie."), new Inline("1", Kind: InlineKind.FootnoteRef),
            new Inline(" Drugie zdanie."), new Inline("2", Kind: InlineKind.FootnoteRef),
        ];
        var footnotes = new Dictionary<int, IReadOnlyList<Inline>>
        {
            [1] = T("Pierwszy przypis. " + LongSentence),
            [2] = T("Drugi przypis."),
        };

        LayoutStyle style = Style();
        TypesetResult result = Typesetter.Typeset(Doc([new ParagraphElement(text)], footnotes), style);

        List<Word> bottom = PageWords(result.Pdf)[0].Where(w => Baseline(w) > style.Bottom - 60 && Baseline(w) < style.FooterBaseline - 1).ToList();
        double first = Baseline(bottom.First(w => w.Text == "Pierwszy"));
        double second = Baseline(bottom.First(w => w.Text == "Drugi"));
        Assert.Contains(bottom, w => w.Text == "1)" && Baseline(w) == first);
        Assert.Contains(bottom, w => w.Text == "2)" && Baseline(w) == second);
        Assert.True(second > first);
    }

    [Fact]
    public void RunningHeaderFooterAndPageNumbers_AreArtifactsWithTheTotalPageCount()
    {
        LayoutStyle style = Style();
        TypesetResult result = Typesetter.Typeset(Doc(Paragraphs(24)), style);

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
