namespace LegalAgent.Faq.Tests;

public sealed class UnitMatcherTests
{
    [Theory]
    [InlineData("§ 12 ust. 3", "§ 12.")]
    [InlineData("§ 12", "§ 12.")]
    [InlineData("§ 1,", "§ 1")]
    [InlineData("§ 1, ust. 2", "§ 1")]
    [InlineData("Rozdział 2", "Rozdział 2")]
    [InlineData("rozdział 2", "Rozdział 2")]
    [InlineData("ART. 5", "Art. 5")]
    [InlineData("§ 12", "§ 12")]
    [InlineData("  §   12  ", "§ 12")]
    public void Matches_CitedUnitInDocument(string cited, string unit)
    {
        Assert.True(UnitMatcher.Matches(cited, [unit]));
    }

    [Theory]
    [InlineData("Art. 5a", "Art. 5")]
    [InlineData("§ 120", "§ 12")]
    [InlineData("§ 12", "§ 120")]
    [InlineData("Rozdział 3", "Rozdział 2")]
    public void DoesNotMatch_DifferentUnit(string cited, string unit)
    {
        Assert.False(UnitMatcher.Matches(cited, [unit]));
    }

    [Fact]
    public void Matches_DesignationAmongHeadingTexts()
    {
        string[] units = ["Rozdział 2", "Rozdział 2. Otwarcie rachunku"];

        Assert.True(UnitMatcher.Matches("Rozdział 2", units));
        Assert.True(UnitMatcher.Matches("Rozdział 2. Otwarcie rachunku", units));
    }

    [Theory]
    [InlineData("6.")]
    [InlineData("6")]
    [InlineData("6 ust. 2")]
    [InlineData("6. Jakie informacje musisz podać?")]
    public void Matches_NumberOfNumberedHeading(string cited)
    {
        Assert.True(UnitMatcher.Matches(cited, ["6. Jakie informacje musisz podać?"]));
    }

    [Theory]
    [InlineData("§ 6", "6. Jakie informacje musisz podać?")]
    [InlineData("6", "16. Przelewy")]
    [InlineData("2", "2.1. Przelewy krajowe")]
    [InlineData("2.1", "2. Przelewy")]
    [InlineData("6", "6 miesięcy okresu wypowiedzenia")]
    public void DoesNotMatch_OtherNumber(string cited, string unit)
    {
        Assert.False(UnitMatcher.Matches(cited, [unit]));
    }

    [Fact]
    public void EmptyUnitList_MatchesNothing()
    {
        Assert.False(UnitMatcher.Matches("§ 1", []));
    }

    [Theory]
    [InlineData("§ 12", "§ 12")]
    [InlineData("§ 12 ust. 3", "§ 12")]
    [InlineData("§ 12.", "§ 12")]
    [InlineData("§\u00a012", "§ 12")]
    [InlineData("rozdział 2", "Rozdział 2")]
    [InlineData("6", "6. Jakie rachunki prowadzimy?")]
    [InlineData("6 ust. 2", "6. Jakie rachunki prowadzimy?")]
    public void Find_ReturnsUnitAsListed(string cited, string expected)
    {
        string[] units = ["Rozdział 2", "Rozdział 2. Otwarcie rachunku", "§ 12", "§ 12.", "6. Jakie rachunki prowadzimy?"];

        Assert.Equal(expected, UnitMatcher.Find(cited, units));
    }

    [Fact]
    public void Find_FirstMatchingUnitInOrder()
    {
        Assert.Equal("§ 1.", UnitMatcher.Find("§ 1", ["§ 1.", "§ 1"]));
        Assert.Equal("§ 1", UnitMatcher.Find("§ 1", ["§ 1", "§ 1."]));
    }

    [Theory]
    [InlineData("§ 3")]
    [InlineData("Art. 5a")]
    [InlineData("")]
    [InlineData("   ")]
    public void Find_NoMatch_ReturnsNull(string cited)
    {
        Assert.Null(UnitMatcher.Find(cited, ["§ 1", "§ 2.", "Art. 5"]));
    }

    [Theory]
    [InlineData("§ 12 ust. 3", "§ 12.")]
    [InlineData("§ 120", "§ 12")]
    [InlineData("6", "16. Przelewy")]
    [InlineData("6.", "6. Jakie informacje musisz podać?")]
    [InlineData("", "§ 1")]
    [InlineData("ART. 5", "Art. 5")]
    public void Find_AgreesWithMatches(string cited, string unit)
    {
        Assert.Equal(UnitMatcher.Matches(cited, [unit]), UnitMatcher.Find(cited, [unit]) is not null);
    }
}
