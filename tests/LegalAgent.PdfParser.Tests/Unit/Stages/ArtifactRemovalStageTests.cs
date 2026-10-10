using System.Globalization;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using static LegalAgent.PdfParser.Tests.Fixtures.LayoutFactory;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

public sealed class ArtifactRemovalStageTests
{
    private const string Registry = "Bank Przykładowy S.A. z siedzibą w Warszawie, KRS 0000000000";

    private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<LayoutLine> Body(int page) =>
    [
        Line($"Treść merytoryczna strony {N(page)} pierwszy akapit.", 72, 120),
        Line("Kolejna linia treści merytorycznej.", 72, 134),
    ];

    private static PipelineContext Run(PipelineContext context)
    {
        new ArtifactRemovalStage().Execute(context);
        return context;
    }

    private static void AssertBodyKept(PipelineContext context)
    {
        foreach (LayoutPage page in context.Pages)
        {
            Assert.Contains(page.Lines, l => l.Text.StartsWith("Treść merytoryczna strony", StringComparison.Ordinal));
            Assert.Contains(page.Lines, l => l.Text == "Kolejna linia treści merytorycznej.");
        }
    }

    [Fact]
    public void Order_IsArtifactRemoval()
    {
        Assert.Equal(StageOrder.ArtifactRemoval, new ArtifactRemovalStage().Order);
    }

    // (a)
    [Fact]
    public void DziennikUstawHeader_IsRemovedFromAllPages_IncludingEmbeddedPageOneVariant()
    {
        PipelineContext context = Run(Document(6, n => Body(n).Append(n == 1
            ? Line("Dziennik Ustaw – 1 – Poz. 1234 · Ustawa z dnia 5 czerwca 2024 r.", 72, 30)
            : Line($"Dziennik Ustaw – {N(n)} – Poz. 1234", 200, 30))));

        Assert.DoesNotContain(AllLineTexts(context), t => t.Contains("Dziennik Ustaw", StringComparison.Ordinal));
        Assert.Contains(context.Pages[0].Lines, l => l.Text == "Ustawa z dnia 5 czerwca 2024 r.");
        AssertBodyKept(context);
    }

    // (b)
    [Fact]
    public void MirroredOddEvenHeaders_AreRemoved()
    {
        PipelineContext context = Run(Document(6, n => Body(n).Append(n % 2 == 1
            ? Line("Ustawa o usługach płatniczych", 72, 30)
            : RightAligned($"Dziennik Ustaw – {N(n)} – Poz. 1234", 523, 30))));

        Assert.DoesNotContain(AllLineTexts(context), t => t.Contains("Ustawa o usługach", StringComparison.Ordinal));
        Assert.DoesNotContain(AllLineTexts(context), t => t.Contains("Dziennik Ustaw", StringComparison.Ordinal));
        AssertBodyKept(context);
    }

    [Fact]
    public void HeaderOnOddPagesOnly_IsRemoved_WhenSplittingOddEven()
    {
        // 3 of 7 pages (< 50 %) overall, but 3 of the 4 odd pages (75 %).
        PipelineContext context = Run(Document(7, n => Body(n).Concat(n is 1 or 3 or 5
            ? [Line("Ustawa o usługach płatniczych", 72, 30)]
            : [])));

        Assert.DoesNotContain(AllLineTexts(context), t => t.Contains("Ustawa o usługach", StringComparison.Ordinal));
    }

    [Fact]
    public void HeaderOnOddPagesOnly_IsKept_WhenNotSplittingOddEven()
    {
        PipelineContext context = Run(Document(
            7,
            n => Body(n).Concat(n is 1 or 3 or 5 ? [Line("Ustawa o usługach płatniczych", 72, 30)] : []),
            o => o.Artifacts.SplitOddEven = false));

        Assert.Equal(3, AllLineTexts(context).Count(t => t == "Ustawa o usługach płatniczych"));
    }

    // (c)
    [Fact]
    public void RunningHeaderWithChangingChapterName_IsRemoved()
    {
        PipelineContext context = Run(Document(6, n => Body(n).Append(Line(
            n <= 3
                ? "Regulamin rachunków osobistych – Postanowienia ogólne"
                : "Regulamin rachunków osobistych – Postanowienia końcowe",
            72,
            30))));

        Assert.DoesNotContain(AllLineTexts(context), t => t.StartsWith("Regulamin rachunków", StringComparison.Ordinal));
        AssertBodyKept(context);
    }

