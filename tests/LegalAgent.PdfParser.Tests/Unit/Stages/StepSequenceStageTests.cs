using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using static LegalAgent.PdfParser.Tests.Fixtures.LayoutFactory;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

/// <summary>
/// FR-067: step schemes („Kolejność działań | Wyjaśnienie”) — shaded boxes with step names on the left, the explanation
/// to their right. Geometry follows the mBank terms: boxes at 64–206 pt, explanation from 212 pt.
/// </summary>
public sealed class StepSequenceStageTests
{
    private const double BoxLeft = 64;
    private const double BoxRight = 206;
    private const double TitleLeft = 70;
    private const double TextLeft = 212;

    /// <summary>One visual line; parts sharing a baseline are merged into one line with several segments, as line assembly does.</summary>
    private static LayoutLine Row(double top, params (string Text, double Left)[] parts)
    {
        List<LayoutLine> pieces = parts.Select(p => Line(p.Text, p.Left, top)).ToList();
        if (pieces.Count == 1)
        {
            return pieces[0];
        }

        List<LayoutWord> words = pieces.SelectMany(p => p.Words).ToList();
        Rect box = pieces.Skip(1).Aggregate(pieces[0].Box, (acc, p) => acc.Union(p.Box));
        var line = new LayoutLine(words, box, pieces[0].Baseline);
        foreach (LayoutLine piece in pieces)
        {
            line.Segments.Add(piece.Segments[0]);
        }

        return line;
    }

    /// <summary>The line set bold, as column-name rows are printed.</summary>
    private static LayoutLine Bold(LayoutLine line)
    {
        static LayoutWord Set(LayoutWord w) => w with { Style = TextStyle.Bold };
        var bold = new LayoutLine(line.Words.Select(Set).ToList(), line.Box, line.Baseline);
        foreach (LineSegment segment in line.Segments)
        {
            bold.Segments.Add(new LineSegment(segment.Words.Select(Set).ToList(), segment.Box));
        }

        return bold;
    }

    private static LayoutLine Header(double top) => Bold(Row(top, ("Kolejność działań", TitleLeft), ("Wyjaśnienie", TextLeft)));

    private static Rect Box(double top, double bottom) => new(BoxLeft, top, BoxRight, bottom);

    private static LayoutPage PageWith(int number, IEnumerable<LayoutLine> lines, params Rect[] areas)
    {
        LayoutPage page = Page(number, lines);
        foreach (Rect area in areas)
        {
            page.FilledAreas.Add(area);
        }

        return page;
    }

    /// <summary>The scheme of page 9 of the mBank terms: three steps, titles vertically centred in their boxes.</summary>
    private static LayoutPage ThreeStepPage() => PageWith(
        1,
        [
            Line("3) Zawarcie umowy wygląda tak:", 90, 100),
            Header(120),
            Row(137, ("Składasz wniosek", TitleLeft), ("Składasz pisemny wniosek.", TextLeft)),
            Line("Zanim przystąpimy do umowy,", TextLeft, 172),
            Row(184, ("Potwierdzamy Twoją", TitleLeft), ("potwierdzimy Twoją tożsamość", TextLeft)),
            Row(196, ("tożsamość", TitleLeft), ("w sposób z regulaminu", TextLeft)),
            Line("obsługi klientów.", TextLeft, 206),
            Line("• Złożenie wniosku nie jest zawarciem umowy.", TextLeft, 238),
            Line("• Sprawdzimy warunek zawarcia umowy,", TextLeft, 250),
            Row(262, ("Sprawdzamy warunek", TitleLeft), ("tj. numer PESEL.", TextLeft + 10)),
            Line("• Umowę zawieramy pisemnie.", TextLeft, 274),
            Line("4) Więcej informacji znajdziesz na stronie.", 90, 320),
        ],
        Box(134, 150),
        Box(170, 216),
        new Rect(69, 184, 200, 206),
        Box(236, 300));

    private static List<string> Texts(LayoutPage page) =>
        page.Lines.Where(l => l.Role != LineRole.Artifact).Select(l => l.Text).ToList();

