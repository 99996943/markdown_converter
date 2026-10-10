using LegalAgent.Faq.Model;

namespace LegalAgent.Faq.Tests;

/// <summary>Checking the ranked selection and choosing the final items from it (T067l).</summary>
public sealed class ValidatorSelectionTests
{
    private static readonly FaqCandidate[] Candidates =
    [
        new("D1-K1", "D1", "P1?", "O1.", "§ 1"),
        new("D1-K2", "D1", "P2?", "O2.", null),
        new("D2-K1", "D2", "P3?", "O3.", "Art. 5"),
        new("D3-K1", "D3", "P4?", "O4.", null),
        new("D1-K3", "D1", "P5?", "O5.", "§ 1"),
        new("D2-K2", "D2", "P6?", "O6.", null, "Opłata wynosi 30 zł miesięcznie"),
    ];

    [Fact]
    public void ValidSelection_GivesNumberedItems()
    {
        ParsedItem[] items = [.. Enumerable.Range(1, 3).Select(i => Item(i))];
        items[1] = items[1] with { Question = "  Pytanie z odstępami?  ", Answer = "\nOdpowiedź.\n\nDrugi akapit.\n" };

        SelectionResult result = FaqResponseValidator.ValidateSelection(items, Candidates, 3);

        Assert.Equal([1, 2, 3], result.Items.Select(i => i.Number));
        Assert.Equal("Pytanie z odstępami?", result.Items[1].Question);
        Assert.Equal("Odpowiedź.\n\nDrugi akapit.", result.Items[1].Answer);
        Assert.Equal([new FaqSource("D1", "§ 1")], result.Items[0].Sources);
        Assert.Equal(["D1-K1"], result.Items[0].BasedOn);
        Assert.Empty(result.Skipped);
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

        IReadOnlyList<FaqItem> result = FaqResponseValidator.ValidateSelection(items, Candidates, 3).Items;

        Assert.Equal([new FaqSource("D2", "Art. 5"), new FaqSource("D1", "§ 1")], result[0].Sources);
        Assert.Equal([new FaqSource("D1", null)], result[1].Sources);
        Assert.Equal([new FaqSource("D3", null), new FaqSource("D2", "Art. 5")], result[2].Sources);
    }

    [Fact]
    public void Sources_DocumentWithUnit_DropsItsSourceWithoutUnit()
    {
        ParsedItem[] items = [Item(1) with { BasedOn = ["D1-K2", "D1-K1"] }, Item(2), Item(3)];

        IReadOnlyList<FaqItem> result = FaqResponseValidator.ValidateSelection(items, Candidates, 3).Items;

        Assert.Equal([new FaqSource("D1", "§ 1")], result[0].Sources);
    }

    [Fact]
    public void LargerPool_TopRankedChosen()
    {
        SelectionResult result = FaqResponseValidator.ValidateSelection([.. Enumerable.Range(1, 5).Select(i => Item(i))], Candidates, 3);

        Assert.Equal(["Pytanie końcowe 1?", "Pytanie końcowe 2?", "Pytanie końcowe 3?"], result.Items.Select(i => i.Question));
        Assert.Equal([1, 2, 3], result.Items.Select(i => i.Number));
    }

