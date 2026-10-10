using System.Globalization;
using LegalAgent.Faq.Model;
using LegalAgent.Faq.Tests.Fakes;
using Microsoft.SemanticKernel;

namespace LegalAgent.Faq.Tests;

public sealed class FaqGeneratorTests
{
    private readonly FakeChatCompletionService chat = new();
    private readonly List<(FaqStep Step, string Schema, PromptExecutionSettings Settings)> settingsCalls = [];

    [Fact]
    public async Task Generate_RequestsCandidatesInTurnThenSelection()
    {
        ScriptValidRun();

        await Generator().GenerateAsync(Documents(), cancellationToken: Ct);

        Assert.Equal(6, chat.Calls.Count);
        Assert.Equal(1, chat.MaxConcurrency);
        for (int i = 1; i <= 5; i++)
        {
            string user = chat.Calls[i - 1].User;
            Assert.Contains(Invariant($"Dokument D{i}: Regulamin {i}\n"), user, StringComparison.Ordinal);
            Assert.Contains(Invariant($"Źródło: https://example.test/pdf/reg-{i}.pdf\n"), user, StringComparison.Ordinal);
            Assert.Contains(Markdown(i), user, StringComparison.Ordinal);
            Assert.Contains(Invariant($"\nJednostki dokumentu D{i} ("), user, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Generate_SelectionMessageListsDocumentsAndCandidatesWithoutDocumentText()
    {
        chat.Respond(FaqJson.Candidates(new CandidateJson("Ile kosztuje karta?", "Dziesięć złotych.", "§ 1"), new CandidateJson("Czy jest limit?", "Nie.")));
        for (int i = 2; i <= 5; i++)
        {
            chat.Respond(FaqJson.Candidates(Invariant($"D{i}"), 2));
        }

        chat.Respond(FaqJson.Selection(10, 5));

        await Generator().GenerateAsync(Documents(), cancellationToken: Ct);

        string user = chat.Calls[5].User;
        Assert.StartsWith(
            "Dokumenty:\nD1: Regulamin 1 — https://example.test/pdf/reg-1.pdf\nD2: Regulamin 2 — https://example.test/pdf/reg-2.pdf\n",
            user,
            StringComparison.Ordinal);
        Assert.Contains("\n\nKandydaci:\n[D1-K1] (D1, § 1) Pytanie: Ile kosztuje karta? | Odpowiedź: Dziesięć złotych.\n", user, StringComparison.Ordinal);
        Assert.Contains("[D1-K2] (D1) Pytanie: Czy jest limit? | Odpowiedź: Nie.\n", user, StringComparison.Ordinal);
        Assert.Contains("[D5-K2] (D5) Pytanie: Pytanie 2 o dokument D5?", user, StringComparison.Ordinal);
        Assert.DoesNotContain("Treść dokumentu", user, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_PassesSettingsFromFactory()
    {
        ScriptValidRun();

        await Generator().GenerateAsync(Documents(), cancellationToken: Ct);

        Assert.Equal(6, settingsCalls.Count);
        Assert.All(settingsCalls.Take(5), c => Assert.Equal((FaqStep.Candidates, FaqSchemas.Candidates), (c.Step, c.Schema)));
        Assert.Equal((FaqStep.Selection, FaqSchemas.Selection), (settingsCalls[5].Step, settingsCalls[5].Schema));
        for (int i = 0; i < 6; i++)
        {
            Assert.Same(settingsCalls[i].Settings, chat.Calls[i].Settings);
        }
    }

    [Fact]
    public async Task Generate_ReturnsItemsDocumentsAndCandidates()
    {
        ScriptValidRun();

        FaqResult result = await Generator().GenerateAsync(Documents(), cancellationToken: Ct);

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(Enumerable.Range(1, 10), result.Items.Select(i => i.Number));
        Assert.Equal("Pytanie końcowe 1?", result.Items[0].Question);
        Assert.Equal([new FaqSource("D1", null)], result.Items[0].Sources);
        Assert.Equal(["D1", "D2", "D3", "D4", "D5"], result.Documents.Select(d => d.Id));
        Assert.Equal(new FaqSourceDocument("D3", "Regulamin 3", new Uri("https://example.test/pdf/reg-3.pdf")), result.Documents[2]);
        Assert.Equal(15, result.Candidates.Count);
        Assert.Equal("D2-K3", result.Candidates[5].Id);
    }

    [Fact]
    public async Task Generate_ReportsProgressInOrder()
    {
        ScriptValidRun();
        var events = new List<FaqEvent>();

        await Generator().GenerateAsync(Documents(), new SyncProgress(events), Ct);

        Assert.Equal(
            [
                FaqEventKind.CandidatesStarted, FaqEventKind.CandidatesFinished,
                FaqEventKind.CandidatesStarted, FaqEventKind.CandidatesFinished,
                FaqEventKind.CandidatesStarted, FaqEventKind.CandidatesFinished,
                FaqEventKind.CandidatesStarted, FaqEventKind.CandidatesFinished,
                FaqEventKind.CandidatesStarted, FaqEventKind.CandidatesFinished,
                FaqEventKind.SelectionStarted, FaqEventKind.SelectionFinished,
            ],
            events.Select(e => e.Kind));
        FaqEvent first = events[0];
        Assert.Equal("D1", first.DocumentId);
        Assert.Equal(chat.Calls[0].User.Length, first.Characters);
        Assert.Equal((int)Math.Ceiling(first.Characters / 3.0), first.EstimatedTokens);
        Assert.Equal(("D1", 3), (events[1].DocumentId, events[1].Count));
        FaqEvent selection = events[10];
        Assert.Null(selection.DocumentId);
        Assert.Equal(15, selection.Count);
        Assert.Equal(chat.Calls[5].User.Length, selection.Characters);
        Assert.Equal(10, events[11].Count);
    }

    [Fact]
    public async Task Generate_ChecksInputBeforeFirstRequest()
    {
        ScriptValidRun();
        var generator = new FaqGenerator(chat, new FaqGeneratorOptions { MaxDocumentTokens = 10 }, Settings);

        await Assert.ThrowsAsync<FaqInputTooLongException>(() => generator.GenerateAsync(Documents(), cancellationToken: Ct));

        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task Generate_CancelledDuringRequest_Throws()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        chat.Respond(FaqJson.Candidates("D1", 3)).Hang(started);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        Task<FaqResult> run = Generator().GenerateAsync(Documents(), cancellationToken: cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(2, chat.Calls.Count);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private FaqGenerator Generator() => new(chat, new FaqGeneratorOptions(), Settings);

    private PromptExecutionSettings Settings(FaqStep step, string schema)
    {
        var settings = new PromptExecutionSettings { ModelId = Invariant($"{step}-{settingsCalls.Count}") };
        settingsCalls.Add((step, schema, settings));
        return settings;
    }

    private void ScriptValidRun()
    {
        for (int i = 1; i <= 5; i++)
        {
            chat.Respond(FaqJson.Candidates(Invariant($"D{i}"), 3));
        }

        chat.Respond(FaqJson.Selection(10, 5));
    }

    [Fact]
    public async Task Generate_DropsUngroundedCandidate_AndReportsIt()
    {
        chat.Respond(FaqJson.Candidates(
            FaqJson.Candidate("D1", 1),
            FaqJson.Candidate("D1", 2),
            new CandidateJson("Ile kosztuje karta?", "Karta kosztuje 99 zł.", "§ 1")));
        for (int i = 2; i <= 5; i++)
        {
            chat.Respond(FaqJson.Candidates(Invariant($"D{i}"), 2));
        }

        chat.Respond(FaqJson.Selection(10, 5));
        var events = new List<FaqEvent>();

        FaqResult result = await Generator().GenerateAsync(Documents(), new SyncProgress(events), Ct);

        Assert.DoesNotContain(result.Candidates, c => c.Id == "D1-K3");
        FaqEvent dropped = Assert.Single(events, e => e.Kind == FaqEventKind.CandidateDropped);
        Assert.Equal("D1", dropped.DocumentId);
        Assert.Equal("kandydat D1-K3: liczba „99” nie występuje w jednostce „§ 1”", dropped.Detail);
        Assert.Equal(2, events.First(e => e.Kind == FaqEventKind.CandidatesFinished).Count);
    }

    [Fact]
    public async Task Generate_AllCandidatesOfDocumentDropped_RejectsResponse()
    {
        chat.Respond(FaqJson.Candidates(new CandidateJson("Zmyślone?", "Tak.", "", "tego zdania nie ma w dokumencie")));

        FaqResponseException e = await Assert.ThrowsAsync<FaqResponseException>(() => Generator().GenerateAsync(Documents(), cancellationToken: Ct));

        Assert.Equal(FaqStep.Candidates, e.Step);
        Assert.Equal("D1", e.DocumentId);
        Assert.Equal(["kandydat D1-K1: cytat „tego zdania nie ma w dokumencie” nie występuje w dokumencie D1"], e.Problems);
        Assert.Single(chat.Calls);
    }

    private static string Markdown(int i) =>
        Invariant($"# Regulamin {i}\n\n<!-- page: 1 -->\n## § 1.\n\nTreść dokumentu {i}.\n");

    private static FaqDocumentInput[] Documents() =>
    [
        .. Enumerable.Range(1, 5).Select(i => new FaqDocumentInput(
            Invariant($"Regulamin {i}"),
            new Uri(Invariant($"https://example.test/pdf/reg-{i}.pdf")),
            Markdown(i),
            ["§ 1", "§ 1."])),
    ];

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private sealed class SyncProgress(List<FaqEvent> events) : IProgress<FaqEvent>
    {
        public void Report(FaqEvent value) => events.Add(value);
    }
}