    // (d)
    [Fact]
    public void RegistryFooter_IsRemoved()
    {
        PipelineContext context = Run(Document(5, n => Body(n).Append(Line(Registry, 72, 805))));

        Assert.DoesNotContain(AllLineTexts(context), t => t.StartsWith("Bank Przykładowy", StringComparison.Ordinal));
        ArtifactSummary summary = Assert.Single(context.Report.Build(5, TimeSpan.Zero).RemovedArtifacts);
        Assert.Equal(ArtifactKind.RunningFooter, summary.Kind);
        Assert.Equal(5, summary.Occurrences);
    }

    // (e)
    [Theory]
    [InlineData("{n}", 0)]
    [InlineData("- {n} -", 0)]
    [InlineData("– {n} –", 2)]
    [InlineData("{n} / 40", 0)]
    [InlineData("Strona {n} z 40", 0)]
    [InlineData("Str. {n}", 4)]
    [InlineData("s. {n}/40", 0)]
    public void PageNumberFooters_AreRemoved(string format, int offset)
    {
        PipelineContext context = Run(Document(4, n => Body(n).Append(
            Line(format.Replace("{n}", N(n + offset), StringComparison.Ordinal), 290, 805))));

        Assert.All(context.Pages, p => Assert.Equal(2, p.Lines.Count));
        ArtifactSummary summary = Assert.Single(context.Report.Build(4, TimeSpan.Zero).RemovedArtifacts);
        Assert.Equal(ArtifactKind.PageNumber, summary.Kind);
        Assert.Equal([1, 2, 3, 4], summary.Pages);
    }

    [Fact]
    public void PageNumberInconsistentWithSequence_IsKept()
    {
        PipelineContext context = Run(Document(4, n => Body(n).Append(Line(n == 3 ? "99" : N(n), 290, 805))));

        Assert.Equal(["99"], AllLineTexts(context).Where(t => !t.Contains(' ', StringComparison.Ordinal)));
    }

    [Fact]
    public void PageNumberInHeaderZone_IsRemoved()
    {
        PipelineContext context = Run(Document(4, n => Body(n).Append(Line(N(n), 290, 30))));

        Assert.All(context.Pages, p => Assert.Equal(2, p.Lines.Count));
    }

    // (f)
    [Fact]
    public void NonRepeatedLinesInMarginZones_AreKept()
    {
        PipelineContext context = Run(Document(5, n => n switch
        {
            2 => Body(n).Append(Line("ostatnia linia akapitu, która spadła do strefy stopki.", 72, 790)),
            3 => Body(n).Append(Line("Rozdział 2", 260, 795)),
            _ => Body(n),
        }));

        Assert.Contains("ostatnia linia akapitu, która spadła do strefy stopki.", AllLineTexts(context));
        Assert.Contains("Rozdział 2", AllLineTexts(context));
        Assert.Empty(context.Report.Build(5, TimeSpan.Zero).RemovedArtifacts);
    }

    // (g)
    [Fact]
    public void RepeatedBodyLineOutsideMarginZones_IsKept()
    {
        PipelineContext context = Run(Document(6, n => Body(n).Append(Line("(uchylony)", 72, 300))));

        Assert.Equal(6, AllLineTexts(context).Count(t => t == "(uchylony)"));
    }

    // (h)
    [Fact]
    public void ShortDocument_RemovesOnlyPageNumbers()
    {
        PipelineContext context = Run(Document(2, n => Body(n)
            .Append(Line("Regulamin promocji", 72, 30))
            .Append(Line(N(n), 290, 805))));

        Assert.Equal(2, AllLineTexts(context).Count(t => t == "Regulamin promocji"));
        Assert.DoesNotContain(AllLineTexts(context), t => t is "1" or "2");
    }

    // (i)
    [Fact]
    public void SameTextAtVaryingPositions_IsKept()
    {
        double[] tops = [2, 22, 42, 2, 22, 42];
        PipelineContext context = Run(Document(6, n => Body(n).Append(Line("Wersja robocza dokumentu", 72, tops[n - 1]))));

        Assert.Equal(6, AllLineTexts(context).Count(t => t == "Wersja robocza dokumentu"));
    }

