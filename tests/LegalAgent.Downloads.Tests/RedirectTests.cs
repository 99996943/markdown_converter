using System.Net;
using LegalAgent.Downloads.Model;
using LegalAgent.Downloads.Tests.Fakes;

namespace LegalAgent.Downloads.Tests;

public sealed class RedirectTests : IDisposable
{
    private const string Start = "https://www.example.test/pdf/regulamin.pdf";

    private readonly TempDirectory temp = new();
    private readonly FakeHttpHandler http = new();

    public void Dispose()
    {
        http.Dispose();
        temp.Dispose();
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.SeeOther)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.PermanentRedirect)]
    public async Task RedirectToAllowedHost_IsFollowed_AndOriginalAddressIsKept(HttpStatusCode code)
    {
        byte[] body = PdfBytes.Sample("cel");
        http.Redirect(Start, "https://cdn.example.test/files/abc123.pdf", code)
            .Pdf("https://cdn.example.test/files/abc123.pdf", body);

        DownloadResult result = await RunAsync();

        Assert.Equal(DownloadStatus.Downloaded, result.Status);
        Assert.Equal(new Uri(Start), result.Address);
        Assert.Equal("regulamin.pdf", result.FileName);
        Assert.Equal(body, await File.ReadAllBytesAsync(Path.Combine(Output, "regulamin.pdf"), Ct));
        Assert.Contains("\"url\": \"" + Start + "\"", await File.ReadAllTextAsync(Path.Combine(Output, "manifest.json"), Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RelativeLocation_IsResolvedAgainstCurrentAddress()
    {
        http.Redirect(Start, "/nowe/regulamin-2026.pdf").Pdf("https://www.example.test/nowe/regulamin-2026.pdf");

        DownloadResult result = await RunAsync();

        Assert.Equal(DownloadStatus.Downloaded, result.Status);
        Assert.Equal(new Uri("https://www.example.test/nowe/regulamin-2026.pdf"), http.Requests[^1].Address);
    }

    [Fact]
    public async Task RedirectToOtherHost_IsRejected_BeforeAnyRequestToIt()
    {
        http.Redirect(Start, "https://evil.com/a.pdf").Pdf("https://evil.com/a.pdf");

        DownloadResult result = await RunAsync();

        Assert.Equal(DownloadStatus.Failed, result.Status);
        Assert.Equal(DownloadErrorKind.RedirectNotAllowed, result.Error?.Kind);
        Assert.Contains("evil.com", result.Error?.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(http.Requests, r => r.Address.Host == "evil.com");
    }

    [Fact]
    public async Task RedirectFromHttpsToHttp_IsRejectedUnlessAllowed()
    {
        http.Redirect(Start, "http://www.example.test/pdf/regulamin.pdf").Pdf("http://www.example.test/pdf/regulamin.pdf");

        DownloadResult rejected = await RunAsync();
        DownloadResult allowed = await RunAsync(Options with { AllowHttp = true });

        Assert.Equal(DownloadErrorKind.RedirectNotAllowed, rejected.Error?.Kind);
        Assert.Equal(DownloadStatus.Downloaded, allowed.Status);
    }

    [Fact]
    public async Task ChainLongerThanLimit_IsTooManyRedirects()
    {
        http.Redirect(Start, "https://www.example.test/r1")
            .Redirect("https://www.example.test/r1", "https://www.example.test/r2")
            .Redirect("https://www.example.test/r2", "https://www.example.test/r3")
            .Pdf("https://www.example.test/r3");

        DownloadResult tooMany = await RunAsync(Options with { MaxRedirects = 2 });
        DownloadResult enough = await RunAsync(Options with { MaxRedirects = 3 });

        Assert.Equal(DownloadErrorKind.TooManyRedirects, tooMany.Error?.Kind);
        Assert.Equal(DownloadStatus.Downloaded, enough.Status);
    }

    [Fact]
    public async Task RedirectWithoutLocation_IsHttpStatusFailure()
    {
        http.Status(Start, HttpStatusCode.Found);

        DownloadResult result = await RunAsync();

        Assert.Equal(DownloadErrorKind.HttpStatus, result.Error?.Kind);
        Assert.Equal(302, result.Error?.HttpStatusCode);
    }

    private string Output => temp.Combine("out");

    private DownloadOptions Options => new() { OutputDirectory = Output, AllowedHosts = ["example.test"] };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<DownloadResult> RunAsync(DownloadOptions? options = null)
    {
        DownloadRun run = await new DocumentDownloader(new HttpClient(http, false), options ?? Options)
            .DownloadAllAsync([new Uri(Start)], cancellationToken: Ct);
        return Assert.Single(run.Results);
    }
}