    private static List<(string Text, string Number)> Titles(PipelineContext context) =>
        context.Pages.SelectMany(p => p.Lines)
            .Where(l => l.Role == LineRole.StepTitle)
            .Select(l => (l.Text, l.Annotations.TryGetValue(LayoutAnnotations.StepNumber, out string? n) ? n : ""))
            .ToList();

    private static PipelineContext Run(PipelineContext context)
    {
        new StepSequenceStage().Execute(context);
        return context;
    }

    [Fact]
    public void Order_IsStepSequence_BeforeTableDetection()
    {
        Assert.Equal(StageOrder.StepSequence, new StepSequenceStage().Order);
        Assert.True(StageOrder.StepSequence > StageOrder.FootnoteDetection && StageOrder.StepSequence < StageOrder.TableDetection);
    }

    [Fact]
    public void Scheme_TitlesAreNumberedAndEachIsFollowedByItsExplanation()
    {
        PipelineContext context = Run(Context([ThreeStepPage()]));

        Assert.Equal(
            [
                "3) Zawarcie umowy wygląda tak:",
                "Składasz wniosek", "Składasz pisemny wniosek.",
                "Potwierdzamy Twoją", "tożsamość",
                "Zanim przystąpimy do umowy,", "potwierdzimy Twoją tożsamość", "w sposób z regulaminu", "obsługi klientów.",
                "Sprawdzamy warunek",
                "• Złożenie wniosku nie jest zawarciem umowy.", "• Sprawdzimy warunek zawarcia umowy,", "tj. numer PESEL.",
                "• Umowę zawieramy pisemnie.",
                "4) Więcej informacji znajdziesz na stronie.",
            ],
            Texts(context.Pages[0]));
        Assert.Equal(
            [("Składasz wniosek", "1"), ("Potwierdzamy Twoją", "2"), ("tożsamość", "2"), ("Sprawdzamy warunek", "3")],
            Titles(context));
    }

    [Fact]
    public void Scheme_ExplanationLinesStayUnclassifiedAndCarryTheirColumn()
    {
        PipelineContext context = Run(Context([ThreeStepPage()]));

        List<LayoutLine> explanation = context.Pages[0].Lines.Where(l => l.Box.Left >= BoxRight).ToList();
        Assert.Equal(9, explanation.Count);
        Assert.All(explanation, l =>
        {
            Assert.Equal(LineRole.Unknown, l.Role);
            Assert.True(l.Annotations.ContainsKey(LayoutAnnotations.StepIndex), l.Text);
            Assert.Equal(TextLeft, LayoutAnnotations.GetNumber(l, LayoutAnnotations.ColumnLeft));
        });
        Assert.All(
            context.Pages[0].Lines.Where(l => l.Box.Left == 90),
            l => Assert.False(l.Annotations.ContainsKey(LayoutAnnotations.StepIndex), l.Text));
    }

    [Fact]
    public void Scheme_ColumnNameRowIsDropped()
    {
        PipelineContext context = Run(Context([ThreeStepPage()]));

        LayoutLine header = Assert.Single(context.Pages[0].Lines, l => l.Text.StartsWith("Kolejność", StringComparison.Ordinal));
        Assert.Equal(LineRole.Artifact, header.Role);
    }

    [Fact]
    public void Scheme_ContinuedOnTheNextPage_EmptyBoxContinuesTheStepAndNumberingGoesOn()
    {
        LayoutPage first = PageWith(
            1,
            [
                Line("5) Przyjęcie przelewu wygląda tak:", 90, 600),
                Header(620),
                Row(637, ("Składasz zlecenie", TitleLeft), ("Zlecenie składasz z datą bieżącą.", TextLeft)),
                Row(700, ("Autoryzujesz", TitleLeft), ("• Zgadzasz się na przelew", TextLeft)),
                Line("i podajesz numer", TextLeft + 10, 712),
            ],
            Box(634, 650),
            Box(690, 725));
        LayoutPage second = PageWith(
            2,
            [
                Header(60),
                Line("telefonu odbiorcy.", TextLeft + 10, 76),
                Line("• Autoryzację opisaliśmy w regulaminie.", TextLeft, 88),
                Row(140, ("Przyjmujemy zlecenie", TitleLeft), ("Przelew zrealizujemy.", TextLeft)),
                Line("6) Z jednym numerem telefonu powiązany jest rachunek.", 90, 200),
            ],
            Box(74, 110),
            Box(130, 160));

        PipelineContext context = Run(Context([first, second]));

        Assert.Equal(
            [("Składasz zlecenie", "1"), ("Autoryzujesz", "2"), ("Przyjmujemy zlecenie", "3")],
            Titles(context));
        Assert.Equal(LineRole.Artifact, context.Pages[1].Lines.Single(l => l.Text.StartsWith("Kolejność", StringComparison.Ordinal)).Role);
        Assert.Equal(
            ["telefonu odbiorcy.", "• Autoryzację opisaliśmy w regulaminie.", "Przyjmujemy zlecenie", "Przelew zrealizujemy.",
                "6) Z jednym numerem telefonu powiązany jest rachunek."],
            Texts(context.Pages[1]));
    }

