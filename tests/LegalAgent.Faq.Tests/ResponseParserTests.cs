using LegalAgent.Faq.Model;
using LegalAgent.Faq.Tests.Fakes;

namespace LegalAgent.Faq.Tests;

public sealed class ResponseParserTests
{
    private const string NotJson = "odpowiedź nie jest poprawnym JSON-em zgodnym ze schematem";

    [Fact]
    public void Candidates_GetIdsInResponseOrder()
    {
        string json = FaqJson.Candidates(
            new CandidateJson("Ile kosztuje karta?", "10 zł.", "§ 3"),
            new CandidateJson("Jak zamknąć rachunek?", "Pisemnie.", ""));

        Parsed<IReadOnlyList<FaqCandidate>> parsed = FaqResponseParser.ParseCandidates(json, "D2");

        Assert.Null(parsed.Problem);
        Assert.Equal(
            [
                new FaqCandidate("D2-K1", "D2", "Ile kosztuje karta?", "10 zł.", "§ 3"),
                new FaqCandidate("D2-K2", "D2", "Jak zamknąć rachunek?", "Pisemnie.", null),
            ],
            parsed.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Candidates_BlankUnit_IsNull(string unit)
    {
        Parsed<IReadOnlyList<FaqCandidate>> parsed = FaqResponseParser.ParseCandidates(
            FaqJson.Candidates(new CandidateJson("P?", "O.", unit)),
            "D1");

        Assert.Null(Assert.Single(parsed.Value!).Unit);
    }

    [Fact]
    public void Selection_ParsesItems()
    {
        string json = FaqJson.Selection(
            new ItemJson("Pytanie?", "Odpowiedź.", ["D1-K1", "D2-K3"], [new SourceJson("D1", "§ 3"), new SourceJson("D2", " ")]));

        Parsed<IReadOnlyList<ParsedItem>> parsed = FaqResponseParser.ParseSelection(json);

        Assert.Null(parsed.Problem);
        ParsedItem item = Assert.Single(parsed.Value!);
        Assert.Equal("Pytanie?", item.Question);
        Assert.Equal("Odpowiedź.", item.Answer);
        Assert.Equal(["D1-K1", "D2-K3"], item.BasedOn);
        Assert.Equal([new FaqSource("D1", "§ 3"), new FaqSource("D2", null)], item.Sources);
    }

    [Theory]
    [InlineData("to nie jest JSON")]
    [InlineData("{\"candidates\":[{\"question\":\"P?\",\"answer\":\"O.\",\"unit\":\"\"}]")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"candidates\":{}}")]
    [InlineData("{\"candidates\":[{\"answer\":\"O.\",\"unit\":\"\"}]}")]
    [InlineData("{\"candidates\":[{\"question\":5,\"answer\":\"O.\",\"unit\":\"\"}]}")]
    [InlineData("{\"candidates\":[{\"question\":\"P?\",\"answer\":\"O.\",\"unit\":null}]}")]
    [InlineData("{\"candidates\":[{\"question\":\"P?\",\"answer\":\"O.\",\"unit\":\"\",\"extra\":1}]}")]
    [InlineData("Oto wynik: {\"candidates\":[]}")]
    [InlineData("{\"candidates\":[]} Gotowe.")]
    [InlineData("```json\n{\"candidates\":[]}\n```")]
    public void Candidates_InvalidJson_IsProblem(string json)
    {
        Parsed<IReadOnlyList<FaqCandidate>> parsed = FaqResponseParser.ParseCandidates(json, "D1");

        Assert.Null(parsed.Value);
        Assert.Equal(NotJson, parsed.Problem);
    }

    [Theory]
    [InlineData("{\"items\":[{\"question\":\"P?\",\"answer\":\"O.\",\"basedOn\":\"D1-K1\",\"sources\":[]}]}")]
    [InlineData("{\"items\":[{\"question\":\"P?\",\"answer\":\"O.\",\"basedOn\":[1],\"sources\":[]}]}")]
    [InlineData("{\"items\":[{\"question\":\"P?\",\"answer\":\"O.\",\"basedOn\":[],\"sources\":[{\"documentId\":\"D1\"}]}]}")]
    [InlineData("{\"items\":[{\"question\":\"P?\",\"answer\":\"O.\",\"basedOn\":[]}]}")]
    [InlineData("{\"candidates\":[]}")]
    [InlineData("")]
    public void Selection_InvalidJson_IsProblem(string json)
    {
        Parsed<IReadOnlyList<ParsedItem>> parsed = FaqResponseParser.ParseSelection(json);

        Assert.Null(parsed.Value);
        Assert.Equal(NotJson, parsed.Problem);
    }

    [Fact]
    public void Candidates_WhitespaceAroundJson_Accepted()
    {
        Parsed<IReadOnlyList<FaqCandidate>> parsed = FaqResponseParser.ParseCandidates(
            "\n  " + FaqJson.Candidates("D1", 2) + "\n",
            "D1");

        Assert.Equal(2, parsed.Value!.Count);
    }
}
