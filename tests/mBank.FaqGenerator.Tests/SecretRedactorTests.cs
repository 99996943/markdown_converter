namespace MBank.FaqGenerator.Tests;

public sealed class SecretRedactorTests
{
    [Fact]
    public void EveryOccurrence_IsReplaced()
    {
        Assert.Equal("klucz *** i znowu *** koniec", SecretRedactor.Redact("klucz abc123 i znowu abc123 koniec", "abc123"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyKey_LeavesTextUnchanged(string? key)
    {
        Assert.Equal("tekst bez zmian", SecretRedactor.Redact("tekst bez zmian", key));
    }

    [Fact]
    public void RegexCharacters_AreReplacedLiterally()
    {
        Assert.Equal("a *** b", SecretRedactor.Redact("a .*+?[x](y)$ b", ".*+?[x](y)$"));
        Assert.Equal("abc", SecretRedactor.Redact("abc", ".*"));
    }

    [Fact]
    public void CaseMatters()
    {
        Assert.Equal("ABC123 ***", SecretRedactor.Redact("ABC123 abc123", "abc123"));
    }
}
