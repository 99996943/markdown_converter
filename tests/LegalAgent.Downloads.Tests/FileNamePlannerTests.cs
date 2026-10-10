namespace LegalAgent.Downloads.Tests;

public sealed class FileNamePlannerTests
{
    private static string Name(string url) => FileNamePlanner.Plan([new Uri(url)])[0].FileName;

    [Theory]
    [InlineData("https://www.mbank.pl/pdf/regulaminy/reg-konta.pdf", "reg-konta.pdf")]
    [InlineData("https://www.mbank.pl/a.pdf?v=3#page=2", "a.pdf")]
    [InlineData("https://www.mbank.pl/pdf/Regulamin%20konta.pdf", "Regulamin konta.pdf")]
    [InlineData("https://www.mbank.pl/pdf/op%C5%82aty.pdf", "opłaty.pdf")]
    [InlineData("https://www.mbank.pl/pdf/REG.PDF", "REG.pdf")]
    [InlineData("https://www.mbank.pl/pdf/regulamin", "regulamin.pdf")]
    [InlineData("https://www.mbank.pl/pdf/regulamin/", "regulamin.pdf")]
    [InlineData("https://www.mbank.pl/pdf/CON.pdf", "_CON.pdf")]
    [InlineData("https://www.mbank.pl/pdf/lpt1", "_lpt1.pdf")]
    public void Plan_DerivesNameFromLastSegment(string url, string expected) => Assert.Equal(expected, Name(url));

    [Theory]
    [InlineData("https://www.mbank.pl/pdf/a%3Cb%3Ec%3Ad%22e%7Cf%3Fg%2Ah.pdf", "a-b-c-d-e-f-g-h.pdf")]
    [InlineData("https://www.mbank.pl/pdf/a%5Cb.pdf", "a-b.pdf")]
    [InlineData("https://www.mbank.pl/pdf/a%01%02b.pdf", "a-b.pdf")]
    [InlineData("https://www.mbank.pl/pdf/%20.%20reg.%20", "reg.pdf")]
    [InlineData("https://www.mbank.pl/pdf/a%3C%3E%3Ab.pdf", "a-b.pdf")]
    public void Plan_ReplacesForbiddenCharacters(string url, string expected) => Assert.Equal(expected, Name(url));

    [Fact]
    public void Plan_NormalizesToNfc()
    {
        // "ł" is precomposed; "ó" given decomposed (o + U+0301).
        string name = Name("https://www.mbank.pl/pdf/o%CC%81.pdf");

        Assert.Equal("ó.pdf", name);
    }

    [Fact]
    public void Plan_TruncatesLongStemTo120Characters()
    {
        string stem = new('a', 200);

        string name = Name($"https://www.mbank.pl/pdf/{stem}.pdf");

        Assert.Equal(new string('a', 120) + ".pdf", name);
    }

    [Theory]
    [InlineData("https://www.mbank.pl/")]
    [InlineData("https://www.mbank.pl")]
    [InlineData("https://www.mbank.pl/pdf/...")]
    public void Plan_NoUsableSegment_UsesIndexedDefault(string url)
    {
        IReadOnlyList<PlannedDownload> plan = FileNamePlanner.Plan([new Uri("https://www.mbank.pl/a.pdf"), new Uri(url)]);

        Assert.Equal("regulamin-2.pdf", plan[1].FileName);
    }

    [Fact]
    public void Plan_Collisions_GetSuffixWithAddressIndex()
    {
        IReadOnlyList<PlannedDownload> plan = FileNamePlanner.Plan(
        [
            new Uri("https://www.mbank.pl/a/regulamin.pdf"),
            new Uri("https://www.mbank.pl/b/inny.pdf"),
            new Uri("https://www.mbank.pl/c/regulamin.pdf"),
            new Uri("https://www.mbank.pl/d/REGULAMIN.pdf"),
        ]);

        Assert.Equal(["regulamin.pdf", "inny.pdf", "regulamin-3.pdf", "REGULAMIN-4.pdf"], plan.Select(p => p.FileName));
    }

    [Fact]
    public void Plan_KeepsIndexAndAddress_AndIsDeterministic()
    {
        Uri[] addresses = [new Uri("https://www.mbank.pl/x.pdf"), new Uri("https://www.mbank.pl/y.pdf")];

        IReadOnlyList<PlannedDownload> first = FileNamePlanner.Plan(addresses);
        IReadOnlyList<PlannedDownload> second = FileNamePlanner.Plan(addresses);

        Assert.Equal([1, 2], first.Select(p => p.Index));
        Assert.Equal(addresses, first.Select(p => p.Address));
        Assert.Equal(first, second);
    }
}
