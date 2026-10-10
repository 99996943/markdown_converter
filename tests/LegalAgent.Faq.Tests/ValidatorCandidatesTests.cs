using LegalAgent.Faq.Model;

namespace LegalAgent.Faq.Tests;

public sealed class ValidatorCandidatesTests
{
    private static readonly string[] Units = ["Rozdział 1", "Rozdział 1. Postanowienia ogólne", "§ 1", "§ 1.", "§ 2", "§ 2."];

    [Fact]
    public void ValidCandidates_Accepted()
    {
        FaqResponseValidator.ValidateCandidates([C(1, "Pytanie?", unit: "§ 1"), C(2, "Inne pytanie?")], "D2", Units, 10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void CountOutOfRange_Rejected(int count)
    {
        FaqCandidate[] candidates = [.. Enumerable.Range(1, count).Select(k => C(k, $"Pytanie {k}?"))];

        FaqResponseException e = Assert.Throws<FaqResponseException>(
            () => FaqResponseValidator.ValidateCandidates(candidates, "D2", Units, 3));

        Assert.Equal(FaqStep.Candidates, e.Step);
        Assert.Equal("D2", e.DocumentId);
        Assert.Contains(e.Problems, p => p.Contains($"liczba kandydatów {count} poza zakresem 1–3", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void CountAtBoundary_Accepted(int count)
    {
        FaqResponseValidator.ValidateCandidates([.. Enumerable.Range(1, count).Select(k => C(k, $"Pytanie {k}?"))], "D2", Units, 3);
    }

    [Fact]
    public void BlankFields_Rejected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateCandidates(
            [C(1, "Pytanie?"), C(2, "  "), C(3, "Trzecie?", answer: "\n")],
            "D1",
            Units,
            10));

        Assert.Equal(["pozycja 2: puste pytanie", "pozycja 3: pusta odpowiedź"], e.Problems);
    }

    [Theory]
    [InlineData("ile kosztuje karta?")]
    [InlineData("Ile  kosztuje   karta")]
    [InlineData(" ILE KOSZTUJE KARTA ? ")]
    public void RepeatedQuestion_Rejected(string repeated)
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateCandidates(
            [C(1, "Ile kosztuje karta?"), C(2, repeated)],
            "D1",
            Units,
            10));

        Assert.Equal(["pozycja 2: powtórzone pytanie (jak w pozycji 1)"], e.Problems);
    }

    [Fact]
    public void UnknownUnit_Rejected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateCandidates(
            [C(1, "Pytanie?", unit: "§ 12"), C(2, "Drugie?", unit: "§ 2 ust. 1")],
            "D1",
            Units,
            10));

        Assert.Equal(["pozycja 1: jednostka „§ 12” nie występuje w dokumencie D1"], e.Problems);
    }

    [Fact]
    public void AllProblemsCollected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateCandidates(
            [C(1, ""), C(2, "P?", unit: "Art. 5"), C(3, "p"), C(4, "Q?", answer: "")],
            "D4",
            Units,
            3));

        Assert.Equal(
            [
                "liczba kandydatów 4 poza zakresem 1–3",
                "pozycja 1: puste pytanie",
                "pozycja 2: jednostka „Art. 5” nie występuje w dokumencie D4",
                "pozycja 3: powtórzone pytanie (jak w pozycji 2)",
                "pozycja 4: pusta odpowiedź",
            ],
            e.Problems);
        Assert.Contains("Odpowiedź modelu odrzucona (krok kandydatów, D4):", e.Message, StringComparison.Ordinal);
    }

    private static FaqCandidate C(int k, string question, string answer = "Odpowiedź.", string? unit = null) =>
        new($"D1-K{k}", "D1", question, answer, unit);
}
