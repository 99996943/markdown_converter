using System.Net;
using MBank.FaqGenerator.Tests.Fakes;

namespace MBank.FaqGenerator.Tests;

public sealed class ErrorReportingTests : IDisposable
{
    private static readonly string[] Urls = [.. Enumerable.Range(1, 5).Select(i => $"https://www.example.test/pdf/reg-{i}.pdf")];

    private readonly AppHarness app = new();

    public void Dispose() => app.Dispose();

    [Fact]
    public async Task SomeLinksFail_OthersDownloaded_ReasonsReported_ExitCode3()
    {
        app.Http.Pdf(Urls[0]).Status(Urls[1], HttpStatusCode.NotFound).Pdf(Urls[2]).Hang(Urls[3]).Pdf(Urls[4]);
        app.Environment["FAQGEN__Download__TimeoutSeconds"] = "0.2";

        AppRun run = await app.RunAsync([], Input, Ct);

        Assert.Equal(3, run.Code);
        string output = run.Out.ReplaceLineEndings("\n");
        Assert.Contains($"[2/5] błąd: {Urls[1]} — serwer zwrócił 404 Not Found", output, StringComparison.Ordinal);
        Assert.Contains($"[4/5] błąd: {Urls[3]} — przekroczono limit czasu 0,2 s", output, StringComparison.Ordinal);
        Assert.Contains($"Pobrano 3 z 5 plików do {app.OutputDirectory}:", output, StringComparison.Ordinal);
        Assert.Contains($"  2. BŁĄD {Urls[1]} — serwer zwrócił 404 Not Found\n", output, StringComparison.Ordinal);
        Assert.Contains($"  4. BŁĄD {Urls[3]} — przekroczono limit czasu 0,2 s\n", output, StringComparison.Ordinal);
        Assert.Contains("  3. reg-3.pdf — ", output, StringComparison.Ordinal);
        Assert.Contains(Urls[3], run.Err, StringComparison.Ordinal);
        Assert.Equal(
            ["manifest.json", "reg-1.pdf", "reg-3.pdf", "reg-5.pdf"],
            app.Root.FileNames("downloads"));
    }

    [Fact]
    public async Task AllLinksFail_ExitCode3()
    {
        AppRun run = await app.RunAsync([], Input, Ct);

        Assert.Equal(3, run.Code);
        Assert.Contains("Pobrano 0 z 5 plików", run.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnexpectedException_IsReported_ExitCode1()
    {
        app.Http.Pdf(Urls[0]).Throw(Urls[1], new InvalidOperationException("awaria atrapy"));

        AppRun run = await app.RunAsync([], Input, Ct);

        Assert.Equal(1, run.Code);
        Assert.Contains("Błąd nieoczekiwany", run.Err, StringComparison.Ordinal);
        Assert.Contains("awaria atrapy", run.Err, StringComparison.Ordinal);
    }

    private static string Input => string.Join("\n", Urls) + "\n";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
