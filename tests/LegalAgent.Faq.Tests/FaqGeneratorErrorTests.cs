using System.Globalization;
using System.Net;
using LegalAgent.Faq.Model;
using LegalAgent.Faq.Tests.Fakes;
using Microsoft.SemanticKernel;

namespace LegalAgent.Faq.Tests;

/// <summary>The first failure ends the generation; nothing is retried (FR-424).</summary>
public sealed class FaqGeneratorErrorTests
{
    private readonly FakeChatCompletionService chat = new();

    [Fact]
    public async Task ServiceErrorAtD3_StopsWithoutFurtherRequests()
    {
        chat.Respond(FaqJson.Candidates("D1", 2))
            .Respond(FaqJson.Candidates("D2", 2))
            .Fail(new HttpOperationException(HttpStatusCode.TooManyRequests, "{}", "Too Many Requests", null));

        FaqServiceException e = await Assert.ThrowsAsync<FaqServiceException>(() => Generate());

        Assert.Equal(FaqServiceErrorKind.RateLimited, e.Kind);
        Assert.Equal("D3", e.DocumentId);
        Assert.Equal(FaqStep.Candidates, e.Step);
        Assert.Equal(3, chat.Calls.Count);
    }

    [Fact]
    public async Task ServiceErrorAtSelection_ReportsSelectionStep()
    {
        for (int i = 1; i <= 5; i++)
        {
            chat.Respond(FaqJson.Candidates(Invariant($"D{i}"), 2));
        }

        chat.Fail(new HttpRequestException("brak sieci"));

        FaqServiceException e = await Assert.ThrowsAsync<FaqServiceException>(() => Generate());

        Assert.Equal((FaqServiceErrorKind.Network, FaqStep.Selection, null), (e.Kind, e.Step, e.DocumentId));
        Assert.Equal(6, chat.Calls.Count);
    }

    [Fact]
    public async Task RejectedCandidates_StopsWithProblems()
    {
        chat.Respond(FaqJson.Candidates("D1", 2)).Respond("{\"candidates\":[]}");

        FaqResponseException e = await Assert.ThrowsAsync<FaqResponseException>(() => Generate());

        Assert.Equal((FaqStep.Candidates, "D2"), (e.Step, e.DocumentId));
        Assert.Equal(["liczba kandydatów 0 poza zakresem 1–10"], e.Problems);
        Assert.Equal(2, chat.Calls.Count);
    }

    [Fact]
    public async Task InvalidJson_StopsWithProblem()
    {
        chat.Respond("Oto pytania: …");

        FaqResponseException e = await Assert.ThrowsAsync<FaqResponseException>(() => Generate());

        Assert.Equal(["odpowiedź nie jest poprawnym JSON-em zgodnym ze schematem"], e.Problems);
        Assert.Single(chat.Calls);
    }

    [Fact]
    public async Task RejectedSelection_ReportsAllProblems()
    {
        for (int i = 1; i <= 5; i++)
        {
            chat.Respond(FaqJson.Candidates(Invariant($"D{i}"), 2));
        }

        chat.Respond(FaqJson.Selection([.. FaqJson.SelectionItems(7, 5), new ItemJson("Skąd?", "Z D9.", ["D9-K1"])]));

        FaqResponseException e = await Assert.ThrowsAsync<FaqResponseException>(() => Generate());

        Assert.Equal(FaqStep.Selection, e.Step);
        Assert.Equal(
            ["liczba pozycji 8 zamiast 10", "pozycja 8: kandydat D9-K1 nie istnieje"],
            e.Problems);
        Assert.Equal(6, chat.Calls.Count);
    }

    private Task<FaqResult> Generate() =>
        new FaqGenerator(chat, new FaqGeneratorOptions(), (_, _) => new PromptExecutionSettings())
            .GenerateAsync(Documents(), cancellationToken: TestContext.Current.CancellationToken);

    private static FaqDocumentInput[] Documents() =>
    [
        .. Enumerable.Range(1, 5).Select(i => new FaqDocumentInput(
            Invariant($"Regulamin {i}"),
            new Uri(Invariant($"https://example.test/{i}.pdf")),
            Invariant($"# Regulamin {i}\n\nTreść dokumentu testowego.\n"),
            [])),
    ];

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
