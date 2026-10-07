using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

public sealed class LineAssemblyStageTests
{
    private const double Size = 11;

    /// <summary>
    /// Lays out <paramref name="text"/> as glyphs starting at <paramref name="x"/>. Letters advance
    /// <c>0.5 * size</c>; a space advances <c>0.26 * size</c>. When <paramref name="explicitSpaces"/> is false,
    /// spaces produce only a horizontal jump and no glyph (PDFs without space characters).
    /// </summary>
    private static List<LayoutGlyph> Text(
        string text,
        double x,
        double baseline,
        double size = Size,
        bool explicitSpaces = true,
        bool bold = false)
    {
        var glyphs = new List<LayoutGlyph>();
        double cursor = x;
        foreach (char c in text)
        {
            if (c == ' ')
            {
                double width = 0.26 * size;
                if (explicitSpaces)
                {
                    glyphs.Add(new LayoutGlyph(" ", new Rect(cursor, baseline - size, cursor + width, baseline + (0.2 * size)), baseline, size, false, false));
                }

                cursor += width;
                continue;
            }

            double advance = 0.5 * size;
            glyphs.Add(new LayoutGlyph(
                c.ToString(),
                new Rect(cursor, baseline - (0.8 * size), cursor + advance, baseline + (0.2 * size)),
                baseline,
                size,
                bold,
                false));
            cursor += advance;
        }

        return glyphs;
    }

    private static LayoutPage Run(
        IEnumerable<LayoutGlyph> glyphs,
        double width = 595,
        double height = 842,
        Action<PdfParserOptions>? configure = null)
    {
        LayoutPage page = Page(glyphs, width, height);
        RunPages(configure, page);
        return page;
    }

    private static LayoutPage Page(IEnumerable<LayoutGlyph> glyphs, double width = 595, double height = 842, int number = 1)
    {
        var page = new LayoutPage(number, width, height);
        foreach (LayoutGlyph g in glyphs)
        {
            page.Glyphs.Add(g);
        }

        return page;
    }

    private static PipelineContext RunPages(Action<PdfParserOptions>? configure, params LayoutPage[] pages)
    {
        var options = new PdfParserOptions();
        configure?.Invoke(options);
        var context = new PipelineContext(options, new SourceInfo(null, pages.Length, null, 0, new string('0', 64)), new ReportBuilder());
        foreach (LayoutPage p in pages)
        {
            context.Pages.Add(p);
        }

        new LineAssemblyStage().Execute(context);
        return context;
    }

    private static string[] LineTexts(LayoutPage page) => page.Lines.Select(l => l.Text).ToArray();

    [Fact]
    public void Order_IsLineAssembly()
    {
        Assert.Equal(StageOrder.LineAssembly, new LineAssemblyStage().Order);
    }

    [Fact]
    public void Execute_BuildsWordsFromExplicitSpaceGlyphs()
    {
        LayoutPage page = Run(Text("Bank pobiera opłatę", 50, 100));

        LayoutLine line = Assert.Single(page.Lines);
        Assert.Equal(["Bank", "pobiera", "opłatę"], line.Words.Select(w => w.Text).ToArray());
        Assert.Equal("Bank pobiera opłatę", line.Text);
    }

    [Fact]
    public void Execute_ReconstructsWordsFromGapsWhenThereAreNoSpaceGlyphs()
    {
        LayoutPage page = Run(Text("Ala ma kota", 50, 100, explicitSpaces: false));

        LayoutLine line = Assert.Single(page.Lines);
        Assert.Equal(["Ala", "ma", "kota"], line.Words.Select(w => w.Text).ToArray());
    }

