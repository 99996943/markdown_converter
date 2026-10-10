using System.Net;
using System.Net.Http;
using LegalAgent.Downloads.Model;
using LegalAgent.Downloads.Tests.Fakes;

namespace LegalAgent.Downloads.Tests;

public sealed class DownloadErrorsTests : IDisposable
{
    private const string Good1 = "https://www.example.test/ok-1.pdf";
    private const string Good2 = "https://www.example.test/ok-2.pdf";
    private const string Bad = "https://www.example.test/zly.pdf";

    private readonly TempDirectory temp = new();
    private readonly FakeHttpHandler http = new();

    public DownloadErrorsTests()
    {
        http.Pdf(Good1).Pdf(Good2);
    }

    public void Dispose()
    {
        http.Dispose();
        temp.Dispose();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "serwer zwrócił 404 Not Found")]
    [InlineData(HttpStatusCode.InternalServerError, "serwer zwrócił 500 Internal Server Error")]
    [InlineData(HttpStatusCode.Forbidden, "serwer zwrócił 403 Forbidden")]
    public async Task ErrorStatus_IsHttpStatusFailure_OthersDownloaded(HttpStatusCode code, string message)
    {
        http.Status(Bad, code);

        DownloadRun run = await RunAsync();

        DownloadError error = AssertOnlyBadFailed(run);
        Assert.Equal(DownloadErrorKind.HttpStatus, error.Kind);
        Assert.Equal((int)code, error.HttpStatusCode);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public async Task ConnectionFailure_IsConnectionFailure_WithHostInMessage_AndDetail()
    {
        http.Throw(Bad, new HttpRequestException(HttpRequestError.NameResolutionError, "No such host is known. (www.example.test:443)"));

        DownloadRun run = await RunAsync();

        DownloadError error = AssertOnlyBadFailed(run);
        Assert.Equal(DownloadErrorKind.Connection, error.Kind);
        Assert.Contains("www.example.test", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("No such host", error.Message, StringComparison.Ordinal);
        Assert.Contains("No such host", error.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HtmlInsteadOfPdf_IsNotPdfFailure_AndNotWritten()
    {
        http.Html(Bad);

        DownloadRun run = await RunAsync();

        DownloadError error = AssertOnlyBadFailed(run);
        Assert.Equal(DownloadErrorKind.NotPdf, error.Kind);
        Assert.Equal("pod adresem nie ma pliku PDF", error.Message);
        Assert.False(File.Exists(Path.Combine(Output, "zly.pdf")));
    }

    [Fact]
    public async Task EmptyBody_IsNotPdfFailure()
    {
        http.Body(Bad, () => new MemoryStream([]));

        DownloadRun run = await RunAsync();

        Assert.Equal(DownloadErrorKind.NotPdf, AssertOnlyBadFailed(run).Kind);
    }

    private string Output => temp.Combine("out");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<DownloadRun> RunAsync()
    {
        var options = new DownloadOptions { OutputDirectory = Output, AllowedHosts = ["example.test"] };
        return await new DocumentDownloader(new HttpClient(http, false), options)
            .DownloadAllAsync([new Uri(Good1), new Uri(Bad), new Uri(Good2)], cancellationToken: Ct);
    }

    private DownloadError AssertOnlyBadFailed(DownloadRun run)
    {
        Assert.False(run.AllSucceeded);
        Assert.Equal(DownloadStatus.Downloaded, run.Results[0].Status);
        Assert.Equal(DownloadStatus.Downloaded, run.Results[2].Status);
        Assert.True(File.Exists(Path.Combine(Output, "ok-1.pdf")));
        Assert.True(File.Exists(Path.Combine(Output, "ok-2.pdf")));

        DownloadResult bad = run.Results[1];
        Assert.Equal(DownloadStatus.Failed, bad.Status);
        Assert.Null(bad.SizeBytes);
        Assert.Null(bad.Sha256);
        Assert.Empty(Directory.GetFiles(Output, "*.part"));
        return Assert.IsType<DownloadError>(bad.Error);
    }
}
