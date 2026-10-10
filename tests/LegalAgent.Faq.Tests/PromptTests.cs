using LegalAgent.Faq.Model;
using LegalAgent.Faq.Tests.Fakes;
using Microsoft.SemanticKernel;

namespace LegalAgent.Faq.Tests;

/// <summary>Required elements of the system messages (contracts/model-exchange.md).</summary>
public sealed class PromptTests
{
    [Fact]
    public async Task CandidateMessage_HasRequiredElements()
    {
        string system = (await RunAsync(candidates: 7)).Calls[0].System;

        Assert.Contains("FAQ", system, StringComparison.Ordinal);
        Assert.Contains("klient", system, StringComparison.Ordinal);
        Assert.Contains("wyłącznie na podstawie", system, StringComparison.Ordinal);
        Assert.Contains("spoza dokumentu", system, StringComparison.Ordinal);
        Assert.Contains("Dokument nie rozstrzyga", system, StringComparison.Ordinal);
        Assert.Contains("po polsku", system, StringComparison.Ordinal);
        Assert.Contains("co najwyżej 7", system, StringComparison.Ordinal);
        Assert.Contains("dokładnie tak, jak w nagłówku dokumentu", system, StringComparison.Ordinal);
        Assert.Contains("pusty tekst", system, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectionMessage_HasRequiredElements()
    {
        string system = (await RunAsync(candidates: 7)).Calls[2].System;

        Assert.Contains("dokładnie 2", system, StringComparison.Ordinal);
        Assert.Contains("najważniejsz", system, StringComparison.Ordinal);
        Assert.Contains("połączyć", system, StringComparison.Ordinal);
        Assert.Contains("faktów, których nie ma w kandydatach", system, StringComparison.Ordinal);
        Assert.Contains("basedOn", system, StringComparison.Ordinal);
        Assert.Contains("sources", system, StringComparison.Ordinal);
        Assert.Contains("po polsku", system, StringComparison.Ordinal);
    }

    private static async Task<FakeChatCompletionService> RunAsync(int candidates)
    {
        var chat = new FakeChatCompletionService()
            .Respond(FaqJson.Candidates("D1", 2))
            .Respond(FaqJson.Candidates("D2", 2))
            .Respond(FaqJson.Selection(2, 2));
        var generator = new FaqGenerator(
            chat,
            new FaqGeneratorOptions { CandidatesPerDocument = candidates, ItemCount = 2 },
            (_, _) => new PromptExecutionSettings());
        FaqDocumentInput[] documents =
        [
            new("A", new Uri("https://example.test/a.pdf"), "# A\n\nTreść.\n", []),
            new("B", new Uri("https://example.test/b.pdf"), "# B\n\nTreść.\n", []),
        ];
        await generator.GenerateAsync(documents, cancellationToken: TestContext.Current.CancellationToken);
        return chat;
    }
}
