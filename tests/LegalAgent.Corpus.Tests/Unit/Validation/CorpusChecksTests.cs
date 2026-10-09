using LegalAgent.Corpus.Tests.Unit.Content;
using LegalAgent.Corpus.Validation;

namespace LegalAgent.Corpus.Tests.Unit.Validation;

public sealed class CorpusChecksTests
{
    private static readonly string[] Forbidden = ["Żółty Bank"];

    [Fact]
    public void ForbiddenNames_DetectsVariantsAndCarriesBlock()
    {
        var texts = new[]
        {
            new RenderedText("D1", "b1", "Umowa w Zolty Bank jest ważna."),
            new RenderedText("D1", "b2", "Klient żółty   BANK."),
            new RenderedText("D2", "b3", "Bank Niebieski."),
        };

        var result = CorpusChecks.ForbiddenNames(texts, Forbidden);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, v => v.BlockId == "b1" && v.DocumentId == "D1");
        Assert.Contains(result, v => v.BlockId == "b2" && v.DocumentId == "D1");
        Assert.All(result, v => Assert.Contains("Żółty Bank", v.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void ForbiddenNames_CleanText_NoViolations()
    {
        Assert.Empty(CorpusChecks.ForbiddenNames([new RenderedText("D1", null, "Zwykły tekst.")], Forbidden));
    }

    [Fact]
    public void Uniqueness_SameTextInTwoDocuments_Violation()
    {
        var result = CorpusChecks.Uniqueness(
        [
            new RenderedBlock("A", "blk", false, "Tekst   Powtórzony."),
            new RenderedBlock("B", "blk", false, "tekst powtórzony."),
        ]);

        var v = Assert.Single(result);
        Assert.Contains("A", v.Message, StringComparison.Ordinal);
        Assert.Contains("B", v.Message, StringComparison.Ordinal);
        Assert.Contains("blk", v.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Uniqueness_SharedBlocksAndSameDocument_NoViolation()
    {
        var result = CorpusChecks.Uniqueness(
        [
            new RenderedBlock("A", "s", true, "Wspólny."),
            new RenderedBlock("B", "s", true, "Wspólny."),
            new RenderedBlock("A", "x", false, "Powtórka."),
            new RenderedBlock("A", "y", false, "Powtórka."),
        ]);

        Assert.Empty(result);
    }

    [Fact]
    public void SharedShare_AboveMax_Violation()
    {
        var result = CorpusChecks.SharedShare([new DocumentWords("A", 100, 25)], 0.2);

        var v = Assert.Single(result);
        Assert.Contains("0.250", v.Message, StringComparison.Ordinal);
        Assert.Equal("A", v.DocumentId);
    }

    [Fact]
    public void SharedShare_AtOrBelowMax_None()
    {
        Assert.Empty(CorpusChecks.SharedShare([new DocumentWords("A", 100, 20)], 0.2));
    }

    [Fact]
    public void References_Unresolved_NamesAll()
    {
        var v = Assert.Single(CorpusChecks.References([new UnresolvedReference("DOC1", "blk9", "dokument:taryfa")]));

        Assert.Contains("DOC1", v.Message, StringComparison.Ordinal);
        Assert.Contains("blk9", v.Message, StringComparison.Ordinal);
        Assert.Contains("dokument:taryfa", v.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateStructure_MiniLibrary_Satisfied()
    {
        Assert.Empty(CorpusChecks.TemplateStructure(MiniContent.Load()));
    }

    [Fact]
    public void TemplateStructure_RichSatisfiedSet_None()
    {
        var lib = MiniContent.LoadModified(dir => MiniContent.Replace(
            dir, "typy.yaml", "wymagane-elementy: [kroki]",
            "wymagane-elementy: [kroki, kroki-poziomy:3, schemat, lista-kontrolna, zalacznik, metryczka]"));

        Assert.Empty(CorpusChecks.TemplateStructure(lib));
    }

    [Fact]
    public void TemplateStructure_MissingElement_Violation()
    {
        var lib = MiniContent.LoadModified(dir => MiniContent.Replace(
            dir, "typy.yaml", "wymagane-elementy: [kroki]", "wymagane-elementy: [kroki, ramka]"));

        var v = Assert.Single(CorpusChecks.TemplateStructure(lib));
        Assert.Equal("procedura-reklamacji", v.Template);
        Assert.Contains("ramka", v.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateStructure_UnknownName_Violation()
    {
        var lib = MiniContent.LoadModified(dir => MiniContent.Replace(
            dir, "typy.yaml", "wymagane-elementy: [kroki]", "wymagane-elementy: [kroki, nieznany]"));

        var v = Assert.Single(CorpusChecks.TemplateStructure(lib));
        Assert.Contains("nieznany", v.Message, StringComparison.Ordinal);
        Assert.Contains("procedury", v.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateStructure_CountNotMet_Violation()
    {
        var lib = MiniContent.LoadModified(dir => MiniContent.Replace(
            dir, "typy.yaml", "wymagane-elementy: [paragrafy]", "wymagane-elementy: [paragrafy:99]"));

        var v = Assert.Single(CorpusChecks.TemplateStructure(lib));
        Assert.Equal("regulamin-karty", v.Template);
        Assert.Contains("paragrafy", v.Message, StringComparison.Ordinal);
    }
}
