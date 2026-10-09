using System.Security.Cryptography;
using LegalAgent.Downloads.Model;
using LegalAgent.Downloads.Tests.Fakes;

namespace LegalAgent.Downloads.Tests;

public sealed class DocumentDownloaderTests : IDisposable
{
    private const string A = "https://www.example.test/pdf/regulamin-konta.pdf";
    private const string B = "https://files.example.test/taryfa.pdf?v=2";

    private readonly TempDirectory temp = new();
    private readonly FakeHttpHandler http = new();

    private string Output => temp.Combine("out");

    public void Dispose()
    {
        http.Dispose();
        temp.Dispose();
    }

    [Fact]
    public async Task DownloadAll_Success_WritesFilesAndReturnsResults()
    {
        byte[] bodyA = PdfBytes.Sample("a");
        byte[] bodyB = PdfBytes.Sample("b");
        var modified = new DateTimeOffset(2026, 9, 30, 8, 15, 0, TimeSpan.Zero);
        http.Pdf(A, bodyA, modified).Pdf(B, bodyB);

        DownloadRun run = await Downloader().DownloadAllAsync([new Uri(A), new Uri(B)], cancellationToken: Ct);

        Assert.True(Directory.Exists(Output));
        Assert.True(run.AllSucceeded);
        Assert.Equal([1, 2], run.Results.Select(r => r.Index));

        DownloadResult a = run.Results[0];
        Assert.Equal(DownloadStatus.Downloaded, a.Status);
        Assert.Equal(new Uri(A), a.Address);
        Assert.Equal("regulamin-konta.pdf", a.FileName);
        Assert.Equal(bodyA.Length, a.SizeBytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bodyA)), a.Sha256);
        Assert.Equal(modified, a.LastModified);
        Assert.Null(a.Error);
        Assert.Equal(bodyA, await File.ReadAllBytesAsync(Path.Combine(Output, "regulamin-konta.pdf"), Ct));

        DownloadResult b = run.Results[1];
        Assert.Equal("taryfa.pdf", b.FileName);
        Assert.Null(b.LastModified);
        Assert.Matches("^[0-9a-f]{64}$", b.Sha256);
        Assert.Equal(bodyB, await File.ReadAllBytesAsync(Path.Combine(Output, "taryfa.pdf"), Ct));

        Assert.Empty(Directory.GetFiles(Output, "*.part"));
    }

    [Fact]
    public async Task DownloadAll_SendsConfiguredUserAgent()
    {
        http.Pdf(A);

        await Downloader(Options with { UserAgent = "agent-testowy/2.0" }).DownloadAllAsync([new Uri(A)], cancellationToken: Ct);

        Assert.Equal("agent-testowy/2.0", Assert.Single(http.Requests).UserAgent);
    }

    [Fact]
    public async Task DownloadAll_EmptyList_ThrowsWithoutChanges()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Downloader().DownloadAllAsync([], cancellationToken: Ct));

        Assert.False(Directory.Exists(Output));
    }

    [Fact]
    public async Task DownloadAll_AddressOutsideAllowedHosts_ThrowsWithoutRequests()
    {
        http.Pdf("https://evil.com/a.pdf");

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => Downloader().DownloadAllAsync([new Uri(A), new Uri("https://evil.com/a.pdf")], cancellationToken: Ct));

        Assert.Empty(http.Requests);
        Assert.False(Directory.Exists(Output));
    }

    [Fact]
    public async Task DownloadAll_RunsAllDownloadsConcurrently_AndReportsProgress()
    {
        string[] urls = [.. Enumerable.Range(1, 5).Select(i => $"https://www.example.test/{i}.pdf")];
        foreach (string url in urls)
        {
            http.Pdf(url);
        }

        // Every response is held until all five requests arrived: a sequential downloader times out at the barrier.
        http.HoldUntil(5);
        var progress = new EventCollector();

        DownloadRun run = await Downloader().DownloadAllAsync([.. urls.Select(u => new Uri(u))], progress, Ct);

        Assert.True(run.AllSucceeded);
        Assert.Equal(5, http.Requests.Count);
        for (int index = 1; index <= 5; index++)
        {
            DownloadEvent[] events = [.. progress.Events.Where(e => e.Index == index)];
            Assert.Equal([DownloadEventKind.Started, DownloadEventKind.Finished], events.Select(e => e.Kind));
            Assert.Null(events[0].Result);
            Assert.Equal(DownloadStatus.Downloaded, events[1].Result?.Status);
            Assert.Equal(new Uri(urls[index - 1]), events[1].Address);
        }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DownloadOptions Options => new() { OutputDirectory = Output, AllowedHosts = ["example.test"] };

    private DocumentDownloader Downloader(DownloadOptions? options = null) =>
        new(new HttpClient(http, disposeHandler: false), options ?? Options);
}
