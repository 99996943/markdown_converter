using System.Globalization;
using System.Text.RegularExpressions;
using LegalAgent.Faq;
using LegalAgent.Faq.Model;
using LegalAgent.Faq.Tests.Fakes;
using MBank.FaqGenerator.Tests.Fakes;

namespace MBank.FaqGenerator.Tests;

/// <summary>The whole run: download, conversion, FAQ (US1).</summary>
public sealed class FaqFlowTests : IDisposable
{
    private static readonly string[] Urls = [.. Enumerable.Range(1, 5).Select(i => $"https://www.example.test/pdf/reg-{i}.pdf")];

    private readonly AppHarness app = new();

    public FaqFlowTests()
    {
        app.ServeRegulations(Urls, warningAt: 2);
    }

    public void Dispose() => app.Dispose();

    [Fact]
    public async Task FullRun_ConvertsAndWritesFaq_ExitCode0()
    {
        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(0, run.Code);
        for (int i = 1; i <= 5; i++)
        {
            Assert.True(File.Exists(Path.Combine(app.OutputDirectory, Invariant($"reg-{i}.md"))));
        }

        Assert.Equal(ExpectedFaq(), await File.ReadAllTextAsync(app.FaqFile, Ct));
        Assert.Equal("test-key", app.Model.ReceivedKey);
        Assert.Equal(6, app.Model.Calls.Count);
    }

    [Fact]
    public async Task FullRun_FaqHeaderFromApplication()
    {
        await app.RunAsync(UrlArgs(), "", Ct);

        string faq = await File.ReadAllTextAsync(app.FaqFile, Ct);
        Assert.StartsWith(
            "---\ntype: faq\ntitle: \"FAQ — regulaminy mBanku\"\n"
            + "description: \"10 najważniejszych pytań i odpowiedzi na podstawie 5 regulaminów mBanku.\"\n"
            + "resource:\n" + string.Concat(Urls.Select(u => $"  - \"{u}\"\n"))
            + "timestamp: \"2026-10-09T12:00:00Z\"\nmodel: \"gpt-4o-mini\"\ndeployment: \"gpt-4o-mini\"\n---\n",
            faq,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullRun_PrintsConversionAndFaqProgress()
    {
        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);
        string output = run.Out.ReplaceLineEndings("\n");

        Assert.Contains(
            "\nKonwersja do Markdown:\n[1/5] reg-1.pdf → reg-1.md (1 strona)\n"
            + "[2/5] reg-2.pdf → reg-2.md (1 strona, 1 ostrzeżenie)\n"
            + "      ostrzeżenie IMG001_ImagesIgnored (strona 1): Obrazy na stronie 1 zostały pominięte.\n"
            + "[3/5] reg-3.pdf → reg-3.md (1 strona)\n",
            output,
            StringComparison.Ordinal);
        Assert.Contains("[5/5] reg-5.pdf → reg-5.md (1 strona)\nPrzekonwertowano 5 z 5 plików.\n", output, StringComparison.Ordinal);
        Assert.Contains("Klucz API Azure OpenAI: ********\n", output, StringComparison.Ordinal);
        Assert.Contains("\nGenerowanie FAQ (gpt-4o-mini, wdrożenie gpt-4o-mini):\n[D1] kandydaci z reg-1.md — ", output, StringComparison.Ordinal);
        Assert.Matches(@"\[D1\] kandydaci z reg-1\.md — [\d  ]+ znak(i|ów)? \(~[\d  ]+ tokenów\)…\n\[D1\] 3 kandydatów \(wejście 100, wyjście 10 tokenów\)\n", output);
        Assert.Matches(@"\[wybór\] 15 kandydatów — [\d  ]+ znak(i|ów)? \(~[\d  ]+ tokenów\)…\n\[wybór\] 10 pytań \(wejście 100, wyjście 10 tokenów\)\n", output);
        Assert.Contains($"\nZapisano FAQ: {app.FaqFile} (10 pytań; łącznie wejście 600, wyjście 60 tokenów)\n", output, StringComparison.Ordinal);
        Assert.True(
            output.IndexOf("Przekonwertowano 5 z 5", StringComparison.Ordinal) > output.IndexOf("Manifest:", StringComparison.Ordinal),
            "conversion follows the download summary");
    }

    [Fact]
    public async Task ConversionErrors_ExitCode5_OthersConverted_NoKeyNoRequests()
    {
        app.Http.Pdf(Urls[1], TestPdfs.Corrupted()).Pdf(Urls[3], TestPdfs.NoText());

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(5, run.Code);
        Assert.Contains("Błąd konwersji reg-2.pdf: ", run.Err, StringComparison.Ordinal);
        Assert.Contains("Błąd konwersji reg-4.pdf: ", run.Err, StringComparison.Ordinal);
        Assert.Contains("Przekonwertowano 3 z 5 plików.", run.Out, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(app.OutputDirectory, "reg-5.md")));
        Assert.Equal(0, app.Keys.Reads);
        Assert.Empty(app.Model.Calls);
        Assert.False(File.Exists(app.FaqFile));
    }