    /// <summary>
    /// Glyphs of a large font without space characters: advance boxes touch inside a word, but the ink boxes
    /// (side bearings) leave ~5 pt gaps between letters. Words are separated by a 0.33 em advance gap.
    /// </summary>
    private static List<LayoutGlyph> InkInsetText(string text, double x, double baseline, double size)
    {
        var glyphs = new List<LayoutGlyph>();
        double cursor = x;
        foreach (char c in text)
        {
            if (c == ' ')
            {
                cursor += 0.33 * size;
                continue;
            }

            double advance = 0.7 * size;
            double bearing = 0.08 * size;
            glyphs.Add(new LayoutGlyph(
                c.ToString(),
                new Rect(cursor + bearing, baseline - (0.75 * size), cursor + advance - bearing, baseline),
                baseline,
                size,
                true,
                false,
                cursor,
                cursor + advance));
            cursor += advance;
        }

        return glyphs;
    }

    [Fact]
    public void Execute_SeparatesWordsByAdvanceGapsNotInkGaps_RelativeToTheLineFontSize()
    {
        // Real case (mBank terms): 32 pt Verdana title without space glyphs on a page of 9 pt body text.
        List<LayoutGlyph> glyphs = InkInsetText("Regulamin podstawowego rachunku", 40, 80, 32);
        for (int i = 0; i < 20; i++)
        {
            glyphs.AddRange(InkInsetText("tekst podstawowy strony", 40, 200 + (i * 12), 9));
        }

        LayoutPage page = Run(glyphs);

        LayoutLine title = page.Lines[0];
        Assert.Equal(["Regulamin", "podstawowego", "rachunku"], title.Words.Select(w => w.Text).ToArray());
        Assert.Single(title.Segments);
        Assert.All(page.Lines.Skip(1), l => Assert.Equal("tekst podstawowy strony", l.Text));
    }

    [Fact]
    public void Execute_DoesNotSplitWordsWithSmallLetterSpacing()
    {
        // Letters 1 pt apart (tracking) must stay one word: no "spaced-out" output.
        var glyphs = new List<LayoutGlyph>();
        double x = 50;
        foreach (char c in "test")
        {
            glyphs.Add(new LayoutGlyph(c.ToString(), new Rect(x, 92, x + 5.5, 102), 100, Size, false, false));
            x += 6.5;
        }

        LayoutPage page = Run(glyphs);

        LayoutLine line = Assert.Single(page.Lines);
        Assert.Equal(["test"], line.Words.Select(w => w.Text).ToArray());
    }

    [Fact]
    public void Execute_GroupsGlyphsWithSameBaselineIntoOneLineAndSeparatesLinesBelow()
    {
        List<LayoutGlyph> glyphs = [.. Text("pierwsza linia", 50, 100), .. Text("druga linia", 50, 114)];

        LayoutPage page = Run(glyphs);

        Assert.Equal(["pierwsza linia", "druga linia"], LineTexts(page));
    }

    [Fact]
    public void Execute_GroupsGlyphsWhoseBaselinesDifferSlightly()
    {
        List<LayoutGlyph> glyphs = Text("abc", 50, 100);
        glyphs.AddRange(Text("def", 50 + (3 * 5.5), 100.8));

        LayoutPage page = Run(glyphs);

        LayoutLine line = Assert.Single(page.Lines);
        Assert.Equal("abcdef", line.Text);
    }

    [Fact]
    public void Execute_KeepsSuperscriptFootnoteMarkerInItsLine()
    {
        List<LayoutGlyph> glyphs = Text("ustawa", 50, 100);
        // Marker: 7 pt digit raised by 4.5 pt; its baseline differs by more than 30% of its size but its
        // vertical extent lies inside the line.
        glyphs.Add(new LayoutGlyph("1", new Rect(50 + (6 * 5.5), 90, 50 + (6 * 5.5) + 3.5, 97), 95.5, 7, false, false));
        glyphs.AddRange(Text("zawiera", 50 + (6 * 5.5) + 8, 100));

        LayoutPage page = Run(glyphs);

        LayoutLine line = Assert.Single(page.Lines);
        Assert.Contains("ustawa1", line.Words.Select(w => w.Text));
    }

