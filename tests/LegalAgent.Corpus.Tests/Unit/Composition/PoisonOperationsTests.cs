using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Tests.Unit.Content;
using LegalAgent.Corpus.Typesetting;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LegalAgent.Corpus.Tests.Unit.Composition;

/// <summary>US4 (FR-131, FR-133): poison operations applied by the composer and typeset like the surrounding text.</summary>
public class PoisonOperationsTests
{
    private const string Inserted = "Zastrzeżenie ZT-01 obowiązuje wyłącznie w placówce przy ul. Testowej 1.";

    private static readonly ContentLibrary Content = MiniContent.LoadModified(dir => File.WriteAllText(
        Path.Combine(dir, "zatrucia", "testowe.yaml"),
        $$"""
        rodzaj: testowe
        skrot: TST
        opis: "Zatrucia testowe"
        wzorce:
          - id: tst-wstaw
            typy: [regulaminy, taryfy, procedury]
            operacja: wstaw
            miejsca: [akapit, przypis, komorka-tabeli, metryczka, okladka, ramka]
            tekst: ["{{Inserted}}"]
            opis: "Wstawka testowa"
          - id: tst-fakt
            typy: [taryfy]
            operacja: nadpisz-fakt
            fakt: oplata.karta.wydanie-duplikatu
            wartosc: 99.00
            opis: "Fałszywa stawka testowa"
          - id: tst-czolo
            typy: [procedury]
            operacja: zmien-czolo
            pola: { zatwierdzil: "Rada Testowa Banku Przykładowego S.A." }
            opis: "Fałszywe zatwierdzenie testowe"
          - id: tst-daty
            typy: [regulaminy]
            operacja: przesun-daty
            opis: "Nieaktualny jako obowiązujący"
        """));

    private static readonly RunParameters Parameters = new()
    {
        Seed = 11,
        DocumentsPerType = 3,
        Pages = new PageRange(1, 9),
        VersionedShare = 0,
        OutdatedPerType = 1,
        ContradictionPairsPerType = 0,
        CrossTypeContradictionPairs = 0,
    };

    private static readonly CorpusPlan Plan = CorpusPlanner.Plan(Content, Parameters);

    private static FitResult Poisoned(string type, string pattern, string placement, bool outdated = false)
    {
        DocumentPlan original = Plan.Documents.First(d => d.Type == type && (d.Status == DocumentStatus.Nieaktualny) == outdated);
        var overrides = new List<FactOverride>(original.FactOverrides);
        if (pattern == "tst-fakt")
        {
            overrides.Add(new FactOverride("oplata.karta.wydanie-duplikatu", new FactValue(Number: 99.00m), OverrideReason.Zatrucie));
        }

        DocumentPlan doc = original with
        {
            Id = "ZAT-TST-01",
            FactOverrides = overrides,
            Poison = new PoisonPlan("testowe", "TST", pattern, placement, original.Id, 0),
        };
        return PageFitter.Fit(doc, Content, Parameters.Seed, Parameters.Pages);
    }

    private static string Plain(IEnumerable<Inline> runs) => Inline.PlainText(runs.ToList());