    public static TheoryData<string> HeaderRowShapes() =>
        ["names on two baselines", "arrow below the names", "name wrapped onto two lines", "name centred beside a wrapped name"];

    /// <summary>Column-name rows as printed in the mBank terms (pages 32, 33 and 47; terms of deposits pages 8–10).</summary>
    [Theory]
    [MemberData(nameof(HeaderRowShapes))]
    public void ColumnNameRow_IsRecognisedInItsPrintedShapes(string shape)
    {
        LayoutLine[] header = shape switch
        {
            "names on two baselines" => [Bold(Line("Wyjaśnienie", TextLeft, 118)), Bold(Line("Kolejność działań", TitleLeft, 120))],
            "arrow below the names" => [Header(94)],
            "name wrapped onto two lines" => [Bold(Row(110, ("Zasady komunikacji", TitleLeft), ("Opis", TextLeft))), Bold(Line("zmian", TitleLeft, 121))],
            _ => [Bold(Line("Zasady komunikacji", TitleLeft, 103)), Bold(Line("Opis", TextLeft, 111)), Bold(Line("zmian", TitleLeft, 119))],
        };
        LayoutPage page = PageWith(
            1,
            [
                Line("Tak brzmi wstęp przed schematem:", 90, 70),
                .. header,
                Row(140, ("Wysyłasz prośbę", TitleLeft), ("Prośbę wyślesz w aplikacji.", TextLeft)),
                Row(206, ("Płatnik akceptuje", TitleLeft), ("Poinformujemy Cię o tym.", TextLeft)),
            ],
            Box(134, 175),
            Box(200, 240));

        PipelineContext context = Run(Context([page]));

        Assert.All(header, l => Assert.Equal(LineRole.Artifact, l.Role));
        Assert.Equal(LineRole.Unknown, page.Lines.Single(l => l.Text.StartsWith("Tak brzmi", StringComparison.Ordinal)).Role);
        Assert.Equal([("Wysyłasz prośbę", "1"), ("Płatnik akceptuje", "2")], Titles(context));
    }

    public static TheoryData<string> NotColumnNameRows() => ["plain above the first box", "partly bold above the first box", "plain between boxes"];

    /// <summary>
    /// T037 — spec 008 FR-634: column-name rows are printed wholly bold; a plain or partly bold line above a box is text
    /// (of the document or of an explanation) and never becomes an artifact.
    /// </summary>
    [Theory]
    [MemberData(nameof(NotColumnNameRows))]
    public void LineAboveABox_NotWhollyBold_IsNotAColumnNameRow(string shape)
    {
        bool between = shape == "plain between boxes";
        LayoutLine line = Row(between ? 182 : 100, ("Uwaga dla klienta", TitleLeft), ("tekst nad polem.", TextLeft));
        if (shape == "partly bold above the first box")
        {
            line = new LayoutLine(
                line.Words.Select(w => w.Box.Left < BoxRight ? w with { Style = TextStyle.Bold } : w).ToList(), line.Box, line.Baseline);
            line.Segments.Add(new LineSegment(line.Words, line.Box));
        }

        LayoutPage page = PageWith(
            1,
            [
                .. between ? Array.Empty<LayoutLine>() : [line],
                Row(117, ("Pierwszy krok", TitleLeft), ("Opis pierwszego kroku.", TextLeft)),
                Row(137, ("Drugi krok", TitleLeft), ("Opis drugiego kroku.", TextLeft)),
                .. between ? [line] : Array.Empty<LayoutLine>(),
                Row(207, ("Trzeci krok", TitleLeft), ("Opis trzeciego kroku.", TextLeft)),
            ],
            Box(114, 130),
            Box(134, 150),
            Box(204, 220));

        Run(Context([page]));

        Assert.Equal(LineRole.Unknown, line.Role);
        Assert.Contains("Uwaga dla klienta tekst nad polem.", Texts(page));
    }