    [Fact]
    public void Execute_DoesNotMergeLinesFarApart()
    {
        List<LayoutGlyph> glyphs = [.. Text("a", 50, 100), .. Text("b", 50, 111)];

        LayoutPage page = Run(glyphs);

        Assert.Equal(2, page.Lines.Count);
    }

    [Fact]
    public void Execute_SortsLinesTopToBottomRegardlessOfGlyphOrder()
    {
        List<LayoutGlyph> glyphs =
        [
            .. Text("dol", 50, 300),
            .. Text("lewa", 50, 100),
            .. Text("srodek", 50, 200),
        ];

        LayoutPage page = Run(glyphs);

        Assert.Equal(["lewa", "srodek", "dol"], LineTexts(page));
    }

    [Fact]
    public void Execute_SameBaselineFarApartHorizontallyIsOneLineWithSegments()
    {
        List<LayoutGlyph> glyphs = [.. Text("Prowadzenie rachunku", 50, 100), .. Text("5 zł", 400, 100)];

        LayoutPage page = Run(glyphs);

        LayoutLine line = Assert.Single(page.Lines);
        Assert.Equal(2, line.Segments.Count);
        Assert.Equal("Prowadzenie rachunku", line.Segments[0].Text);
        Assert.Equal("5 zł", line.Segments[1].Text);
        Assert.Equal(400, line.Segments[1].Box.Left, 0.01);
    }

    [Fact]
    public void Execute_OrdinaryLineHasSingleSegment()
    {
        LayoutPage page = Run(Text("zwykla linia tekstu", 50, 100));

        LayoutLine line = Assert.Single(page.Lines);
        LineSegment segment = Assert.Single(line.Segments);
        Assert.Equal("zwykla linia tekstu", segment.Text);
    }

    [Fact]
    public void Execute_GapBelowCellGapFactorDoesNotSplit()
    {
        // Mean space width = 2.86 pt, factor 2.0 => threshold 5.72 pt. A 5 pt gap is a wide word gap only.
        List<LayoutGlyph> glyphs = [.. Text("ab cd ef", 50, 100)];
        glyphs.AddRange(Text("gh", 50 + (6 * 5.5) + (2 * 2.86) + 5, 100));

        LayoutPage page = Run(glyphs);

        LayoutLine line = Assert.Single(page.Lines);
        Assert.Single(line.Segments);
    }

    [Fact]
    public void Execute_CellGapFactorOptionControlsSplitting()
    {
        List<LayoutGlyph> glyphs = [.. Text("ab cd ef", 50, 100)];
        glyphs.AddRange(Text("gh", 50 + (6 * 5.5) + (2 * 2.86) + 5, 100));

        LayoutPage page = Run(glyphs, configure: o => o.Tables.CellGapFactor = 1.5);

        LayoutLine line = Assert.Single(page.Lines);
        Assert.Equal(2, line.Segments.Count);
    }

    [Fact]
    public void Execute_ComputesZonesFromEachPagesOwnHeight()
    {
        // Same Y coordinates on a portrait and a landscape page must land in different zones.
        List<LayoutGlyph> Glyphs() =>
        [
            .. Text("top", 50, 60),
            .. Text("middle", 50, 300),
            .. Text("bottom", 50, 570),
        ];

        LayoutPage portrait = Page(Glyphs(), 595, 842, 1);
        LayoutPage landscape = Page(Glyphs(), 842, 595, 2);
        RunPages(null, portrait, landscape);

        Assert.Equal([LineZone.Header, LineZone.Body, LineZone.Body], portrait.Lines.Select(l => l.Zone).ToArray());
        Assert.Equal([LineZone.Body, LineZone.Body, LineZone.Footer], landscape.Lines.Select(l => l.Zone).ToArray());
    }

    [Fact]
    public void Execute_ZoneRatioComesFromArtifactOptions()
    {
        LayoutPage page = Run(Text("top", 50, 150), configure: o => o.Artifacts.MarginZoneRatio = 0.2);

        Assert.Equal(LineZone.Header, Assert.Single(page.Lines).Zone);
    }

