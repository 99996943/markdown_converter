using System.Text;
using LegalAgent.Downloads.Model;
using LegalAgent.Downloads.Tests.Fakes;

namespace LegalAgent.Downloads.Tests;

public sealed class ManifestTests : IDisposable
{
    private readonly TempDirectory temp = new();
    private readonly FakeHttpHandler http = new();

    public void Dispose()
    {
        http.Dispose();
        temp.Dispose();
    }

    [Fact]
    public void Serialize_MatchesContract()
    {
        DownloadResult[] results =
        [
            new()
            {
                Index = 1,
                Address = new Uri("https://www.mbank.pl/pdf/regulaminy/reg-konta.pdf"),
                FileName = "reg-konta.pdf",
                Status = DownloadStatus.Downloaded,
                SizeBytes = 831898,
                Sha256 = new string('a', 64),
                LastModified = new DateTimeOffset(2026, 9, 30, 10, 15, 0, TimeSpan.FromHours(2)),
            },
            new()
            {
                Index = 3,
                Address = new Uri("https://www.mbank.pl/pdf/stary.pdf"),
                FileName = "stary.pdf",
                Status = DownloadStatus.Failed,
                Error = new DownloadError(DownloadErrorKind.HttpStatus, "serwer zwrócił 404 Not Found", 404, "szczegóły wyjątku"),
            },
            new()
            {
                Index = 4,
                Address = new Uri("https://www.mbank.pl/pdf/wolny.pdf"),
                FileName = "wolny.pdf",
                Status = DownloadStatus.Failed,
                Error = new DownloadError(DownloadErrorKind.Timeout, "przekroczono limit czasu 60 s"),
            },
        ];

        string json = DownloadManifestJson.Serialize(results);

        const string expected = """
            {
              "schemaVersion": 1,
              "items": [
                {
                  "index": 1,
                  "url": "https://www.mbank.pl/pdf/regulaminy/reg-konta.pdf",
                  "status": "downloaded",
                  "file": "reg-konta.pdf",
                  "size": 831898,
                  "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                  "lastModified": "2026-09-30T08:15:00Z"
                },
                {
                  "index": 3,
                  "url": "https://www.mbank.pl/pdf/stary.pdf",
                  "status": "failed",
                  "file": "stary.pdf",
                  "error": {
                    "kind": "http-status",
                    "httpStatus": 404,
                    "message": "serwer zwrócił 404 Not Found"
                  }
                },
                {
                  "index": 4,
                  "url": "https://www.mbank.pl/pdf/wolny.pdf",
                  "status": "failed",
                  "file": "wolny.pdf",
                  "error": {
                    "kind": "timeout",
                    "message": "przekroczono limit czasu 60 s"
                  }
                }
              ]
            }

            """;
        Assert.Equal(expected.ReplaceLineEndings("\n"), json);
        Assert.DoesNotContain("szczegóły", json, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', json);
    }

    [Theory]
    [InlineData(DownloadErrorKind.HttpStatus, "http-status")]
    [InlineData(DownloadErrorKind.Timeout, "timeout")]
    [InlineData(DownloadErrorKind.Connection, "connection")]
    [InlineData(DownloadErrorKind.NotPdf, "not-pdf")]
    [InlineData(DownloadErrorKind.TooLarge, "too-large")]
    [InlineData(DownloadErrorKind.RedirectNotAllowed, "redirect-not-allowed")]
    [InlineData(DownloadErrorKind.TooManyRedirects, "too-many-redirects")]
    [InlineData(DownloadErrorKind.WriteFailed, "write-failed")]
    [InlineData(DownloadErrorKind.Cancelled, "cancelled")]
    public void Serialize_ErrorKindsAreKebabCase(DownloadErrorKind kind, string expected)
    {
        DownloadResult result = new()
        {
            Index = 1,
            Address = new Uri("https://www.mbank.pl/a.pdf"),
            FileName = "a.pdf",
            Status = DownloadStatus.Failed,
            Error = new DownloadError(kind, "x"),
        };

        Assert.Contains($"\"kind\": \"{expected}\"", DownloadManifestJson.Serialize([result]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloadAll_WritesManifest_IdenticalAcrossRuns()
    {
        string output = temp.Combine("out");
        http.Pdf("https://www.example.test/a.pdf", lastModified: new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero))
            .Pdf("https://www.example.test/b.pdf");
        var options = new DownloadOptions { OutputDirectory = output, AllowedHosts = ["example.test"] };
        Uri[] addresses = [new("https://www.example.test/a.pdf"), new("https://www.example.test/b.pdf")];

        DownloadRun first = await new DocumentDownloader(new HttpClient(http, false), options).DownloadAllAsync(addresses, cancellationToken: Ct);
        byte[] firstManifest = await File.ReadAllBytesAsync(first.ManifestPath, Ct);
        DownloadRun second = await new DocumentDownloader(new HttpClient(http, false), options).DownloadAllAsync(addresses, cancellationToken: Ct);
        byte[] secondManifest = await File.ReadAllBytesAsync(second.ManifestPath, Ct);

        Assert.Equal(Path.Combine(output, "manifest.json"), first.ManifestPath);
        Assert.Equal(firstManifest, secondManifest);
        Assert.Equal(DownloadManifestJson.Serialize(first.Results), Encoding.UTF8.GetString(firstManifest));
        Assert.False(firstManifest.AsSpan().StartsWith(Encoding.UTF8.Preamble), "manifest must not start with a BOM");
        Assert.Empty(Directory.GetFiles(output, "*.part"));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
