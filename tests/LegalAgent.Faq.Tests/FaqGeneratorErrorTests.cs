using System.Globalization;
using System.Net;
using LegalAgent.Faq.Model;
using LegalAgent.Faq.Tests.Fakes;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

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

        string rejected = FaqJson.Selection([.. FaqJson.SelectionItems(7, 5), new ItemJson("Skąd?", "Z D9.", ["D9-K1"])]);
        chat.Respond(FaqJson.Selection(9, 5)).Respond(rejected);

        FaqResponseException e = await Assert.ThrowsAsync<FaqResponseException>(() => Generate());

        Assert.Equal(FaqStep.Selection, e.Step);
        Assert.Equal(
            ["liczba pozycji 8 zamiast 10", "pozycja 8: kandydat D9-K1 nie istnieje"],
            e.Problems);
        Assert.Equal(7, chat.Calls.Count);
    }

    [Fact]
    public async Task RejectedSelection_IsCorrectedOnce()
    {
        for (int i = 1; i <= 5; i++)
        {
            chat.Respond(FaqJson.Candidates(Invariant($"D{i}"), 2));
        }

        string rejected = FaqJson.Selection(9, 5);
        chat.Respond(rejected).Respond(FaqJson.Selection(10, 5));
        var events = new List<FaqEvent>();

        FaqResult result = await new FaqGenerator(chat, new FaqGeneratorOptions(), (_, _) => new PromptExecutionSettings())
            .GenerateAsync(Documents(), new SyncProgress(events), TestContext.Current.CancellationToken);

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(7, chat.Calls.Count);
        IReadOnlyList<ChatMessageContent> history = chat.Calls[6].History;
        Assert.Equal([AuthorRole.System, AuthorRole.User, AuthorRole.Assistant, AuthorRole.User], history.Select(m => m.Role));
        Assert.Equal(chat.Calls[5].System, history[0].Content);
        Assert.Equal(chat.Calls[5].User, history[1].Content);
        Assert.Equal(rejected, history[2].Content);
        Assert.Contains("- liczba pozycji 9 zamiast 10\n", history[3].Content, StringComparison.Ordinal);
        Assert.Contains("dokładnie 10", history[3].Content, StringComparison.Ordinal);
        FaqEvent correction = Assert.Single(events, e => e.Kind == FaqEventKind.SelectionCorrection);
        Assert.Equal("liczba pozycji 9 zamiast 10", correction.Detail);
        Assert.Equal(
            [FaqEventKind.SelectionStarted, FaqEventKind.SelectionCorrection, FaqEventKind.SelectionFinished],
            events.Skip(10).Select(e => e.Kind));
    }

    [Fact]
    public async Task UnbalancedSelection_IsCorrected()
    {
        for (int i = 1; i <= 5; i++)
        {
            chat.Respond(FaqJson.Candidates(Invariant($"D{i}"), 2));
        }

        int[] documentOf = [1, 1, 1, 1, 2, 2, 3, 3, 4, 4];
        ItemJson[] unbalanced =
        [
            .. documentOf.Select((d, i) => new ItemJson(
                Invariant($"Pytanie końcowe {i + 1}?"),
                "Odpowiedź końcowa.",
                [Invariant($"D{d}-K{(i % 2) + 1}")])),
        ];
        chat.Respond(FaqJson.Selection(unbalanced)).Respond(FaqJson.Selection(10, 5));
        var events = new List<FaqEvent>();

        FaqResult result = await new FaqGenerator(chat, new FaqGeneratorOptions(), (_, _) => new PromptExecutionSettings())
            .GenerateAsync(Documents(), new SyncProgress(events), TestContext.Current.CancellationToken);

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(
            "dokument D1: 4 pozycje (najwyżej 3); dokument D5: brak pozycji (co najmniej 1)",
            Assert.Single(events, e => e.Kind == FaqEventKind.SelectionCorrection).Detail);
    }

    [Fact]
    public async Task SelectionNotJson_IsCorrectedOnce()
    {
        for (int i = 1; i <= 5; i++)
        {
            chat.Respond(FaqJson.Candidates(Invariant($"D{i}"), 2));
        }

        chat.Respond("Oto pytania: …").Respond(FaqJson.Selection(10, 5));

        FaqResult result = await Generate();

        Assert.Equal(10, result.Items.Count);
        Assert.Contains("odpowiedź nie jest poprawnym JSON-em zgodnym ze schematem", chat.Calls[6].History[^1].Content, StringComparison.Ordinal);
    }

    private sealed class SyncProgress(List<FaqEvent> events) : IProgress<FaqEvent>
    {
        public void Report(FaqEvent value) => events.Add(value);
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