    [Fact]
    public void TooFewItems_Rejected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [Item(1), Item(2)],
            Candidates,
            3));

        Assert.Equal(FaqStep.Selection, e.Step);
        Assert.Null(e.DocumentId);
        Assert.Equal(["liczba poprawnych pozycji 2, potrzeba co najmniej 3"], e.Problems);
        Assert.Contains("Odpowiedź modelu odrzucona (krok wyboru):", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidItems_AreSkipped()
    {
        ParsedItem[] items =
        [
            Item(1) with { Question = " " },
            Item(2),
            Item(3) with { Answer = "Masz na to 14 dni." },
            Item(4) with { BasedOn = ["D1-K2", "D1-K9"] },
            Item(5) with { Answer = "" },
            Item(6) with { BasedOn = [] },
            Item(7) with { Question = "pytanie końcowe 2" },
            Item(8) with { BasedOn = ["D2-K2"], Answer = "Opłata wynosi 30 zł (O6)." },
        ];

        SelectionResult result = FaqResponseValidator.ValidateSelection(items, Candidates, 2);

        Assert.Equal(["Pytanie końcowe 2?", "Pytanie końcowe 8?"], result.Items.Select(i => i.Question));
        Assert.Equal(
            [
                "pozycja 1: puste pytanie",
                "pozycja 3: liczba „14” nie występuje w kandydatach basedOn",
                "pozycja 4: kandydat D1-K9 nie istnieje",
                "pozycja 5: pusta odpowiedź",
                "pozycja 6: puste basedOn",
                "pozycja 7: powtórzone pytanie (jak w pozycji 2)",
            ],
            result.Skipped);
    }

    [Fact]
    public void SkippedItemsLeavingTooFew_RejectedWithReasons()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [Item(1), Item(2, "D1-K1") with { Answer = "Opłata wynosi 30 zł, a O1 to zasada." }, Item(3)],
            Candidates,
            3));

        Assert.Equal(
            ["liczba poprawnych pozycji 2, potrzeba co najmniej 3", "pozycja 2: liczba „30” nie występuje w kandydatach basedOn"],
            e.Problems);
    }

    [Fact]
    public void Balance_CoversEveryDocumentFirst_ThenFillsInRankOrder()
    {
        ParsedItem[] items =
        [
            Item(1, "D1-K1"), Item(2, "D1-K2"), Item(3, "D1-K3"), Item(4, "D2-K1"), Item(5, "D3-K1"), Item(6, "D2-K2"),
        ];

        SelectionResult result = FaqResponseValidator.ValidateSelection(items, Candidates, 4, minPerDocument: 1, maxPerDocument: 2);

        Assert.Equal(
            ["Pytanie końcowe 1?", "Pytanie końcowe 2?", "Pytanie końcowe 4?", "Pytanie końcowe 5?"],
            result.Items.Select(i => i.Question));
        Assert.Equal([1, 2, 3, 4], result.Items.Select(i => i.Number));
    }

    [Fact]
    public void Balance_ItemOfTwoDocuments_CountsForBoth()
    {
        ParsedItem[] items =
        [
            Item(1) with { BasedOn = ["D1-K1", "D2-K1"] }, Item(2, "D2-K2"), Item(3, "D1-K2"), Item(4, "D3-K1"),
        ];

        SelectionResult result = FaqResponseValidator.ValidateSelection(items, Candidates, 2, minPerDocument: 1, maxPerDocument: 1);

        Assert.Equal(["Pytanie końcowe 1?", "Pytanie końcowe 4?"], result.Items.Select(i => i.Question));
    }

    [Fact]
    public void Balance_DocumentWithoutItems_Rejected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [Item(1, "D1-K1"), Item(2, "D2-K1"), Item(3, "D1-K2")],
            Candidates,
            3,
            minPerDocument: 1,
            maxPerDocument: 2));

        Assert.Equal(["dokument D3: brak pozycji (co najmniej 1)"], e.Problems);
    }

    [Fact]
    public void Balance_LimitLeavesTooFew_Rejected()
    {
        FaqResponseException e = Assert.Throws<FaqResponseException>(() => FaqResponseValidator.ValidateSelection(
            [Item(1, "D1-K1"), Item(2, "D1-K2"), Item(3, "D1-K3"), Item(4, "D2-K1"), Item(5, "D3-K1")],
            Candidates,
            5,
            minPerDocument: 1,
            maxPerDocument: 2));

        Assert.Equal(["po zastosowaniu limitu 2 pozycji na dokument zostają 4 z 5 pozycji"], e.Problems);
    }

    private static ParsedItem Item(int i, string candidateId = "") =>
        new($"Pytanie końcowe {i}?", "Odpowiedź końcowa.", [candidateId.Length > 0 ? candidateId : i == 1 ? "D1-K1" : "D1-K2"]);
}
