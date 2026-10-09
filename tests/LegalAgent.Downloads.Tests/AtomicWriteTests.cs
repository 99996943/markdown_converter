using System.Net;
using LegalAgent.Downloads.Model;
using LegalAgent.Downloads.Tests.Fakes;

namespace LegalAgent.Downloads.Tests;

public sealed class AtomicWriteTests : IDisposable
{
    private const string Url = "https://www.example.test/a.pdf";
    private static readonly byte[] OldContent = PdfBytes.Sample("stara wersja");

    private readonly TempDirectory temp = new();
    private readonly FakeHttpHandler http = new();

    public AtomicWriteTests()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllBytes(Target, OldContent);
    }

    public void Dispose()
    {
        http.Dispose();
        temp.Dispose();
    }

    [Fact]
    public async Task ConnectionBrokenMidBody_FailsAndKeepsOldFile()
    {
        http.Body(Url, () => new ScriptedStream(PdfBytes.OfSize(200_000), failAfter: 100_000));

        DownloadResult result = await RunAsync();

        Assert.Equal(DownloadStatus.Failed, result.Status);
        Assert.Equal(DownloadErrorKind.Connection, result.Error?.Kind);
        AssertOldFileIntact();
    }

    [Fact]
    public async Task Timeout_KeepsOldFile()
    {
        http.Body(Url, () => new ScriptedStream(PdfBytes.OfSize(1000), failAfter: 500, hangAfter: true));

        DownloadResult result = await RunAsync(Options with { Timeout = TimeSpan.FromMilliseconds(200) });

        Assert.Equal(DownloadErrorKind.Timeout, result.Error?.Kind);
        AssertOldFileIntact();
    }

    [Fact]
    public async Task NotPdf_KeepsOldFile()
    {
        http.Html(Url);

        DownloadResult result = await RunAsync();

        Assert.Equal(DownloadErrorKind.NotPdf, result.Error?.Kind);
        AssertOldFileIntact();
    }

    [Fact]
    public async Task PartFileCannotBeCreated_IsWriteFailed_AndKeepsOldFile()
    {
        // A directory in place of the .part file makes the write fail on every system, also as root.
        Directory.CreateDirectory(Target + ".part");
        http.Pdf(Url);

        DownloadResult result = await RunAsync();

        Assert.Equal(DownloadStatus.Failed, result.Status);
        Assert.Equal(DownloadErrorKind.WriteFailed, result.Error?.Kind);
        Assert.StartsWith("nie można zapisać pliku", result.Error?.Message, StringComparison.Ordinal);
        Assert.Equal(OldContent, File.ReadAllBytes(Target));
    }

    [Fact]
    public async Task AllAddressesFail_NoNewPdf_OnlyManifestWritten()
    {
        File.Delete(Target);
        http.Status(Url, HttpStatusCode.NotFound).Html("https://www.example.test/b.pdf");

        DownloadRun run = await new DocumentDownloader(new HttpClient(http, false), Options)
            .DownloadAllAsync([new Uri(Url), new Uri("https://www.example.test/b.pdf")], cancellationToken: Ct);

        Assert.All(run.Results, r => Assert.Equal(DownloadStatus.Failed, r.Status));
        Assert.Equal(["manifest.json"], temp.FileNames("out"));
        string manifest = await File.ReadAllTextAsync(run.ManifestPath, Ct);
        Assert.Contains("serwer zwrócił 404 Not Found", manifest, StringComparison.Ordinal);
        Assert.Contains("pod adresem nie ma pliku PDF", manifest, StringComparison.Ordinal);
    }

    private string Output => temp.Combine("out");

    private string Target => Path.Combine(Output, "a.pdf");

    private DownloadOptions Options => new() { OutputDirectory = Output, AllowedHosts = ["example.test"] };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<DownloadResult> RunAsync(DownloadOptions? options = null)
    {
        DownloadRun run = await new DocumentDownloader(new HttpClient(http, false), options ?? Options)
            .DownloadAllAsync([new Uri(Url)], cancellationToken: Ct);
        return Assert.Single(run.Results);
    }

    private void AssertOldFileIntact()
    {
        Assert.Equal(OldContent, File.ReadAllBytes(Target));
        Assert.False(File.Exists(Target + ".part"));
    }
}
