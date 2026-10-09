using LegalAgent.Downloads.Model;
using LegalAgent.Downloads.Tests.Fakes;

namespace LegalAgent.Downloads.Tests;

public sealed class CancellationTests : IDisposable
{
    private const string Fast = "https://www.example.test/szybki.pdf";
    private const string Slow = "https://www.example.test/wolny.pdf";

    private readonly TempDirectory temp = new();
    private readonly FakeHttpHandler http = new();

    public void Dispose()
    {
        http.Dispose();
        temp.Dispose();
    }

    [Fact]
    public async Task CancelDuringDownload_WritesManifestWithCancelled_ThenThrows()
    {
        http.Pdf(Fast).Body(Slow, () => new ScriptedStream(PdfBytes.OfSize(1000), failAfter: 500, hangAfter: true));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var progress = new CancelOnFinished(1, cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Downloader().DownloadAllAsync([new Uri(Fast), new Uri(Slow)], progress, cancellation.Token));

        string manifest = await File.ReadAllTextAsync(Path.Combine(Output, "manifest.json"), TestContext.Current.CancellationToken);
        Assert.Contains("\"status\": \"downloaded\"", manifest, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"cancelled\"", manifest, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Output, "szybki.pdf")));
        Assert.False(File.Exists(Path.Combine(Output, "wolny.pdf")));
        Assert.Empty(Directory.GetFiles(Output, "*.part"));
    }

    [Fact]
    public async Task CancelledBeforeStart_ThrowsWithoutTouchingDirectory()
    {
        http.Pdf(Fast);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Downloader().DownloadAllAsync([new Uri(Fast)], null, cancellation.Token));

        Assert.False(Directory.Exists(Output));
        Assert.Empty(http.Requests);
    }

    private string Output => temp.Combine("out");

    private DocumentDownloader Downloader() =>
        new(new HttpClient(http, false), new DownloadOptions { OutputDirectory = Output, AllowedHosts = ["example.test"] });

    /// <summary>Cancels when the given address finished.</summary>
    private sealed class CancelOnFinished(int index, CancellationTokenSource cancellation) : IProgress<DownloadEvent>
    {
        public void Report(DownloadEvent value)
        {
            if (value.Index == index && value.Kind == DownloadEventKind.Finished)
            {
                cancellation.Cancel();
            }
        }
    }
}
