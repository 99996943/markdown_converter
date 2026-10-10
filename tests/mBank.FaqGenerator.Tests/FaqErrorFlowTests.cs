using System.Net;
using LegalAgent.Faq.Tests.Fakes;
using MBank.FaqGenerator.Tests.Fakes;
using Microsoft.SemanticKernel;

namespace MBank.FaqGenerator.Tests;

/// <summary>Errors of the FAQ stage: messages and exit codes of contracts/cli.md; the previous FAQ stays (US3).</summary>
public sealed class FaqErrorFlowTests : IDisposable
{
    private const string PreviousFaq = "---\ntype: faq\n---\n\n## Poprzednie FAQ\n";

    private static readonly string[] Urls = [.. Enumerable.Range(1, 5).Select(i => $"https://www.example.test/pdf/reg-{i}.pdf")];

    private readonly AppHarness app = new();

    public FaqErrorFlowTests()
    {
        app.ServeRegulations(Urls);
        Directory.CreateDirectory(app.FaqDirectory);
        File.WriteAllText(app.FaqFile, PreviousFaq);
    }

    public void Dispose() => app.Dispose();

    [Fact]
    public async Task Authentication_ExitCode6()
    {
        app.Model.Fail(Http(HttpStatusCode.Unauthorized));

        await AssertFailsAsync(6, "Usługa Azure OpenAI odrzuciła klucz (401): klucz jest nieprawidłowy lub nie ma dostępu do zasobu.");
    }

    [Fact]
    public async Task DeploymentNotFound_ExitCode6_WithDeploymentAndEndpoint()
    {
        app.Model.Fail(Http(HttpStatusCode.NotFound));

        await AssertFailsAsync(6, $"Nie znaleziono wdrożenia „gpt-4o-mini” w zasobie {AppHarness.Endpoint} (404).");
    }

    [Fact]
    public async Task RateLimited_ExitCode6_WithDocument()
    {
        app.Model.Respond(FaqJson.Candidates("D1", 3)).Respond(FaqJson.Candidates("D2", 3)).Fail(Http(HttpStatusCode.TooManyRequests));

        await AssertFailsAsync(
            6,
            "Przekroczono limit zapytań wdrożenia (429) przy dokumencie D3. Spróbuj później lub zwiększ przepustowość wdrożenia.");
    }

    [Fact]
    public async Task ContentFiltered_ExitCode6_WithDocument()
    {
        app.Model.Respond(FaqJson.Candidates("D1", 3)).Fail(Http(HttpStatusCode.BadRequest, "{\"error\":{\"code\":\"content_filter\"}}"));

        await AssertFailsAsync(6, "Usługa zablokowała zapytanie filtrem treści (D2).");
    }

    [Fact]
    public async Task Timeout_ExitCode6_WithSecondsAndStep()
    {
        for (int i = 1; i <= 5; i++)
        {
            app.Model.Respond(FaqJson.Candidates($"D{i}", 3));
        }

        app.Model.Fail(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));