    /// <summary>
    /// T067f — mBank regulation of customer service, page 22: the chapter heading right above the scheme starts left of
    /// the boxes and runs on across the box edge („przekażemy” straddles it); it is not a column-name row.
    /// </summary>
    [Fact]
    public void HeadingRunningAcrossTheBoxEdge_IsNotAColumnNameRow()
    {
        LayoutLine heading = Line("18. Jak i kiedy przekażemy informację o zmianie regulaminu?", BoxLeft - 6, 90);
        LayoutPage page = PageWith(
            1,
            [
                heading,
                Bold(Row(117, ("Zasady komunikacji", TitleLeft), ("Opis", TextLeft))),
                Bold(Line("zmian", TitleLeft, 129)),
                Row(160, ("Jak i kiedy", TitleLeft), ("1) Informację o zmianach przekażemy:", TextLeft)),
                Line("przekażemy", TitleLeft, 172),
            ],
            Box(110, 150),
            Box(152, 200));

        Run(Context([page]));

        Assert.Equal(LineRole.Unknown, heading.Role);
        Assert.False(heading.Annotations.ContainsKey(LayoutAnnotations.StepIndex));
    }

    [Fact]
    public void TextBetweenBoxes_StartsANewScheme()
    {
        LayoutPage page = PageWith(
            1,
            [
                Header(100),
                Row(117, ("Pierwszy krok", TitleLeft), ("Opis pierwszego kroku.", TextLeft)),
                Row(157, ("Drugi krok", TitleLeft), ("Opis drugiego kroku.", TextLeft)),
                Line("6) Przyjęcie przelewu przychodzącego wygląda tak:", 90, 190),
                Header(210),
                Row(227, ("Sprawdzamy numer", TitleLeft), ("Otrzymasz przelew.", TextLeft)),
                Row(267, ("Przyjmujemy zlecenie", TitleLeft), ("Identyfikujemy Cię.", TextLeft)),
            ],
            Box(114, 130),
            Box(154, 170),
            Box(224, 240),
            Box(264, 280));

        PipelineContext context = Run(Context([page]));

        Assert.Equal(
            [("Pierwszy krok", "1"), ("Drugi krok", "2"), ("Sprawdzamy numer", "1"), ("Przyjmujemy zlecenie", "2")],
            Titles(context));
        Assert.Equal(LineRole.Unknown, page.Lines.Single(l => l.Text.StartsWith("6)", StringComparison.Ordinal)).Role);
        Assert.False(page.Lines.Single(l => l.Text.StartsWith("6)", StringComparison.Ordinal)).Annotations.ContainsKey(LayoutAnnotations.StepIndex));
    }

    /// <summary>The arrow between frames as the firm terms print it: an 18 pt symbol-font glyph from the private use area.</summary>
    private const string Arrow = "";

    /// <summary>T033 — spec 008 FR-635: an arrow line between two boxes is a connector, not text that ends the scheme.</summary>
    [Fact]
    public void ArrowGlyphLineBetweenBoxes_IsAnArtifactAndTheSchemeGoesOn()
    {
        LayoutLine arrow = Line(Arrow, 127, 135, 13);
        LayoutPage page = PageWith(
            1,
            [
                Line("Tak brzmi wstęp przed schematem:", 90, 90),
                Row(117, ("Pierwszy krok", TitleLeft), ("Opis pierwszego kroku.", TextLeft)),
                arrow,
                Row(157, ("Drugi krok", TitleLeft), ("Opis drugiego kroku.", TextLeft)),
            ],
            Box(114, 130),
            Box(154, 170));

        PipelineContext context = Run(Context([page]));

        Assert.Equal(LineRole.Artifact, arrow.Role);
        Assert.Equal([("Pierwszy krok", "1"), ("Drugi krok", "2")], Titles(context));
        Assert.Equal(
            ["Tak brzmi wstęp przed schematem:", "Pierwszy krok", "Opis pierwszego kroku.", "Drugi krok", "Opis drugiego kroku."],
            Texts(page));
    }