    [Fact]
    public void Execute_LinesStartWithUnknownRole()
    {
        LayoutPage page = Run(Text("tekst", 50, 100));

        Assert.Equal(LineRole.Unknown, Assert.Single(page.Lines).Role);
    }

    [Fact]
    public void Execute_BoldWordsGetBoldStyle()
    {
        List<LayoutGlyph> glyphs = [.. Text("Art.", 50, 100, bold: true), .. Text(" tekst", 50 + (4 * 5.5), 100)];

        LayoutPage page = Run(glyphs);

        LayoutLine line = Assert.Single(page.Lines);
        Assert.Equal(TextStyle.Bold, line.Words[0].Style);
        Assert.Equal(TextStyle.None, line.Words[1].Style);
    }

    [Fact]
    public void Execute_SetsBodyStyleFromMostFrequentSizeWeightedByCharacters()
    {
        var glyphs = new List<LayoutGlyph>();
        for (int i = 0; i < 6; i++)
        {
            glyphs.AddRange(Text("zwykly tekst akapitu", 50, 100 + (i * 14)));
        }

        glyphs.AddRange(Text("TYTUL", 50, 40, size: 18));
        glyphs.AddRange(Text("przypis", 50, 700, size: 8));
        var page = Page(glyphs);

        PipelineContext context = RunPages(null, page);

        Assert.NotNull(context.BodyStyle);
        Assert.Equal(11, context.BodyStyle.FontSize, 0.001);
        Assert.Equal(14, context.BodyStyle.Leading, 0.01);
    }

    [Fact]
    public void Execute_BodySizeIsRoundedToHalfPoint()
    {
        var glyphs = new List<LayoutGlyph>();
        glyphs.AddRange(Text("tekst tekst tekst", 50, 100, size: 11.3));
        glyphs.AddRange(Text("tekst tekst tekst", 50, 115, size: 11.3));
        LayoutPage page = Page(glyphs);

        PipelineContext context = RunPages(null, page);

        Assert.Equal(11.5, context.BodyStyle!.FontSize, 0.001);
    }

    [Fact]
    public void Execute_LeadingIsMedianOfConsecutiveBodyLineDistancesIgnoringOtherSizes()
    {
        var glyphs = new List<LayoutGlyph>();
        double[] baselines = [100, 114, 128, 142, 200, 214];
        foreach (double b in baselines)
        {
            glyphs.AddRange(Text("zwykly tekst akapitu", 50, b));
        }

        // A heading between body lines must not produce a body-to-body distance.
        glyphs.AddRange(Text("NAGLOWEK", 50, 170, size: 16));
        LayoutPage page = Page(glyphs);

        PipelineContext context = RunPages(null, page);

        Assert.Equal(14, context.BodyStyle!.Leading, 0.01);
    }

    [Fact]
    public void Execute_ProcessesEveryPageAndLeavesSkippedPagesEmpty()
    {
        LayoutPage first = Page(Text("jeden", 50, 100), number: 1);
        var skipped = new LayoutPage(2, 595, 842) { Skipped = SkipReason.NoTextLayer };
        LayoutPage third = Page(Text("trzy", 50, 100), number: 3);

        RunPages(null, first, skipped, third);

        Assert.Equal(["jeden"], LineTexts(first));
        Assert.Empty(skipped.Lines);
        Assert.Equal(["trzy"], LineTexts(third));
    }

