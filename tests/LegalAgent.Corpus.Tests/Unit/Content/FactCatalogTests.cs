using LegalAgent.Corpus.Content;

namespace LegalAgent.Corpus.Tests.Unit.Content;

public class FactCatalogTests
{
    private const string Id = "oplata.karta.wydanie-duplikatu";

    [Fact]
    public void ValueAt_BeforeFrom_ReturnsBaseValue()
    {
        var value = MiniContent.Load().Facts.ValueAt(Id, new DateOnly(2026, 3, 31));

        Assert.Equal(25.00m, value.Number);
    }

    [Fact]
    public void ValueAt_OnAndAfterFrom_ReturnsLaterValue()
    {
        var facts = MiniContent.Load().Facts;

        Assert.Equal(30.00m, facts.ValueAt(Id, new DateOnly(2026, 4, 1)).Number);
        Assert.Equal(30.00m, facts.ValueAt(Id, new DateOnly(2030, 1, 1)).Number);
    }

    [Fact]
    public void ValueAt_OverrideWins()
    {
        var facts = MiniContent.Load().Facts;
        var overrides = new Dictionary<string, FactValue> { [Id] = new FactValue(Number: 99m) };

        Assert.Equal(99m, facts.ValueAt(Id, new DateOnly(2026, 3, 1), overrides).Number);
        Assert.Equal(99m, facts.ValueAt(Id, new DateOnly(2027, 3, 1), overrides).Number);
    }

    [Fact]
    public void ValueAt_OverrideOfOtherFact_IsIgnored()
    {
        var facts = MiniContent.Load().Facts;
        var overrides = new Dictionary<string, FactValue> { ["termin.reklamacja"] = new FactValue(Number: 3m) };

        Assert.Equal(25.00m, facts.ValueAt(Id, new DateOnly(2026, 3, 1), overrides).Number);
    }

    [Fact]
    public void ValueAt_TextAndDateFacts_ReturnTheirKindOfValue()
    {
        var facts = MiniContent.Load().Facts;

        Assert.Equal("800 000 001", facts.ValueAt("kontakt.infolinia", new DateOnly(2026, 1, 1)).Text);
        Assert.Equal(new DateOnly(2026, 1, 1), facts.ValueAt("data.wejscie-w-zycie", new DateOnly(2026, 1, 1)).Date);
    }

    [Fact]
    public void ValueAt_UnknownFact_ThrowsNamingId()
    {
        var facts = MiniContent.Load().Facts;

        var ex = Assert.Throws<ContentException>(() => facts.ValueAt("brak.takiego", new DateOnly(2026, 1, 1)));

        Assert.Contains("brak.takiego", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Get_And_Contains_WorkOnIds()
    {
        var facts = MiniContent.Load().Facts;

        Assert.True(facts.Contains(Id));
        Assert.False(facts.Contains("brak"));
        Assert.Equal(FactKind.Kwota, facts.Get(Id).Kind);
        Assert.Equal(2, facts.Get(Id).Values.Count);
        Assert.Equal(new DateOnly(2026, 4, 1), facts.Get(Id).Values[1].From);
        Assert.Equal(["data.wejscie-w-zycie", "kontakt.infolinia", Id, "oplata.prowizja.przelew", "termin.reklamacja"],
            facts.All.Select(f => f.Id));
        Assert.Throws<ContentException>(() => facts.Get("brak"));
    }

    [Fact]
    public void Load_AlternativeEqualToValue_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "fakty.yaml", "alternatywy: [0.00, 45.00]", "alternatywy: [0.00, 30.00]")));

        Assert.Equal("fakty.yaml", ex.File);
        Assert.Equal("fakty[0].alternatywy[1]", ex.YamlPath);
    }

    [Fact]
    public void Load_DatesNotIncreasing_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "fakty.yaml", "        wartosc: 30\n", "        wartosc: 30\n      - od: 2026-06-01\n        wartosc: 45\n")));

        Assert.Equal("fakty.yaml", ex.File);
        Assert.Equal("fakty[2].wartosci[2].od", ex.YamlPath);
    }

    [Fact]
    public void Load_FirstValueWithFrom_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "fakty.yaml", "      - wartosc: 1.5\n", "      - od: 2026-01-01\n        wartosc: 1.5\n")));

        Assert.Equal("fakty.yaml", ex.File);
        Assert.Equal("fakty[1].wartosci[0].od", ex.YamlPath);
    }
}