    /// <summary>T033 — the arrow glyph shares a line with an item of the explanation: the glyph is dropped, the item stays.</summary>
    [Fact]
    public void ArrowGlyphMergedWithAnExplanationLine_IsSplitOff()
    {
        LayoutPage page = PageWith(
            1,
            [
                Row(117, ("Pierwszy krok", TitleLeft), ("1. Pierwszy punkt.", TextLeft)),
                Line("2. Drugi punkt.", TextLeft, 129),
                Row(141, (Arrow, 127), ("3. Trzeci punkt.", TextLeft)),
                Row(167, ("Drugi krok", TitleLeft), ("Opis drugiego kroku.", TextLeft)),
            ],
            Box(114, 152),
            Box(164, 180));

        PipelineContext context = Run(Context([page]));

        Assert.Equal([("Pierwszy krok", "1"), ("Drugi krok", "2")], Titles(context));
        Assert.Equal(
            ["Pierwszy krok", "1. Pierwszy punkt.", "2. Drugi punkt.", "3. Trzeci punkt.", "Drugi krok", "Opis drugiego kroku."],
            Texts(page));
        LayoutLine item = page.Lines.Single(l => l.Text == "3. Trzeci punkt.");
        Assert.Equal(LineRole.Unknown, item.Role);
        Assert.True(item.Annotations.ContainsKey(LayoutAnnotations.StepIndex));
        Assert.Contains(page.Lines, l => l.Text == Arrow && l.Role == LineRole.Artifact);
    }

    /// <summary>T035 — spec 008 FR-632: an item of the explanation printed below its box belongs to that box.</summary>
    [Fact]
    public void ExplanationRunningOnBelowTheBox_BelongsToThatBox()
    {
        LayoutPage page = PageWith(
            1,
            [
                Row(117, ("Pierwszy krok", TitleLeft), ("1. Pierwszy punkt.", TextLeft)),
                Line("2. Drugi punkt.", TextLeft, 129),
                Line("3. Trzeci punkt", TextLeft, 141),
                Line("w dwóch wierszach.", TextLeft + 10, 153),
                Row(177, ("Drugi krok", TitleLeft), ("Opis drugiego kroku.", TextLeft)),
            ],
            Box(114, 140),
            Box(174, 190));

        PipelineContext context = Run(Context([page]));

        Assert.Equal([("Pierwszy krok", "1"), ("Drugi krok", "2")], Titles(context));
        Assert.Equal(
            ["Pierwszy krok", "1. Pierwszy punkt.", "2. Drugi punkt.", "3. Trzeci punkt", "w dwóch wierszach.", "Drugi krok", "Opis drugiego kroku."],
            Texts(page));
        Assert.All(
            page.Lines.Where(l => l.Box.Left >= TextLeft),
            l => Assert.Equal(TextLeft, LayoutAnnotations.GetNumber(l, LayoutAnnotations.ColumnLeft)));
    }

    public static TheoryData<string> FlowEnds() => ["wide gap", "text on the left"];

    /// <summary>T035 — the explanation below the last box ends at a wider gap or at text reaching left of the box edge.</summary>
    [Theory]
    [MemberData(nameof(FlowEnds))]
    public void ExplanationBelowTheBox_EndsAtAWideGapOrAtTextOnTheLeft(string end)
    {
        LayoutLine after = end == "wide gap" ? Line("Akapit po schemacie.", TextLeft, 200) : Line("Dalszy tekst po prawej.", TextLeft, 193);
        var lines = new List<LayoutLine>
        {
            Row(117, ("Pierwszy krok", TitleLeft), ("Opis pierwszego kroku.", TextLeft)),
            Row(157, ("Drugi krok", TitleLeft), ("Opis drugiego kroku.", TextLeft)),
            Line("ciąg dalszy opisu.", TextLeft, 169),
        };
        if (end == "text on the left")
        {
            lines.Add(Line("Tekst od lewego marginesu.", 90, 181));
        }

        lines.Add(after);
        LayoutPage page = PageWith(1, lines, Box(114, 130), Box(154, 166));

        Run(Context([page]));

        Assert.True(page.Lines.Single(l => l.Text == "ciąg dalszy opisu.").Annotations.ContainsKey(LayoutAnnotations.StepIndex));
        Assert.False(after.Annotations.ContainsKey(LayoutAnnotations.StepIndex));
        Assert.Equal(LineRole.Unknown, after.Role);
        Assert.Equal(after, page.Lines[^1]);
    }

