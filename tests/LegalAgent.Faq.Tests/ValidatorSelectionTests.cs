using LegalAgent.Faq.Model;

namespace LegalAgent.Faq.Tests;

public sealed class ValidatorSelectionTests
{
    private static readonly Dictionary<string, IReadOnlyList<string>> Units = new(StringComparer.Ordinal)
    {
        ["D1"] = ["§ 1", "§ 2"],
        ["D2"] = ["Art. 5"],
        ["D3"] = [],
    };

    private static readonly FaqCandidate[] Candidates =
    [
        new("D1-K1", "D1", "P1?", "O1.", "§ 1"),
        new("D1-K2", "D1", "P2?", "O2.", null),
        new("D2-K1", "D2", "P3?", "O3.", "Art. 5"),
        new("D3-K1", "D3", "P4?", "O4.", null),
    ];

    [Fact]
    public void ValidSelection_GivesNumberedItems()
    {
        ParsedItem[] items = [.. Enumerable.Range(1, 3).Select(i => Item(i))];
        items[1] = items[1] with { Question = "  Pytanie z odstępami?  ", Answer = "\nOdpowiedź.\n\nDrugi akapit.\n" };

        IReadOnlyList<FaqItem> result = FaqResponseValidator.ValidateSelection(items, Candidates, Units, 3);

        Assert.Equal([1, 2, 3], result.Select(i => i.Number));
        Assert.Equal("Pytanie z odstępami?", result[1].Question);
        Assert.Equal("Odpowiedź.\n\nDrugi akapit.", result[1].Answer);
        Assert.Equal([new FaqSource("D1", "§ 1")], result[0].Sources);
        Assert.Equal(["D1-K1"], result[0].BasedOn);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void WrongCount_Rejected(int count)
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [.. Enumerable.Range(1, count).Select(i => Item(i))],
            Candidates,
            Units,
            3));

        Assert.Equal(FaqStep.Selection, e.Step);
        Assert.Null(e.DocumentId);
        Assert.Equal([$"liczba pozycji {count} zamiast 3"], e.Problems);
        Assert.Contains("Odpowiedź modelu odrzucona (krok wyboru):", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BlankFieldsAndEmptyLists_Rejected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [Item(1) with { Question = " " }, Item(2) with { Answer = "" }, Item(3) with { BasedOn = [], Sources = [] }],
            Candidates,
            Units,
            3));

        Assert.Equal(
            ["pozycja 1: puste pytanie", "pozycja 2: pusta odpowiedź", "pozycja 3: puste basedOn", "pozycja 3: puste sources"],
            e.Problems);
    }

    [Fact]
    public void RepeatedQuestion_Rejected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [Item(1), Item(2), Item(3) with { Question = "pytanie końcowe 1" }],
            Candidates,
            Units,
            3));

        Assert.Equal(["pozycja 3: powtórzone pytanie (jak w pozycji 1)"], e.Problems);
    }

    [Fact]
    public void UnknownCandidate_Rejected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [Item(1), Item(2) with { BasedOn = ["D1-K2", "D1-K9"] }, Item(3)],
            Candidates,
            Units,
            3));

        Assert.Equal(["pozycja 2: kandydat D1-K9 nie istnieje"], e.Problems);
    }

    [Fact]
    public void UnknownDocument_Rejected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [Item(1), Item(2) with { Sources = [new FaqSource("D1", null), new FaqSource("D9", null)] }, Item(3)],
            Candidates,
            Units,
            3));

        Assert.Equal(["pozycja 2: dokument D9 nie istnieje"], e.Problems);
    }

    [Fact]
    public void SourceDocumentNotOfBasedOnCandidates_Rejected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [Item(1) with { BasedOn = ["D1-K1"], Sources = [new FaqSource("D2", null)] }, Item(2), Item(3)],
            Candidates,
            Units,
            3));

        Assert.Equal(["pozycja 1: dokument D2 nie jest dokumentem żadnego z kandydatów basedOn"], e.Problems);
    }

    [Fact]
    public void UnknownUnit_Rejected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [
                Item(1) with { Sources = [new FaqSource("D1", "§ 2 ust. 1")] },
                Item(2) with { BasedOn = ["D2-K1"], Sources = [new FaqSource("D2", "Art. 5a")] },
                Item(3) with { BasedOn = ["D3-K1"], Sources = [new FaqSource("D3", "§ 1")] },
            ],
            Candidates,
            Units,
            3));

        Assert.Equal(
            [
                "pozycja 2: jednostka „Art. 5a” nie występuje w dokumencie D2",
                "pozycja 3: jednostka „§ 1” nie występuje w dokumencie D3",
            ],
            e.Problems);
    }

    private static ParsedItem Item(int i) =>
        new($"Pytanie końcowe {i}?", $"Odpowiedź końcowa {i}.", ["D1-K1"], [new FaqSource("D1", i == 1 ? "§ 1" : null)]);
}
