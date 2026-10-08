using LegalAgent.PdfParser.Layout;
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

    private static LayoutLine Header(double top) => Row(top, ("Kolejność działań", TitleLeft), ("Wyjaśnienie", TextLeft));

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
