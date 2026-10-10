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

    [Fact]
    public void EmptyUnitList_MatchesNothing()
    {
        Assert.False(UnitMatcher.Matches("§ 1", []));
    }
}
