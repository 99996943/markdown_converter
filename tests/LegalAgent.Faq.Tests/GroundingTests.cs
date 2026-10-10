using LegalAgent.Faq.Model;

namespace LegalAgent.Faq.Tests;

/// <summary>Quote and numbers of a candidate against the text of its unit (T067d, T067h).</summary>
public sealed class GroundingTests
{
    private const string Markdown =
        "# Regulamin\n\n<!-- page: 1 -->\n## 6. Jakie informacje musisz podać?\n\n"
        + "- 1\\) Musisz podać:\n  - a\\) numer rachunku odbiorcy (NRB lub IBAN),\n  - b\\) **kwotę** i walutę.\n\n"
        + "### Dodatkowe wyjaśnienia\n\nPrzelew SWIFT złożony po godzinie 13.00 realizujemy następnego dnia.\n\n"
        + "## 7. Jak autoryzujesz transakcję?\n\n<!-- page: 12 -->\nTransakcję autoryzujesz w aplikacji w ciągu 30 dni.\n\n"
        + "## Dodatkowe wyjaśnienia\n\nReklamację rozpatrzymy w ciągu 14 dni od jej otrzymania.\n\n"
        + "## 8. Postanowienia końcowe\n\nOstatni rozdział regulaminu banku.\n";

    [Theory]
    [InlineData("6", "a) numer rachunku odbiorcy (NRB lub IBAN)", "Podajesz numer rachunku.")]
    [InlineData("6. Jakie informacje musisz podać?", "kwotę i walutę", "Podajesz kwotę i walutę.")]
    [InlineData("6", "złożony po godzinie 13:00", "Przelew po 13:00 realizujemy następnego dnia.")]
    [InlineData("7", "„TRANSAKCJĘ autoryzujesz w aplikacji”", "W aplikacji, w ciągu 30 dni.")]
    [InlineData(null, "Transakcję autoryzujesz w aplikacji", "W ciągu 30 dni.")]
    [InlineData("7", "Reklamację rozpatrzymy w ciągu 14 dni", "W ciągu 14 dni.")]
    [InlineData("6", "Musisz podać: … kwotę i walutę", "Kwotę i walutę.")]
    [InlineData("6", "Przelew SWIFT złożony po godzinie 13.00 realizujemy następnego dnia roboczego", "Po 13:00.")]
    public void GroundedCandidate_HasNoProblem(string? unit, string quote, string answer)
    {
        Assert.Null(FaqGrounding.Problem(Candidate(unit, quote, answer), Markdown));
    }

    [Theory]
    [InlineData("6", "Transakcję autoryzujesz w aplikacji", "W aplikacji.", "cytat „Transakcję autoryzujesz w aplikacji” nie występuje w jednostce „6”")]
    [InlineData("7", "Transakcję zatwierdzasz w aplikacji mobilnej banku", "W aplikacji.", "cytat „Transakcję zatwierdzasz w aplikacji mobilnej banku” nie występuje w jednostce „7”")]
    [InlineData("7", "Ostatni rozdział regulaminu banku", "Na końcu.", "cytat „Ostatni rozdział regulaminu banku” nie występuje w jednostce „7”")]
    [InlineData(null, "tego zdania nie ma w dokumencie", "Tak.", "cytat „tego zdania nie ma w dokumencie” nie występuje w dokumencie D1")]
    [InlineData("6", "numer rachunku", "Numer rachunku.", "cytat ma mniej niż 3 słowa")]
    [InlineData("6", "kwotę i walutę", "W ciągu 30 albo 45 dni.", "liczby „30”, „45” nie występują w jednostce „6”")]
    [InlineData("7", "Transakcję autoryzujesz w aplikacji", "W ciągu 12 dni.", "liczba „12” nie występuje w jednostce „7”")]
    [InlineData("6", "Transakcję autoryzujesz w aplikacji", "W ciągu 30 dni.", "cytat „Transakcję autoryzujesz w aplikacji” nie występuje w jednostce „6”; liczba „30” nie występuje w jednostce „6”")]
    public void UngroundedCandidate_HasOneLineProblem(string? unit, string quote, string answer, string reasons)
    {
        Assert.Equal("kandydat D1-K2: " + reasons, FaqGrounding.Problem(Candidate(unit, quote, answer), Markdown));
    }

    [Theory]
    [InlineData("Posiadacze rachunku wspólnego zgadzają się nieodwołalnie na to, aby każdy z nich mógł samodzielnie dysponować pieniędzmi na rachunku, wypowiedzieć umowę, odstąpić od umowy")]
    [InlineData("każdy z nich mógł samodzielnie: a) dysponować pieniędzmi na rachunku, b) wypowiedzieć umowę")]
    public void QuoteAcrossListItems_IgnoresLabels(string quote)
    {
        const string markdown =
            "## 6. Rachunki wspólne\n\n- 6\\) Posiadacze rachunku wspólnego zgadzają się nieodwołalnie na to, aby każdy z nich mógł samodzielnie:\n"
            + "  - a\\) dysponować pieniędzmi na rachunku,\n  - b\\) wypowiedzieć umowę,\n  - c\\) odstąpić od umowy.\n";

        Assert.Null(FaqGrounding.Problem(Candidate("6", quote, "Każdy z posiadaczy."), markdown));
    }

    [Fact]
    public void LongQuote_IsShortenedInProblem()
    {
        const string quote = "tego zdania nie ma w dokumencie ani w żadnym innym regulaminie tego banku";

        Assert.Equal(
            "kandydat D1-K2: cytat „" + quote[..57] + "…” nie występuje w dokumencie D1",
            FaqGrounding.Problem(Candidate(null, quote, "Tak."), Markdown));
    }

    [Fact]
    public void Numbers_AreDigitRuns()
    {
        Assert.Equal(["13", "00", "30", "2026"], FaqGrounding.Numbers("Po 13:00, w ciągu 30 dni (od 2026 r.)."));
    }

    private static FaqCandidate Candidate(string? unit, string quote, string answer) =>
        new("D1-K2", "D1", "Pytanie?", answer, unit, quote);
}
