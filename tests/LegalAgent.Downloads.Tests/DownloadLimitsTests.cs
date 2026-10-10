using System.Diagnostics;
using LegalAgent.Downloads.Model;
using LegalAgent.Downloads.Tests.Fakes;

namespace LegalAgent.Downloads.Tests;

public sealed class DownloadLimitsTests : IDisposable
{
    private const string Good = "https://www.example.test/ok.pdf";
    private const string Bad = "https://www.example.test/limit.pdf";

    private readonly TempDirectory temp = new();
    private readonly FakeHttpHandler http = new();

    public DownloadLimitsTests()
    {
        http.Pdf(Good);
    }

    public void Dispose()
    {
        http.Dispose();
        temp.Dispose();
    }

    [Fact]
    public async Task NoResponse_TimesOut_WithinLimit()
    {
        http.Hang(Bad);
        var stopwatch = Stopwatch.StartNew();

        DownloadRun run = await RunAsync(Options with { Timeout = TimeSpan.FromMilliseconds(200) });

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"took {stopwatch.Elapsed}");
        DownloadError error = AssertOnlyBadFailed(run);
        Assert.Equal(DownloadErrorKind.Timeout, error.Kind);
        Assert.Equal("przekroczono limit czasu 0,2 s", error.Message);
    }

    [Fact]
    public async Task BodyStalls_TimesOut_AndNoPartIsLeft()
    {
        http.Body(Bad, () => new ScriptedStream(PdfBytes.OfSize(1000), failAfter: 500, hangAfter: true));

        DownloadRun run = await RunAsync(Options with { Timeout = TimeSpan.FromMilliseconds(200) });

        Assert.Equal(DownloadErrorKind.Timeout, AssertOnlyBadFailed(run).Kind);
        Assert.False(File.Exists(Path.Combine(Output, "limit.pdf")));
    }

    [Fact]
    public void TimeoutMessage_UsesWholeSecondsWhenPossible() =>
        Assert.Equal("przekroczono limit czasu 60 s", SingleDownload.TimeoutMessage(TimeSpan.FromSeconds(60)));

    [Fact]
    public async Task DeclaredLengthAboveLimit_IsTooLarge_WithoutReadingBody()
    {
        // The body would fail on the first read: TooLarge proves it was never read.
        http.Body(Bad, () => new ScriptedStream(PdfBytes.OfSize(10), failAfter: 0), contentLength: 2001);

        DownloadRun run = await RunAsync(Options with { MaxFileSizeBytes = 2000 });

        DownloadError error = AssertOnlyBadFailed(run);
        Assert.Equal(DownloadErrorKind.TooLarge, error.Kind);
        Assert.StartsWith("plik przekracza limit rozmiaru", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UndeclaredBodyAboveLimit_IsTooLarge()
    {
        http.Body(Bad, () => new ScriptedStream(PdfBytes.OfSize(5000)));

        DownloadRun run = await RunAsync(Options with { MaxFileSizeBytes = 2000 });

        Assert.Equal(DownloadErrorKind.TooLarge, AssertOnlyBadFailed(run).Kind);
        Assert.False(File.Exists(Path.Combine(Output, "limit.pdf")));
    }

    [Fact]
    public async Task BodyExactlyAtLimit_IsDownloaded()
    {
        http.Body(Bad, () => new ScriptedStream(PdfBytes.OfSize(2000)));

        DownloadRun run = await RunAsync(Options with { MaxFileSizeBytes = 2000 });

        Assert.True(run.AllSucceeded);
    }

    [Fact]
    public void SizeMessage_UsesMegabytes() =>
        Assert.Equal("plik przekracza limit rozmiaru 50 MB", SingleDownload.TooLargeMessage(50L * 1024 * 1024));

    private string Output => temp.Combine("out");

    private DownloadOptions Options => new() { OutputDirectory = Output, AllowedHosts = ["example.test"] };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<DownloadRun> RunAsync(DownloadOptions options) =>
        await new DocumentDownloader(new HttpClient(http, false), options)
            .DownloadAllAsync([new Uri(Good), new Uri(Bad)], cancellationToken: Ct);

    private DownloadError AssertOnlyBadFailed(DownloadRun run)
    {
        Assert.Equal(DownloadStatus.Downloaded, run.Results[0].Status);
        Assert.True(File.Exists(Path.Combine(Output, "ok.pdf")));
        Assert.Equal(DownloadStatus.Failed, run.Results[1].Status);
        Assert.Empty(Directory.GetFiles(Output, "*.part"));
        return Assert.IsType<DownloadError>(run.Results[1].Error);
    }
}