    /// <summary>
    /// T035 — a frame broken by a page boundary: on the next page an empty shaded box with the rest of the explanation
    /// below it continues the step; its phrase is printed once and the empty box gives no step name.
    /// </summary>
    [Fact]
    public void FrameBrokenByAPageBoundary_ContinuesBelowAnEmptyBoxOnTheNextPage()
    {
        LayoutPage first = PageWith(
            1,
            [
                Row(637, ("Składasz zlecenie", TitleLeft), ("Zlecenie składasz z datą bieżącą.", TextLeft)),
                Row(700, ("Autoryzujesz", TitleLeft), ("• Zgadzasz się na przelew", TextLeft)),
                Line("i podajesz numer", TextLeft + 10, 712),
            ],
            Box(634, 650),
            Box(690, 740));
        LayoutPage second = PageWith(
            2,
            [
                Line("telefonu odbiorcy.", TextLeft + 10, 72),
                Line("• Autoryzację opisaliśmy w regulaminie.", TextLeft, 84),
                Row(140, ("Przyjmujemy zlecenie", TitleLeft), ("Przelew zrealizujemy.", TextLeft)),
                Line("6) Z jednym numerem telefonu powiązany jest rachunek.", 90, 200),
            ],
            Box(60, 70),
            Box(130, 160));

        PipelineContext context = Run(Context([first, second]));

        Assert.Equal(
            [("Składasz zlecenie", "1"), ("Autoryzujesz", "2"), ("Przyjmujemy zlecenie", "3")],
            Titles(context));
        Assert.Equal(
            ["telefonu odbiorcy.", "• Autoryzację opisaliśmy w regulaminie.", "Przyjmujemy zlecenie", "Przelew zrealizujemy.",
                "6) Z jednym numerem telefonu powiązany jest rachunek."],
            Texts(second));
        Assert.True(second.Lines[0].Annotations.ContainsKey(LayoutAnnotations.StepIndex));
    }

    /// <summary>
    /// T040a — the firm terms: the tall arrow glyph pulls the first word of a wrapped explanation line into its own line,
    /// the rest of that line is a line of its own on the same baseline; once the arrow is split off, the two parts are
    /// one line again.
    /// </summary>
    [Fact]
    public void LinePartSplitOffAnArrow_RejoinsTheRestOfItsLine()
    {
        double rest = TextLeft + 15 + TextWidth("dalszy x") - TextWidth("x");
        LayoutPage page = PageWith(
            1,
            [
                Row(117, ("Pierwszy krok", TitleLeft), ("1. Pierwszy punkt.", TextLeft)),
                Line("2. Drugi punkt oraz", TextLeft, 129),
                Row(141, (Arrow, 127), ("dalszy", TextLeft + 15)),
                Line("ciąg punktu.", rest, 141),
                Row(167, ("Drugi krok", TitleLeft), ("Opis drugiego kroku.", TextLeft)),
            ],
            Box(114, 140),
            Box(164, 180));

        Run(Context([page]));

        Assert.Equal(
            ["Pierwszy krok", "1. Pierwszy punkt.", "2. Drugi punkt oraz", "dalszy ciąg punktu.", "Drugi krok", "Opis drugiego kroku."],
            Texts(page));
        LayoutLine joined = page.Lines.Single(l => l.Text == "dalszy ciąg punktu.");
        Assert.Single(joined.Segments);
        Assert.True(joined.Annotations.ContainsKey(LayoutAnnotations.StepIndex));
    }

