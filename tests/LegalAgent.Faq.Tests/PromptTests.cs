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
        Assert.Contains("z listy „Jednostki dokumentu”", system, StringComparison.Ordinal);
        Assert.Contains("pusty tekst", system, StringComparison.Ordinal);
        Assert.Contains("„quote”", system, StringComparison.Ordinal);
        Assert.Contains("dosłownie", system, StringComparison.Ordinal);
        Assert.Contains("\"quote\"", FaqSchemas.Candidates, StringComparison.Ordinal);
        Assert.Contains("kogo dotyczy", system, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CandidateUserMessage_ListsDocumentUnitsAfterMarkdown()
    {
        string[] units = ["Rozdział 1", "6. Jakie informacje musisz podać, gdy składasz zlecenie płatnicze?"];

        string user = (await RunAsync(candidates: 7, units)).Calls[0].User;

        int markdown = user.IndexOf("Treść dokumentu testowego.", StringComparison.Ordinal);
        int list = user.IndexOf("Jednostki dokumentu D1", StringComparison.Ordinal);
        Assert.True(markdown >= 0 && list > markdown, user);
        Assert.Contains("\n- Rozdział 1\n- 6. Jakie informacje musisz podać, gdy składasz zlecenie płatnicze?\n", user, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CandidateUserMessage_WithoutUnits_AsksForEmptyUnit()
    {
        string user = (await RunAsync(candidates: 7)).Calls[0].User;

        Assert.Contains("Jednostki dokumentu D1: brak", user, StringComparison.Ordinal);
        Assert.Contains("pusty tekst", user, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectionMessage_HasRequiredElements()
    {
        string system = (await RunAsync(candidates: 7)).Calls[2].System;

        Assert.Contains("Wybierz od 2 do 7", system, StringComparison.Ordinal);
        Assert.Contains("uszereguj", system, StringComparison.Ordinal);
        Assert.Contains("najważniejsz", system, StringComparison.Ordinal);
        Assert.Contains("połączyć", system, StringComparison.Ordinal);
        Assert.Contains("faktów, których nie ma w kandydatach", system, StringComparison.Ordinal);
        Assert.Contains("basedOn", system, StringComparison.Ordinal);
        Assert.Contains("jednej sprawy", system, StringComparison.Ordinal);
        Assert.Contains("kogo dotyczy", system, StringComparison.Ordinal);
        Assert.Contains("od 1 do 3 pozycji", system, StringComparison.Ordinal);
        Assert.Contains("z każdego dokumentu co najmniej 2", system, StringComparison.Ordinal);
        Assert.DoesNotContain("sources", system, StringComparison.Ordinal);
        Assert.DoesNotContain("sources", FaqSchemas.Selection, StringComparison.Ordinal);
        Assert.Contains("po polsku", system, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectionMessage_LimitPerDocument_CoversItemCount()
    {
        var chat = new FakeChatCompletionService()
            .Respond(FaqJson.Candidates("D1", 5))
            .Respond(FaqJson.Candidates("D2", 5))
            .Respond(FaqJson.Selection(10, 2));
        var generator = new FaqGenerator(chat, new FaqGeneratorOptions(), (_, _) => new PromptExecutionSettings());
        FaqDocumentInput[] documents =
        [
            new("A", new Uri("https://example.test/a.pdf"), "# A\n\nTreść dokumentu testowego.\n", []),
            new("B", new Uri("https://example.test/b.pdf"), "# B\n\nTreść dokumentu testowego.\n", []),
        ];

        await generator.GenerateAsync(documents, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("od 1 do 5 pozycji", chat.Calls[2].System, StringComparison.Ordinal);
    }

    private static async Task<FakeChatCompletionService> RunAsync(int candidates, string[]? units = null)
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
            new("A", new Uri("https://example.test/a.pdf"), "# A\n\nTreść dokumentu testowego.\n", units ?? []),
            new("B", new Uri("https://example.test/b.pdf"), "# B\n\nTreść dokumentu testowego.\n", []),
        ];
        await generator.GenerateAsync(documents, cancellationToken: TestContext.Current.CancellationToken);
        return chat;
    }
}
