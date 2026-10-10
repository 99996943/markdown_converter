using LegalAgent.Faq.Model;

namespace LegalAgent.Faq.Tests;

public sealed class ValidatorSelectionTests
{
    private static readonly FaqCandidate[] Candidates =
    [
        new("D1-K1", "D1", "P1?", "O1.", "§ 1"),
        new("D1-K2", "D1", "P2?", "O2.", null),
        new("D2-K1", "D2", "P3?", "O3.", "Art. 5"),
        new("D3-K1", "D3", "P4?", "O4.", null),
        new("D1-K3", "D1", "P5?", "O5.", "§ 1"),
    ];

    [Fact]
    public void ValidSelection_GivesNumberedItems()
    {
        ParsedItem[] items = [.. Enumerable.Range(1, 3).Select(i => Item(i))];
        items[1] = items[1] with { Question = "  Pytanie z odstępami?  ", Answer = "\nOdpowiedź.\n\nDrugi akapit.\n" };

        IReadOnlyList<FaqItem> result = FaqResponseValidator.ValidateSelection(items, Candidates, 3);

        Assert.Equal([1, 2, 3], result.Select(i => i.Number));
        Assert.Equal("Pytanie z odstępami?", result[1].Question);
        Assert.Equal("Odpowiedź.\n\nDrugi akapit.", result[1].Answer);
        Assert.Equal([new FaqSource("D1", "§ 1")], result[0].Sources);
        Assert.Equal(["D1-K1"], result[0].BasedOn);
    }

    [Fact]
    public void Sources_ComeFromBasedOnCandidates_InOrder_WithoutDuplicates()
    {
        ParsedItem[] items =
        [
            Item(1) with { BasedOn = ["D2-K1", "D1-K1", "D1-K3"] },
            Item(2) with { BasedOn = ["D1-K2"] },
            Item(3) with { BasedOn = ["D3-K1", "D2-K1"] },
        ];

        IReadOnlyList<FaqItem> result = FaqResponseValidator.ValidateSelection(items, Candidates, 3);

        Assert.Equal([new FaqSource("D2", "Art. 5"), new FaqSource("D1", "§ 1")], result[0].Sources);
        Assert.Equal([new FaqSource("D1", null)], result[1].Sources);
        Assert.Equal([new FaqSource("D3", null), new FaqSource("D2", "Art. 5")], result[2].Sources);
    }

    [Fact]
    public void Sources_DocumentWithUnit_DropsItsSourceWithoutUnit()
    {
        ParsedItem[] items = [Item(1) with { BasedOn = ["D1-K2", "D1-K1"] }, Item(2), Item(3)];

        IReadOnlyList<FaqItem> result = FaqResponseValidator.ValidateSelection(items, Candidates, 3);

        Assert.Equal([new FaqSource("D1", "§ 1")], result[0].Sources);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void WrongCount_Rejected(int count)
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [.. Enumerable.Range(1, count).Select(i => Item(i))],
            Candidates,
            3));

        Assert.Equal(FaqStep.Selection, e.Step);
        Assert.Null(e.DocumentId);
        Assert.Equal([$"liczba pozycji {count} zamiast 3"], e.Problems);
        Assert.Contains("Odpowiedź modelu odrzucona (krok wyboru):", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BlankFieldsAndEmptyBasedOn_Rejected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [Item(1) with { Question = " " }, Item(2) with { Answer = "" }, Item(3) with { BasedOn = [] }],
            Candidates,
            3));

        Assert.Equal(["pozycja 1: puste pytanie", "pozycja 2: pusta odpowiedź", "pozycja 3: puste basedOn"], e.Problems);
    }

    [Fact]
    public void RepeatedQuestion_Rejected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [Item(1), Item(2), Item(3) with { Question = "pytanie końcowe 1" }],
            Candidates,
            3));

        Assert.Equal(["pozycja 3: powtórzone pytanie (jak w pozycji 1)"], e.Problems);
    }

    [Fact]
    public void UnknownCandidate_Rejected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [Item(1), Item(2) with { BasedOn = ["D1-K2", "D1-K9"] }, Item(3)],
            Candidates,
            3));

        Assert.Equal(["pozycja 2: kandydat D1-K9 nie istnieje"], e.Problems);
    }

    private static ParsedItem Item(int i) =>
        new($"Pytanie końcowe {i}?", $"Odpowiedź końcowa {i}.", [i == 1 ? "D1-K1" : "D1-K2"]);
}