    [Fact]
    public void Execute_AlreadyCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        var context = new PipelineContext(
            new PdfParserOptions(),
            new SourceInfo(null, 1, null, 0, new string('0', 64)),
            new ReportBuilder(),
            cts.Token);
        context.Pages.Add(Page(Text("x", 50, 100)));
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => new LineAssemblyStage().Execute(context));
    }

    [Fact]
    public void Execute_OnRealPdfRebuildsLinesAndWords()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page()
            .Text(50, 100, "Bank pobiera opłatę za prowadzenie rachunku.")
            .Text(50, 114, "Druga linia zażółć gęślą jaźń.")
            .Build();
        using StageHarness h = StageHarness.Open(pdf);

        h.Run(new PageExtractionStage(), new TextNormalizationStage(), new LineAssemblyStage());

        LayoutPage page = h.Context.Pages[0];
        Assert.Equal(
            ["Bank pobiera opłatę za prowadzenie rachunku.", "Druga linia zażółć gęślą jaźń."],
            LineTexts(page));
        Assert.All(page.Lines, l => Assert.Single(l.Segments));
        Assert.Equal(11, h.Context.BodyStyle!.FontSize, 0.001);
        Assert.Equal(14, h.Context.BodyStyle.Leading, 0.5);
    }

    // ---- FR-034: side-note column at the page edge ----

    private static readonly string[] MainLines =
    [
        "Tresc glownego przepisu ustawy zajmuje cala szerokosc kolumny tekstu",
        "glownego i jest wyjustowana do prawego marginesu kolumny tekstu ustawy",
        "a obok niej na prawym marginesie stoja noty redakcyjne pisane mniejsza",
        "czcionka ktore informuja o wejsciu w zycie zmian w poszczegolnych art",
    ];

    private static readonly string[] NoteLines = ["Nowe brzmienie pkt 1", "w art. 4 wejdzie w", "zycie po 7 dniach"];

    private static List<LayoutGlyph> SideNotePage(double noteSize)
    {
        var glyphs = new List<LayoutGlyph>();
        for (int i = 0; i < MainLines.Length; i++)
        {
            glyphs.AddRange(Text(MainLines[i], 71, 100 + (20 * i)));
        }

        // Note baselines fall between the main baselines, so ink boxes overlap vertically.
        for (int i = 0; i < NoteLines.Length; i++)
        {
            glyphs.AddRange(Text(NoteLines[i], 480, 106 + (10 * i), noteSize, bold: true));
        }

        return glyphs;
    }

    [Fact]
    public void SideNoteColumn_IsAssembledSeparatelyFromTheMainText_FR034()
    {
        LayoutPage page = Run(SideNotePage(noteSize: 8));

        Assert.Equal(MainLines, page.Lines.Where(l => l.Role != LineRole.SideNote).Select(l => l.Text));
        Assert.Equal(NoteLines, page.Lines.Where(l => l.Role == LineRole.SideNote).Select(l => l.Text));
    }

    [Fact]
    public void NarrowColumnInTheBodyFontSize_IsNotASideNote_FR034()
    {
        LayoutPage page = Run(SideNotePage(noteSize: Size));

        Assert.DoesNotContain(page.Lines, l => l.Role == LineRole.SideNote);
    }

    [Fact]
    public void SideNoteDetection_CanBeDisabled_FR034()
    {
        LayoutPage page = Run(SideNotePage(noteSize: 8), configure: o => o.Layout.DetectSideNotes = false);

        Assert.DoesNotContain(page.Lines, l => l.Role == LineRole.SideNote);
    }

    [Fact]
    public void SideNoteColumnRunningIntoTheFooterZone_StaysOneColumn_FR034()
    {
        List<LayoutGlyph> glyphs = SideNotePage(noteSize: 8);
        glyphs.AddRange(Text("profilu uzytkownika", 480, 790, 8, bold: true));
        glyphs.AddRange(Text("systemu (Dz. U.)", 480, 800, 8, bold: true));
        glyphs.AddRange(Text("s. 2/71", 520, 40, 8));

        LayoutPage page = Run(glyphs);

        Assert.Contains(page.Lines, l => l.Role == LineRole.SideNote && l.Text == "systemu (Dz. U.)");
        Assert.Contains(page.Lines, l => l.Role == LineRole.SideNote && l.Text == "profilu uzytkownika");
        Assert.Contains(page.Lines, l => l.Role != LineRole.SideNote && l.Text == "s. 2/71");
    }
}
