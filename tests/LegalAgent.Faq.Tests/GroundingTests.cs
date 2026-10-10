using LegalAgent.Faq.Model;

namespace LegalAgent.Faq.Tests;

/// <summary>Quote and numbers of a candidate against the text of its unit (T067d).</summary>
public sealed class GroundingTests
{
    private const string Markdown =
        "# Regulamin\n\n<!-- page: 1 -->\n## 6. Jakie informacje musisz podać?\n\n"
        + "- 1\\) Musisz podać:\n  - a\\) numer rachunku odbiorcy (NRB lub IBAN),\n  - b\\) **kwotę** i walutę.\n\n"
        + "### Dodatkowe wyjaśnienia\n\nPrzelew SWIFT złożony po godzinie 13.00 realizujemy następnego dnia.\n\n"
        + "## 7. Jak autoryzujesz transakcję?\n\n<!-- page: 12 -->\nTransakcję autoryzujesz w aplikacji w ciągu 30 dni.\n";

    [Theory]
    [InlineData("6", "a) numer rachunku odbiorcy (NRB lub IBAN)", "Podajesz numer rachunku.")]
    [InlineData("6. Jakie informacje musisz podać?", "kwotę i walutę", "Podajesz kwotę i walutę.")]
    [InlineData("6", "złożony po godzinie 13:00", "Przelew po 13:00 realizujemy następnego dnia.")]
    [InlineData("7", "„TRANSAKCJĘ autoryzujesz w aplikacji”", "W aplikacji, w ciągu 30 dni.")]
    [InlineData(null, "Transakcję autoryzujesz w aplikacji", "W ciągu 30 dni.")]
    public void GroundedCandidate_HasNoProblems(string? unit, string quote, string answer)
    {
        Assert.Empty(FaqGrounding.Problems(Candidate(unit, quote, answer), Markdown));
    }

    [Fact]
    public void QuoteFromAnotherUnit_IsProblem()
    {
        FaqCandidate candidate = Candidate("6", "Transakcję autoryzujesz w aplikacji", "W aplikacji.");

        Assert.Equal(["kandydat D1-K2: cytat nie występuje w jednostce „6”"], FaqGrounding.Problems(candidate, Markdown));
    }

    [Fact]
    public void QuoteNotInDocument_WithoutUnit_IsProblem()
    {
        FaqCandidate candidate = Candidate(null, "tego zdania nie ma w dokumencie", "Tak.");

        Assert.Equal(["kandydat D1-K2: cytat nie występuje w dokumencie D1"], FaqGrounding.Problems(candidate, Markdown));
    }

    [Fact]
    public void ShortQuote_IsProblem()
    {
        FaqCandidate candidate = Candidate("6", "numer rachunku", "Numer rachunku.");

        Assert.Equal(["kandydat D1-K2: cytat ma mniej niż 3 słowa"], FaqGrounding.Problems(candidate, Markdown));
    }

    [Fact]
    public void NumberOutsideUnit_IsProblem()
    {
        FaqCandidate candidate = Candidate("6", "kwotę i walutę", "Podajesz kwotę w ciągu 30 dni.");

        Assert.Equal(["kandydat D1-K2: liczba „30” nie występuje w jednostce „6”"], FaqGrounding.Problems(candidate, Markdown));
    }

    [Fact]
    public void PageMarkers_AreNotText()
    {
        FaqCandidate candidate = Candidate("7", "Transakcję autoryzujesz w aplikacji", "W ciągu 12 dni.");

        Assert.Equal(["kandydat D1-K2: liczba „12” nie występuje w jednostce „7”"], FaqGrounding.Problems(candidate, Markdown));
    }

    [Fact]
    public void Numbers_AreDigitRuns()
    {
        Assert.Equal(["13", "00", "30", "2026"], FaqGrounding.Numbers("Po 13:00, w ciągu 30 dni (od 2026 r.)."));
    }

    private static FaqCandidate Candidate(string? unit, string quote, string answer) =>
        new("D1-K2", "D1", "Pytanie?", answer, unit, quote);
}