    // (j)
    [Fact]
    public void Report_ListsRemovedPatternsSortedByFirstPageThenPattern()
    {
        PipelineContext context = Run(Document(6, n => Body(n)
            .Append(Line($"Dziennik Ustaw – {N(n)} – Poz. 1234", 200, 30))
            .Append(Line($"Strona {N(n)} z 6", 260, 805))));

        IReadOnlyList<ArtifactSummary> artifacts = context.Report.Build(6, TimeSpan.Zero).RemovedArtifacts;

        Assert.Equal(2, artifacts.Count);
        Assert.Equal("dziennik ustaw – # – poz. #", artifacts[0].Pattern);
        Assert.Equal(ArtifactKind.RunningHeader, artifacts[0].Kind);
        Assert.Equal(6, artifacts[0].Occurrences);
        Assert.Equal([1, 2, 3, 4, 5, 6], artifacts[0].Pages);
        Assert.Equal("strona # z #", artifacts[1].Pattern);
        Assert.Equal(ArtifactKind.PageNumber, artifacts[1].Kind);
    }

    // (k)
    [Fact]
    public void Disabled_RemovesNothing()
    {
        PipelineContext context = Run(Document(
            6,
            n => Body(n).Append(Line($"Dziennik Ustaw – {N(n)} – Poz. 1234", 200, 30)).Append(Line(N(n), 290, 805)),
            o => o.Artifacts.Enabled = false));

        Assert.All(context.Pages, p => Assert.Equal(4, p.Lines.Count));
    }

    [Fact]
    public void RemovePageNumbersDisabled_KeepsPageNumbers()
    {
        PipelineContext context = Run(Document(
            6,
            n => Body(n).Append(Line(N(n), 290, 805)),
            o => o.Artifacts.RemovePageNumbers = false));

        Assert.All(context.Pages, p => Assert.Equal(3, p.Lines.Count));
    }

    [Fact]
    public void MarginZonesAreComputedPerPageHeight()
    {
        // Landscape page (height 595): a footer at top 560 is in its footer zone although it would be body on A4.
        var pages = Enumerable.Range(1, 4).Select(n => n == 2
            ? Page(n, Body(n).Append(Line(Registry, 72, 560)), width: 842, height: 595)
            : Page(n, Body(n).Append(Line(Registry, 72, 805))));

        PipelineContext context = Run(Context(pages));

        Assert.DoesNotContain(AllLineTexts(context), t => t.StartsWith("Bank Przykładowy", StringComparison.Ordinal));
    }

    [Fact]
    public void IsDeterministic()
    {
        static PipelineContext Make() => Run(Document(6, n => Body(n)
            .Append(Line($"Dziennik Ustaw – {N(n)} – Poz. 1234", 200, 30))
            .Append(Line(Registry, 72, 805))
            .Append(Line(N(n), 290, 820))));

        Assert.Equal(AllLineTexts(Make()), AllLineTexts(Make()));
        Assert.Equal(
            Make().Report.Build(6, TimeSpan.Zero).RemovedArtifacts.Select(a => a.Pattern),
            Make().Report.Build(6, TimeSpan.Zero).RemovedArtifacts.Select(a => a.Pattern));
    }

    // Spec 008 (US1, FR-600 – FR-602): stand-alone "N/M" page numbers just above the margin zone.
    // A4 height 842: the footer zone starts at 0.92 H = 774.6; a footer line at top 762 lies at ≈ 0.905 H.
    private const double BandFooterTop = 762;

    // Below the header zone (bottom > 0.08 H = 67.4) but in the 0.15 H top band, above the body lines.
    private const double BandHeaderTop = 80;

    private static LayoutLine BandFooter(string text) => RightAligned(text, 523, BandFooterTop);

    private static List<ArtifactSummary> PageNumberArtifacts(PipelineContext context, int pageCount) =>
        context.Report.Build(pageCount, TimeSpan.Zero).RemovedArtifacts.Where(a => a.Kind == ArtifactKind.PageNumber).ToList();

    // (a)
    [Fact]
    public void SlashPageNumbersAboveFooterZone_OnLowestLine_AreRemoved()
    {
        PipelineContext context = Run(Document(3, n => n == 1 ? Body(n) : Body(n).Append(BandFooter($"{N(n)}/3"))));

        Assert.DoesNotContain(AllLineTexts(context), t => t is "2/3" or "3/3");
        AssertBodyKept(context);
        ArtifactSummary summary = Assert.Single(PageNumberArtifacts(context, 3));
        Assert.Equal([2, 3], summary.Pages);
        Assert.Equal(2, summary.Occurrences);
    }