        await AssertFailsAsync(6, "Brak odpowiedzi usługi w ciągu 30 s (krok wyboru).");
    }

    [Fact]
    public async Task Network_ExitCode6_WithCause()
    {
        app.Model.Fail(new HttpOperationException(null, null, "Connection failed", new HttpRequestException("Nie można rozpoznać nazwy hosta faq.example.test")));

        await AssertFailsAsync(6, "Błąd połączenia z usługą Azure OpenAI: Nie można rozpoznać nazwy hosta faq.example.test.");
    }

    [Fact]
    public async Task DocumentTooLong_ExitCode6_WithSizes()
    {
        app.WriteSettings(new Dictionary<string, object?>(), faq: new Dictionary<string, object?> { ["MaxDocumentTokens"] = 10 });

        AppRun run = await AssertFailsAsync(6, "Dokument reg-1.md jest za długi dla modelu: ");

        Assert.Matches(@"za długi dla modelu: [\d  ]+ znak(i|ów)? \(~[\d  ]+ tokenów\), limit 10 tokenów \(Faq:MaxDocumentTokens\)\.", run.Err);
        Assert.Equal(0, app.Keys.Reads);
    }

    [Fact]
    public async Task RejectedSelection_ExitCode7_WithProblems()
    {
        for (int i = 1; i <= 5; i++)
        {
            app.Model.Respond(FaqJson.Candidates($"D{i}", 3));
        }

        app.Model.Respond(FaqJson.Selection(9, 5)).Respond(FaqJson.Selection(8, 5));

        AppRun run = await AssertFailsAsync(7, "Odpowiedź modelu odrzucona (krok wyboru):");

        Assert.Contains("[wybór] odpowiedź odrzucona (liczba poprawnych pozycji 9, potrzeba co najmniej 10) — prośba o poprawkę…", run.Out, StringComparison.Ordinal);
        Assert.Contains("Odpowiedź modelu odrzucona (krok wyboru):\n  - liczba poprawnych pozycji 8, potrzeba co najmniej 10\n", run.Err.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectedSelection_CorrectedOnce_ExitCode0()
    {
        for (int i = 1; i <= 5; i++)
        {
            app.Model.Respond(FaqJson.Candidates($"D{i}", 3));
        }

        app.Model.Respond(FaqJson.Selection(9, 5)).Respond(FaqJson.Selection(10, 5));

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(0, run.Code);
        Assert.Contains("[wybór] odpowiedź odrzucona (liczba poprawnych pozycji 9, potrzeba co najmniej 10) — prośba o poprawkę…\n[wybór] 10 pytań", run.Out.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkippedSelectionItem_IsPrinted()
    {
        for (int i = 1; i <= 5; i++)
        {
            app.Model.Respond(FaqJson.Candidates($"D{i}", 3));
        }

        app.Model.Respond(FaqJson.Selection([.. FaqJson.SelectionItems(10, 5), new ItemJson("Pytanie dodatkowe?", "Masz na to 14 dni.", ["D1-K3"])]));

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(0, run.Code);
        Assert.Contains(
            "[wybór] pominięto pozycję 11: liczba „14” nie występuje w kandydatach basedOn\n[wybór] 10 pytań",
            run.Out.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectedCandidates_ExitCode7_WithDocument()
    {
        app.Model.Respond("nie JSON");

        await AssertFailsAsync(7, "Odpowiedź modelu odrzucona (krok kandydatów, D1):\n  - odpowiedź nie jest poprawnym JSON-em zgodnym ze schematem");
    }

    [Fact]
    public async Task FaqDirectoryIsAFile_ExitCode4()
    {
        string file = app.Root.Combine("plik-zamiast-katalogu");
        await File.WriteAllTextAsync(file, "x", Ct);
        app.WriteSettings(new Dictionary<string, object?>(), faq: new Dictionary<string, object?> { ["OutputDirectory"] = file });

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(4, run.Code);
        Assert.Contains($"Nie można zapisać FAQ_mBank.md w {file}: ", run.Err, StringComparison.Ordinal);
        Assert.Equal(PreviousFaq, await File.ReadAllTextAsync(app.FaqFile, Ct));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public async Task CancelledDuringRequest_ExitCode130_PreviousFaqIntact(int request)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        for (int i = 1; i < request; i++)
        {
            app.Model.Respond(FaqJson.Candidates($"D{i}", 3));
        }

        app.Model.Hang(started);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        Task<AppRun> running = app.RunAsync(UrlArgs(), "", cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        await cancellation.CancelAsync();
        AppRun run = await running;

        Assert.Equal(130, run.Code);
        Assert.Contains("Przerwano.", run.Err, StringComparison.Ordinal);
        Assert.Equal(request, app.Model.Calls.Count);
        Assert.Equal(PreviousFaq, await File.ReadAllTextAsync(app.FaqFile, Ct));
        Assert.False(File.Exists(app.FaqFile + ".tmp"));
    }

    private async Task<AppRun> AssertFailsAsync(int code, string message)
    {
        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(code, run.Code);
        Assert.Contains(message, run.Err.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("Błąd nieoczekiwany", run.Err, StringComparison.Ordinal);
        Assert.Equal(PreviousFaq, await File.ReadAllTextAsync(app.FaqFile, Ct));
        Assert.False(File.Exists(app.FaqFile + ".tmp"));
        return run;
    }

    private static HttpOperationException Http(HttpStatusCode status, string body = "{}") => new(status, body, "błąd usługi", null);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] UrlArgs() => [.. Urls.SelectMany(u => new[] { "--url", u })];
}