    /// <summary>
    /// T040c — the firm terms page 5: the arrow pulls the label „3.” of an item into its line; the text of the item lies
    /// on the label's baseline after a label gap (≈ 8 pt, wider than the short label line is tall) and still rejoins it.
    /// </summary>
    [Fact]
    public void LabelSplitOffAnArrow_RejoinsItsTextAfterTheLabelGap()
    {
        // A label of digits has no descenders: its line is 6 pt tall, shorter than the label gap.
        LayoutLine arrow = Line(Arrow, 127, 137, 13);
        LayoutLine label = Line("3.", TextLeft, 143.8, 6);
        var merged = new LayoutLine([.. arrow.Words, .. label.Words], arrow.Box.Union(label.Box), label.Baseline);
        merged.Segments.Add(arrow.Segments[0]);
        merged.Segments.Add(label.Segments[0]);
        LayoutPage page = PageWith(
            1,
            [
                Row(117, ("Pierwszy krok", TitleLeft), ("1. Pierwszy punkt.", TextLeft)),
                Line("2. Drugi punkt.", TextLeft, 129),
                merged,
                Line("Możemy poprosić o dokumenty", TextLeft + TextWidth("3.") + 8, 141),
                Line("potwierdzające dane.", TextLeft + TextWidth("3.") + 8, 153),
                Row(177, ("Drugi krok", TitleLeft), ("Opis drugiego kroku.", TextLeft)),
            ],
            Box(114, 140),
            Box(174, 190));

        Run(Context([page]));

        Assert.Equal(
            ["Pierwszy krok", "1. Pierwszy punkt.", "2. Drugi punkt.", "3. Możemy poprosić o dokumenty", "potwierdzające dane.",
                "Drugi krok", "Opis drugiego kroku."],
            Texts(page));
    }

    public static TheoryData<string> SingleBoxes() =>
        ["guarded", "plain name", "wide box", "ruling in the band", "nothing to the right", "no name in the box"];

    /// <summary>
    /// T039 — spec 008 FR-633: a single shaded box is a one-frame scheme only behind the guard — a bold name (≥ 80% of
    /// its characters) in a box narrower than half the page, text to its right, no ruling across the band of the box.
    /// White fills never reach <see cref="LayoutPage.FilledAreas"/> (page extraction drops them).
    /// </summary>
    [Theory]
    [MemberData(nameof(SingleBoxes))]
    public void SingleBox_IsAOneFrameSchemeOnlyBehindTheGuard(string shape)
    {
        double right = shape == "wide box" ? 400 : BoxRight;
        double text = shape == "wide box" ? 420 : TextLeft;
        LayoutLine name = Line("Sprawdzamy dokumenty", TitleLeft, 127);
        if (shape != "plain name")
        {
            name = Bold(name);
        }

        var lines = new List<LayoutLine> { Line("Tak brzmi wstęp przed ramką:", 90, 90) };
        if (shape != "no name in the box")
        {
            lines.Add(name);
        }

        if (shape != "nothing to the right")
        {
            lines.Add(Line("1. Zanim zawrzemy umowę, sprawdzimy dokumenty.", text, 117));
            lines.Add(Line("2. Możemy poprosić o dokumenty.", text, 129));
            lines.Add(Line("3. Przekażemy Ci szczegóły.", text, 141));
        }

        LayoutPage page = PageWith(1, lines, new Rect(BoxLeft, 114, right, 154));
        if (shape == "ruling in the band")
        {
            page.Rulings.Add(new Segment(BoxRight + 2, 110, BoxRight + 2, 158));
        }

        PipelineContext context = Run(Context([page]));

        if (shape == "guarded")
        {
            Assert.Equal([("Sprawdzamy dokumenty", "1")], Titles(context));
            Assert.Equal(
                ["Tak brzmi wstęp przed ramką:", "Sprawdzamy dokumenty", "1. Zanim zawrzemy umowę, sprawdzimy dokumenty.",
                    "2. Możemy poprosić o dokumenty.", "3. Przekażemy Ci szczegóły."],
                Texts(page));
            Assert.All(page.Lines.Skip(2), l => Assert.Equal(TextLeft, LayoutAnnotations.GetNumber(l, LayoutAnnotations.ColumnLeft)));
        }
        else
        {
            Assert.Empty(Titles(context));
            Assert.All(page.Lines, l => Assert.Equal(LineRole.Unknown, l.Role));
            Assert.All(page.Lines, l => Assert.False(l.Annotations.ContainsKey(LayoutAnnotations.StepIndex), l.Text));
        }
    }

