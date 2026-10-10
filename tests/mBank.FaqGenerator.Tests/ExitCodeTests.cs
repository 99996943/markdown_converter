using LegalAgent.Downloads.Tests.Fakes;
using MBank.FaqGenerator.Tests.Fakes;

namespace MBank.FaqGenerator.Tests;

public sealed class ExitCodeTests : IDisposable
{
    private static readonly string[] Urls = [.. Enumerable.Range(1, 5).Select(i => $"https://www.example.test/pdf/reg-{i}.pdf")];

    private readonly AppHarness app = new();

    public ExitCodeTests()
    {
        foreach (string url in Urls)
        {
            app.Http.Pdf(url);
        }
    }

    public void Dispose() => app.Dispose();

    [Theory]
    [InlineData("")]
    [InlineData("https://www.example.test/pdf/reg-1.pdf\nhttps://www.example.test/pdf/reg-2.pdf\n")]
    public async Task InputEndsBeforeFiveAddresses_ExitCode2_WithHint(string stdin)
    {
        AppRun run = await app.RunAsync([], stdin, Ct);

        Assert.Equal(2, run.Code);
        Assert.Contains(
            "Brak adresów: wejście zostało zamknięte. Podaj 5 adresów opcją --url albo w konfiguracji (Download:Urls).",
            run.Err,
            StringComparison.Ordinal);
        Assert.Empty(app.Http.Requests);
    }

    [Fact]
    public async Task CancelledWhilePrompting_ExitCode130()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        AppRun run = await app.RunAsync([], string.Join("\n", Urls) + "\n", cancellation.Token);

        Assert.Equal(130, run.Code);
        Assert.Contains("Przerwano", run.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelledWhileDownloading_ExitCode130_ManifestWritten()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        app.Http.Route(Urls[4], async ct =>
        {
            await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });

        AppRun run = await app.RunAsync(UrlArgs(), "", cancellation.Token);

        Assert.Equal(130, run.Code);
        Assert.True(File.Exists(Path.Combine(app.OutputDirectory, "manifest.json")));
        Assert.Empty(Directory.GetFiles(app.OutputDirectory, "*.part"));
    }

    [Theory]
    [InlineData("FAQGEN__Download__TimeoutSeconds", "0")]
    [InlineData("FAQGEN__Download__TimeoutSeconds", "dużo")]
    [InlineData("FAQGEN__Download__MaxRedirects", "50")]
    public async Task InvalidConfiguration_ExitCode2(string key, string value)
    {
        app.Environment[key] = value;

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(2, run.Code);
        Assert.StartsWith("Błąd konfiguracji: ", run.Err, StringComparison.Ordinal);
        Assert.Empty(app.Http.Requests);
    }

    [Fact]
    public async Task NoAllowedHosts_ExitCode2()
    {
        app.WriteSettings(new Dictionary<string, object?> { ["AllowedHosts"] = Array.Empty<string>() });

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(2, run.Code);
        Assert.StartsWith("Błąd konfiguracji: ", run.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutputDirectoryUnusable_ExitCode4()
    {
        string file = app.Root.Combine("plik");
        await File.WriteAllTextAsync(file, "x", Ct);

        AppRun run = await app.RunAsync([.. UrlArgs(), "--output", file], "", Ct);

        Assert.Equal(4, run.Code);
        Assert.Contains("katalogu pobrań", run.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullSuccess_ListsRemovedFiles()
    {
        Directory.CreateDirectory(app.OutputDirectory);
        await File.WriteAllBytesAsync(Path.Combine(app.OutputDirectory, "stary.pdf"), PdfBytes.Sample("x"), Ct);

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(0, run.Code);
        Assert.Contains("Usunięto pliki spoza bieżącej listy: stary.pdf", run.Out, StringComparison.Ordinal);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] UrlArgs() => [.. Urls.SelectMany(u => new[] { "--url", u })];
}