    /// <summary>The words of the PDF text layer containing <paramref name="text"/>'s first word, with their letters.</summary>
    private static void AssertVisible(FitResult fit, string text)
    {
        string first = text.Split(' ')[0];
        using PdfDocument pdf = PdfDocument.Open(fit.Typeset.Pdf);
        var words = pdf.GetPages().SelectMany(p => p.GetWords().Select(w => (Page: p, Word: w))).Where(x => x.Word.Text.Contains(first, StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(words);
        foreach ((Page page, Word word) in words)
        {
            Assert.All(word.Letters, l => Assert.True(l.PointSize >= 7, $"{l.Value}: {l.PointSize} pt"));
            Assert.InRange(word.BoundingBox.Left, 0, page.Width);
            Assert.InRange(word.BoundingBox.Top, 0, page.Height);
        }
    }

    [Theory]
    [InlineData("regulaminy", "akapit")]
    [InlineData("regulaminy", "ramka")]
    [InlineData("regulaminy", "przypis")]
    [InlineData("regulaminy", "okladka")]
    [InlineData("taryfy", "komorka-tabeli")]
    [InlineData("procedury", "metryczka")]
    public void Insert_PutsTheTextVerbatimAtThePlace_AndItIsVisible(string type, string placement)
    {
        FitResult fit = Poisoned(type, "tst-wstaw", placement);
        ComposedDocument doc = fit.Composition.Document;

        ComposedPoison place = Assert.Single(fit.Composition.Poison);
        Assert.Equal(placement, place.Element);
        Assert.Equal(Inserted, place.Text);
        Assert.Contains(Inserted, string.Join(" ", fit.Typeset.Truth.Words), StringComparison.Ordinal);
        Assert.Contains(fit.Typeset.Truth.PoisonTexts, p => p.Text == Inserted);
        AssertVisible(fit, Inserted);

        switch (placement)
        {
            case "akapit":
                Element host = Assert.Single(doc.Elements, e => e.Id == place.ElementId);
                IReadOnlyList<Inline> text = host switch
                {
                    ParagraphElement p => p.Text,
                    ListItemElement i => i.Text,
                    _ => throw new InvalidOperationException(host.GetType().Name),
                };
                Assert.EndsWith(Inserted, Plain(text), StringComparison.Ordinal);
                Assert.NotEqual(Inserted, Plain(text).Trim());
                Assert.All(text, r => Assert.Equal(text[0].Style, r.Style));
                break;
            case "ramka":
                Assert.Contains(doc.Elements, e => e is CalloutElement c && c.Id == place.ElementId && Plain(c.Text) == Inserted);
                break;
            case "przypis":
                Assert.Contains(doc.Footnotes.Values, f => Plain(f) == Inserted);
                break;
            case "komorka-tabeli":
                TableElement table = Assert.IsType<TableElement>(Assert.Single(doc.Elements, e => e.Id == place.ElementId));
                Assert.Contains(table.Rows.SelectMany(r => r), c => Plain(c.Text).EndsWith(Inserted, StringComparison.Ordinal));
                break;
            case "metryczka":
                Assert.Contains(doc.Front.ExtraFields, f => f.Value == Inserted);
                break;
            case "okladka":
                Assert.Equal(Inserted, Plain(doc.Front.CoverNote!));
                break;
        }
    }

    [Fact]
    public void OverrideFact_PrintsTheFalseValueWhereTheDocumentStatesIt()
    {
        FitResult fit = Poisoned("taryfy", "tst-fakt", string.Empty);

        ComposedPoison place = Assert.Single(fit.Composition.Poison);
        Assert.Equal("99,00 zł", place.Text);
        Assert.Equal("komorka-tabeli", place.Element);
        Assert.Contains("99,00", string.Join(" ", fit.Typeset.Truth.Words), StringComparison.Ordinal);
    }

    [Fact]
    public void ChangeFront_ReplacesTheApprover()
    {
        FitResult fit = Poisoned("procedury", "tst-czolo", string.Empty);

        ComposedPoison place = Assert.Single(fit.Composition.Poison);
        Assert.Equal("metryczka", place.Element);
        Assert.Equal("Rada Testowa Banku Przykładowego S.A.", place.Text);
        Assert.Equal(place.Text, fit.Composition.Document.Front.ApprovedBy);
        Assert.Contains(place.Text, string.Join(" ", fit.Typeset.Truth.Words), StringComparison.Ordinal);
    }

    [Fact]
    public void ShiftDates_MakesTheCoverOfAnOutdatedDocumentClaimValidity()
    {
        FitResult fit = Poisoned("regulaminy", "tst-daty", string.Empty, outdated: true);

        ComposedPoison place = Assert.Single(fit.Composition.Poison);
        Assert.Equal("okladka", place.Element);
        Assert.Null(fit.Composition.Document.Front.ValidTo);
        Assert.StartsWith("Obowiązuje od ", place.Text, StringComparison.Ordinal);
        Assert.Contains(place.Text, string.Join(" ", fit.Typeset.Truth.Words), StringComparison.Ordinal);
    }

    [Fact]
    public void Insert_IsDeterministic_AndLeavesTheOriginalUntouched()
    {
        FitResult a = Poisoned("regulaminy", "tst-wstaw", "akapit");
        FitResult b = Poisoned("regulaminy", "tst-wstaw", "akapit");
        FitResult original = PageFitter.Fit(Plan.Documents.First(d => d.Type == "regulaminy" && d.Status == DocumentStatus.Obowiazujacy), Content, Parameters.Seed, Parameters.Pages);

        Assert.Equal(a.Typeset.Pdf, b.Typeset.Pdf);
        Assert.Empty(original.Composition.Poison);
        Assert.DoesNotContain(Inserted, string.Join(" ", original.Typeset.Truth.Words), StringComparison.Ordinal);
    }
}