    public static TheoryData<string> NotSchemes() => ["single box", "wide boxes", "nothing to the right", "disabled"];

    [Theory]
    [MemberData(nameof(NotSchemes))]
    public void LayoutsThatAreNotSchemes_AreLeftUntouched(string layout)
    {
        double right = layout == "wide boxes" ? 400 : BoxRight;
        bool textRight = layout != "nothing to the right";
        var lines = new List<LayoutLine>
        {
            textRight ? Row(117, ("Pierwszy krok", TitleLeft), ("Opis pierwszego kroku.", 420)) : Line("Pierwszy krok", TitleLeft, 117),
        };
        var areas = new List<Rect> { new(BoxLeft, 114, right, 130) };
        if (layout != "single box")
        {
            lines.Add(textRight ? Row(157, ("Drugi krok", TitleLeft), ("Opis drugiego kroku.", 420)) : Line("Drugi krok", TitleLeft, 157));
            areas.Add(new Rect(BoxLeft, 154, right, 170));
        }

        LayoutPage page = PageWith(1, lines, [.. areas]);
        List<string> before = page.Lines.Select(l => l.Text).ToList();

        Run(Context([page], o => o.Tables.DetectStepSequences = layout != "disabled"));

        Assert.Equal(before, page.Lines.Select(l => l.Text));
        Assert.All(page.Lines, l => Assert.Equal(LineRole.Unknown, l.Role));
        Assert.All(page.Lines, l => Assert.False(l.Annotations.ContainsKey(LayoutAnnotations.StepIndex)));
    }

    /// <summary>Two wide boxes with long step names and long explanation lines: without care they look like two text columns.</summary>
    private static LayoutPage WideNamesPage()
    {
        const double right = 290;
        const double text = 300;
        const string explanation = "Tekst wyjaśnienia kroku o stałej długości linii";
        var lines = new List<LayoutLine>();
        foreach (double top in new[] { 100.0, 180.0 })
        {
            lines.Add(Line($"T{top} Sprawdzamy warunek zawarcia umowy", TitleLeft, top + 18));
            lines.Add(Line($"U{top} oraz otwieramy rachunek klienta", TitleLeft, top + 30));
            for (int i = 0; i < 5; i++)
            {
                lines.Add(Line($"E{top}.{i} {explanation}", text, top + (i * 12)));
            }
        }

        return PageWith(1, lines, new Rect(BoxLeft, 98, right, 160), new Rect(BoxLeft, 178, right, 240));
    }

    [Fact]
    public void SchemeLines_AreNotReadAsTwoTextColumns()
    {
        PipelineContext context = Run(Context([WideNamesPage()]));
        List<string> stepwise = context.Pages[0].Lines.Select(l => l.Text).ToList();

        new ReadingOrderStage().Execute(context);

        Assert.Equal(stepwise, context.Pages[0].Lines.Select(l => l.Text));
        Assert.StartsWith("T100", stepwise[0], StringComparison.Ordinal);
        Assert.StartsWith("E100.0", stepwise[2], StringComparison.Ordinal);
    }

    [Fact]
    public void SchemeExplanation_IsNotATable()
    {
        LayoutPage page = PageWith(
            1,
            [
                Header(100),
                Row(117, ("Pierwszy krok", TitleLeft), ("Opłata", TextLeft), ("0 zł", 450)),
                Row(137, ("Prowizja", TextLeft), ("1 zł", 450)),
                Row(157, ("Odsetki", TextLeft), ("2 zł", 450)),
                Row(197, ("Drugi krok", TitleLeft), ("Opłata", TextLeft), ("5 zł", 450)),
                Row(217, ("Prowizja", TextLeft), ("6 zł", 450)),
            ],
            Box(114, 170),
            Box(194, 230));
        PipelineContext context = Run(Context([page]));
        context.BodyStyle = new BodyStyle(10, 20);

        new TableDetectionStage().Execute(context);

        Assert.Empty(context.Tables);
        Assert.DoesNotContain(page.Lines, l => l.Role == LineRole.Table);
    }
}