    // (b)
    [Fact]
    public void SlashPageNumbersBelowHeaderZone_OnTopmostLine_AreRemoved()
    {
        PipelineContext context = Run(Document(3, n => n == 1
            ? Body(n)
            : Body(n).Append(RightAligned($"{N(n)}/3", 523, BandHeaderTop))));

        Assert.DoesNotContain(AllLineTexts(context), t => t is "2/3" or "3/3");
        AssertBodyKept(context);
        Assert.Equal([2, 3], Assert.Single(PageNumberArtifacts(context, 3)).Pages);
    }

    // (c)
    [Fact]
    public void SlashNumbersWhoseTotalIsNotThePageCount_AreKept()
    {
        PipelineContext context = Run(Document(3, n => n == 1 ? Body(n) : Body(n).Append(BandFooter($"{N(n)}/5"))));

        Assert.Contains("2/5", AllLineTexts(context));
        Assert.Contains("3/5", AllLineTexts(context));
        Assert.Empty(PageNumberArtifacts(context, 3));
    }

    // (d)
    [Fact]
    public void SlashNumberOnASinglePage_IsKept()
    {
        PipelineContext context = Run(Document(3, n => n == 2 ? Body(n).Append(BandFooter("2/3")) : Body(n)));

        Assert.Contains("2/3", AllLineTexts(context));
        Assert.Empty(PageNumberArtifacts(context, 3));
    }

    // (e)
    [Fact]
    public void SlashNumbersNotFollowingThePages_AreKept()
    {
        PipelineContext context = Run(Document(3, n => n == 1 ? Body(n) : Body(n).Append(BandFooter("2/3"))));

        Assert.Equal(2, AllLineTexts(context).Count(t => t == "2/3"));
        Assert.Empty(PageNumberArtifacts(context, 3));
    }

    // (f)
    [Fact]
    public void SlashNumberThatIsNotTheLowestLine_IsKept()
    {
        PipelineContext context = Run(Document(3, n => n == 1
            ? Body(n)
            : Body(n).Append(BandFooter($"{N(n)}/3")).Append(Line($"Uwaga końcowa strony {N(n)}.", 72, 780))));

        Assert.Contains("2/3", AllLineTexts(context));
        Assert.Contains("3/3", AllLineTexts(context));
        Assert.Empty(PageNumberArtifacts(context, 3));
    }

    // (f)
    [Fact]
    public void SlashNumberSharingItsHeightWithAnotherLine_IsKept()
    {
        PipelineContext context = Run(Document(3, n => n == 1
            ? Body(n)
            : Body(n).Append(BandFooter($"{N(n)}/3")).Append(Line($"Przypis strony {N(n)}.", 72, BandFooterTop))));

        Assert.Contains("2/3", AllLineTexts(context));
        Assert.Contains("3/3", AllLineTexts(context));
        Assert.Empty(PageNumberArtifacts(context, 3));
    }

    // (g) protective: passes before and after the fix.
    [Theory]
    [InlineData("1/2 kwoty")]
    [InlineData("12/2024")]
    [InlineData("1/ tekst")]
    public void SlashTextsThatAreNotPageNumbers_AreKept(string text)
    {
        PipelineContext context = Run(Document(3, n => Body(n)
            .Append(Line(text, 72, 300))
            .Append(Line(text, 72, BandFooterTop))));

        Assert.Equal(6, AllLineTexts(context).Count(t => t == text));
        Assert.Empty(PageNumberArtifacts(context, 3));
    }

    // (h) protective: passes before and after the fix.
    [Fact]
    public void SlashNumberInsideALongerFooterLine_IsUnchanged()
    {
        PipelineContext context = Run(Document(3, n => n == 1
            ? Body(n)
            : Body(n).Append(BandFooter($"Regulamin rachunków {N(n)}/3"))));

        Assert.Contains("Regulamin rachunków 2/3", AllLineTexts(context));
        Assert.Contains("Regulamin rachunków 3/3", AllLineTexts(context));
        Assert.Empty(PageNumberArtifacts(context, 3));
    }

    // (i)
    [Fact]
    public void SlashPageNumbersWithGapsInTheSequence_AreRemoved()
    {
        PipelineContext context = Run(Document(5, n => n is 2 or 3 or 5 ? Body(n).Append(BandFooter($"{N(n)}/5")) : Body(n)));

        Assert.DoesNotContain(AllLineTexts(context), t => t.EndsWith("/5", StringComparison.Ordinal));
        AssertBodyKept(context);
        Assert.Equal([2, 3, 5], Assert.Single(PageNumberArtifacts(context, 5)).Pages);
    }
}