    [Fact]
    public async Task StaleMarkdown_IsRemoved_AndListed()
    {
        Directory.CreateDirectory(app.OutputDirectory);
        await File.WriteAllTextAsync(Path.Combine(app.OutputDirectory, "stary.md"), "x", Ct);

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(0, run.Code);
        Assert.False(File.Exists(Path.Combine(app.OutputDirectory, "stary.md")));
        Assert.Contains(
            "Przekonwertowano 5 z 5 plików.\nUsunięto pliki Markdown spoza bieżącej listy: stary.md\n",
            run.Out.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DroppedCandidate_IsPrinted()
    {
        app.Model.Respond(FaqJson.Candidates(
            FaqJson.Candidate("D1", 1),
            FaqJson.Candidate("D1", 2),
            FaqJson.Candidate("D1", 3),
            new CandidateJson("Zmyślone?", "Tak.", "", "tego zdania nie ma w dokumencie")));

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(0, run.Code);
        Assert.Contains(
            "[D1] pominięto kandydata D1-K4: cytat „tego zdania nie ma w dokumencie” nie występuje w dokumencie D1\n[D1] 3 kandydatów",
            run.Out.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownUsage_IsReportedAsUnknown()
    {
        app.Model.FallbackUsage = null;

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(0, run.Code);
        Assert.Contains("[D1] 3 kandydatów (zużycie tokenów nieznane)", run.Out, StringComparison.Ordinal);
        Assert.Contains("(10 pytań; zużycie tokenów nieznane)", run.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FaqOutputOption_ChangesDirectory_AndCreatesIt()
    {
        string directory = app.Root.Combine(Path.Combine("inny", "katalog"));

        AppRun run = await app.RunAsync([.. UrlArgs(), "--faq-output", directory], "", Ct);

        Assert.Equal(0, run.Code);
        Assert.True(File.Exists(Path.Combine(directory, "FAQ_mBank.md")));
        Assert.False(File.Exists(app.FaqFile));
    }

    [Fact]
    public async Task FaqOutputOption_WithoutValue_ExitCode2()
    {
        AppRun run = await app.RunAsync([.. UrlArgs(), "--faq-output"], "", Ct);

        Assert.Equal(2, run.Code);
        Assert.Contains("--faq-output", run.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullRun_SourceNameLinksToPageOfUnit()
    {
        ServeOnPages();

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(0, run.Code);
        string faq = await File.ReadAllTextAsync(app.FaqFile, Ct);
        string[] sources = [.. faq.Split('\n').Where(l => l.StartsWith("Źród", StringComparison.Ordinal))];
        Assert.Equal(10, sources.Length);
        Assert.All(sources, line => Assert.Matches(@"^Źródło: \[Regulamin \d\]\(https://www\.example\.test/pdf/reg-\d\.pdf#page=3\), ", line));
        Assert.DoesNotContain("#page", faq[..faq.IndexOf("\n---\n", StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullRun_UnitLinksToHeadingInDownloadedMarkdown()
    {
        ServeOnPages();

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(0, run.Code);
        string[] sources = await SourceLinesAsync(app.FaqFile);
        Assert.Equal(10, sources.Length);
        foreach (string line in sources)
        {
            Match match = Regex.Match(line, @"^Źródło: \[Regulamin (\d)\]\([^)]*#page=3\), \[§ 3\]\(\.\./downloads/(reg-\d\.md)#([^)]+)\)$");
            Assert.True(match.Success, line);
            Assert.Equal(Invariant($"reg-{match.Groups[1].Value}.md"), match.Groups[2].Value);
            string markdown = await File.ReadAllTextAsync(Path.Combine(app.OutputDirectory, match.Groups[2].Value), Ct);
            Assert.Contains(match.Groups[3].Value, MarkdownAnchors.ForDocument(markdown));
        }
    }

    [Fact]
    public async Task FullRun_FaqElsewhere_RelativePathToMarkdown()
    {
        ServeOnPages();
        string directory = app.Root.Combine(Path.Combine("wyniki", "faq"));

        AppRun run = await app.RunAsync([.. UrlArgs(), "--faq-output", directory], "", Ct);

        Assert.Equal(0, run.Code);
        string[] sources = await SourceLinesAsync(Path.Combine(directory, "FAQ_mBank.md"));
        Assert.All(sources, line => Assert.Contains("](../../downloads/reg-", line, StringComparison.Ordinal));
    }

    private static async Task<string[]> SourceLinesAsync(string faqFile) =>
        [.. (await File.ReadAllTextAsync(faqFile, Ct)).Split('\n').Where(l => l.StartsWith("Źród", StringComparison.Ordinal))];

    /// <summary>Serves the three-page regulations and makes the model cite „§ 3” (page 3) with a quote from it.</summary>
    private void ServeOnPages()
    {
        for (int i = 0; i < Urls.Length; i++)
        {
            app.Http.Pdf(Urls[i], TestPdfs.RegulationOnPages(Urls[i], Invariant($"Regulamin {i + 1}")));
        }

        app.Model.Fallback = call =>
        {
            if (!call.User.StartsWith("Dokument D", StringComparison.Ordinal))
            {
                return FaqJson.ValidAnswer(call);
            }

            string id = call.User["Dokument ".Length..call.User.IndexOf(':', StringComparison.Ordinal)];
            string quote = LineAfterHeading(call.User, "§ 3.");
            return FaqJson.Candidates(
                new CandidateJson(Invariant($"Jak wypowiedzieć umowę {id}?"), "W każdym czasie.", "§ 3", quote),
                new CandidateJson(Invariant($"Kiedy można wypowiedzieć umowę {id}?"), "W każdym czasie.", "§ 3", quote));
        };
    }

    /// <summary>The first text line after the heading ending with <paramref name="heading"/>.</summary>
    private static string LineAfterHeading(string text, string heading)
    {
        string[] lines = text.Split('\n');
        int index = Array.FindIndex(lines, l => l.StartsWith('#') && l.EndsWith(" " + heading, StringComparison.Ordinal));
        return lines.Skip(index + 1).First(l => l.Length > 0 && !l.StartsWith("<!--", StringComparison.Ordinal));
    }

    private static string ExpectedFaq()
    {
        FaqSourceDocument[] documents =
        [
            .. Urls.Select((u, i) => new FaqSourceDocument(Invariant($"D{i + 1}"), Invariant($"Regulamin {i + 1}"), new Uri(u))),
        ];
        FaqItem[] items =
        [
            .. FaqJson.SelectionItems(10, 5).Select((item, i) => new FaqItem(
                i + 1,
                item.Question,
                item.Answer,
                [.. item.BasedOn.Select(id => new FaqSource(id[..id.IndexOf('-', StringComparison.Ordinal)], null))],
                item.BasedOn)),
        ];
        return FaqMarkdownRenderer.Render(
            new FaqResult(items, documents, [], null),
            new FaqFileHeader(
                "FAQ — regulaminy mBanku",
                "10 najważniejszych pytań i odpowiedzi na podstawie 5 regulaminów mBanku.",
                AppHarness.Now,
                "gpt-4o-mini",
                "gpt-4o-mini"));
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] UrlArgs() => [.. Urls.SelectMany(u => new[] { "--url", u })];
}
