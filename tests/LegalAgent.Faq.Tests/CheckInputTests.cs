using LegalAgent.Faq.Model;
using LegalAgent.Faq.Tests.Fakes;
using Microsoft.SemanticKernel;

namespace LegalAgent.Faq.Tests;

public sealed class CheckInputTests
{
    public static TheoryData<FaqGeneratorOptions> InvalidOptions => new()
    {
        new FaqGeneratorOptions { CandidatesPerDocument = 0 },
        new FaqGeneratorOptions { CandidatesPerDocument = 31 },
        new FaqGeneratorOptions { ItemCount = 0 },
        new FaqGeneratorOptions { MaxDocumentTokens = 0 },
        new FaqGeneratorOptions { CharactersPerToken = 0 },
        new FaqGeneratorOptions { CharactersPerToken = double.NaN },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Constructor_InvalidOptions_Throws(FaqGeneratorOptions options)
    {
        Assert.Throws<ArgumentException>(() => new FaqGenerator(new FakeChatCompletionService(), options, (_, _) => new PromptExecutionSettings()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public void Constructor_BoundaryCandidates_Accepted(int candidates)
    {
        _ = new FaqGenerator(
            new FakeChatCompletionService(),
            new FaqGeneratorOptions { CandidatesPerDocument = candidates },
            (_, _) => new PromptExecutionSettings());
    }

    [Fact]
    public void CheckInput_EstimatesTokensRoundingUp()
    {
        IReadOnlyList<FaqInputEstimate> estimates = FaqGenerator.CheckInput(
            [Doc("a.md", new string('x', 10)), Doc("b.md", new string('y', 9))],
            new FaqGeneratorOptions());

        Assert.Equal([new FaqInputEstimate("a.md", 10, 4), new FaqInputEstimate("b.md", 9, 3)], estimates);
    }

    [Fact]
    public void CheckInput_FirstTooLongDocument_Throws()
    {
        var options = new FaqGeneratorOptions { MaxDocumentTokens = 10 };

        FaqInputTooLongException e = Assert.Throws<FaqInputTooLongException>(() => FaqGenerator.CheckInput(
            [Doc("a.md", new string('x', 30)), Doc("b.md", new string('y', 31)), Doc("c.md", new string('z', 90))],
            options));

        Assert.Equal("b.md", e.DocumentName);
        Assert.Equal(31, e.Characters);
        Assert.Equal(11, e.EstimatedTokens);
        Assert.Equal(10, e.Limit);
    }

    [Fact]
    public void CheckInput_EmptyList_Throws()
    {
        Assert.Throws<ArgumentException>(() => FaqGenerator.CheckInput([], new FaqGeneratorOptions()));
    }

    public static TheoryData<FaqDocumentInput> InvalidDocuments => new()
    {
        new FaqDocumentInput("a.md", new Uri("https://example.test/a.pdf"), "", []),
        new FaqDocumentInput("a.md", new Uri("https://example.test/a.pdf"), "   ", []),
        new FaqDocumentInput("", new Uri("https://example.test/a.pdf"), "treść", []),
        new FaqDocumentInput("a.md", new Uri("a.pdf", UriKind.Relative), "treść", []),
    };

    [Theory]
    [MemberData(nameof(InvalidDocuments))]
    public void CheckInput_InvalidDocument_Throws(FaqDocumentInput document)
    {
        Assert.Throws<ArgumentException>(() => FaqGenerator.CheckInput([document], new FaqGeneratorOptions()));
    }

    private static FaqDocumentInput Doc(string name, string markdown) =>
        new(name, new Uri($"https://example.test/{name}"), markdown, []);
}
