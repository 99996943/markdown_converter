using System.Text;
using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Tests.Unit.Content;
using LegalAgent.Corpus.Typesetting;

namespace LegalAgent.Corpus.Tests.Unit.Typesetting;

public sealed class PageFitterTests
{
    private const ulong RunSeed = 7;

    private static readonly Lazy<ContentLibrary> Library = new(() => MiniContent.LoadModified(dir =>
    {
        File.WriteAllText(Path.Combine(dir, "szablony", "regulamin-objetosc.yaml"), """
            id: regulamin-objetosc
            typ: regulaminy
            temat: objetosc
            tytul: ["Regulamin objętości"]
            uklady: [jedna-kolumna]
            czolo: { okladka: false, metryczka: false, pola: [oznaczenie, wersja, od] }
            sekcje:
              - rodzaj: rozdzial
                tytul: "Postanowienia"
                wymagane: [objetosc-wstep]
                opcjonalne: { kategorie: [objetosc], min: 0, max: 40 }
            """);
        var blocks = new StringBuilder("bloki:\n  - id: objetosc-wstep\n    typy: [regulaminy]\n    tematy: [objetosc]\n    kategoria: wstep\n    jednostka: \"§ {n}.\"\n    elementy:\n      - akapit: \"Wstęp regulaminu.\"\n");
        for (int i = 1; i <= 40; i++)
        {
            blocks.Append(System.Globalization.CultureInfo.InvariantCulture, $"  - id: objetosc-{i:D2}\n    typy: [regulaminy]\n    tematy: [objetosc]\n    kategoria: objetosc\n    jednostka: \"§ {{n}}.\"\n    elementy:\n");
            for (int k = 1; k <= 3; k++)
            {
                blocks.Append(System.Globalization.CultureInfo.InvariantCulture, $"      - ustep: \"Postanowienie {i}.{k}. Bank prowadzi rachunek zgodnie z przepisami prawa oraz postanowieniami umowy, a Klient korzysta z rachunku w sposób zgodny z jego przeznaczeniem, z zachowaniem zasad bezpieczeństwa i z poszanowaniem praw innych osób.\"\n");
            }
        }

        File.WriteAllText(Path.Combine(dir, "bloki", "objetosc.yaml"), blocks.ToString());
    }));

    private static DocumentPlan Plan(int target) => new()
    {
        Id = "REG-01",
        Type = "regulaminy",
        Prefix = "REG",
        Designation = "BP/REG/01",
        Template = "regulamin-objetosc",
        Layout = "jedna-kolumna",
        ValidFrom = new DateOnly(2026, 6, 1),
        BlockPool = Enumerable.Range(1, 40).Select(i => $"objetosc-{i:D2}").ToList(),
        TargetPages = target,
        Seed = 99,
    };

    [Theory]
    [InlineData(2, 2, 4)]
    [InlineData(4, 3, 5)]
    [InlineData(6, 5, 7)]
    public void Fit_HitsThePageRange(int target, int min, int max)
    {
        FitResult fit = PageFitter.Fit(Plan(target), Library.Value, RunSeed, new PageRange(min, max));

        Assert.InRange(fit.Typeset.PageCount, min, max);
        Assert.InRange(fit.Iterations, 1, PageFitter.MaxIterations);
        Assert.Equal(fit.Composition.OptionalBlocks, fit.Composition.Blocks.Count - 1);
    }

    [Fact]
    public void Fit_TooFewBlocks_ReportsAnUnreachableRangeWithDocumentAndTemplate()
    {
        var ex = Assert.Throws<CorpusGenerationException>(() => PageFitter.Fit(Plan(40), Library.Value, RunSeed, new PageRange(40, 45)));

        Assert.Contains("nieosiągalny zakres stron", ex.Message, StringComparison.Ordinal);
        Assert.Equal("REG-01", ex.DocumentId);
        Assert.Equal("regulamin-objetosc", ex.Template);
    }

    [Fact]
    public void Fit_TooMuchRequiredContent_ReportsAnUnreachableRange()
    {
        var ex = Assert.Throws<CorpusGenerationException>(() => PageFitter.Fit(Plan(1) with { BlockPool = [] }, Library.Value, RunSeed, new PageRange(2, 3)));
        Assert.Contains("nieosiągalny zakres stron", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fit_IsDeterministic()
    {
        FitResult a = PageFitter.Fit(Plan(4), Library.Value, RunSeed, new PageRange(3, 5));
        FitResult b = PageFitter.Fit(Plan(4), Library.Value, RunSeed, new PageRange(3, 5));

        Assert.Equal(a.Typeset.Pdf, b.Typeset.Pdf);
        Assert.Equal(a.Iterations, b.Iterations);
    }
}
