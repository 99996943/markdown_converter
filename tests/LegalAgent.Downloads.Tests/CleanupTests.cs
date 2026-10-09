using System.Net;
using LegalAgent.Downloads.Model;
using LegalAgent.Downloads.Tests.Fakes;

namespace LegalAgent.Downloads.Tests;

public sealed class CleanupTests : IDisposable
{
    private static readonly string[] Urls = [.. Enumerable.Range(1, 3).Select(i => $"https://www.example.test/reg-{i}.pdf")];

    private readonly TempDirectory temp = new();
    private readonly FakeHttpHandler http = new();

    public CleanupTests()
    {
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory(Path.Combine(Output, "x"));
        foreach (string name in new[] { "stary.pdf", "STARY2.PDF", "notatki.txt", Path.Combine("x", "y.pdf"), "z.pdf.part" })
        {
            File.WriteAllBytes(Path.Combine(Output, name), PdfBytes.Sample(name));
        }

        foreach (string url in Urls)
        {
            http.Pdf(url);
        }
    }

    public void Dispose()
    {
        http.Dispose();
        temp.Dispose();
    }

    [Fact]
    public async Task AllDownloaded_RemovesPdfsOutsideCurrentSet_AndLeftoverParts()
    {
        DownloadRun run = await RunAsync();

        Assert.True(run.AllSucceeded);
        Assert.Equal(["STARY2.PDF", "stary.pdf", "z.pdf.part"], run.RemovedFiles);
        Assert.Equal(["manifest.json", "notatki.txt", "reg-1.pdf", "reg-2.pdf", "reg-3.pdf"], temp.FileNames("out"));
        Assert.True(File.Exists(Path.Combine(Output, "x", "y.pdf")));
    }

    [Fact]
    public async Task CurrentFileDifferingOnlyInCase_IsNotRemoved()
    {
        File.WriteAllBytes(Path.Combine(Output, "REG-1.PDF"), PdfBytes.Sample("stara"));

        DownloadRun run = await RunAsync();

        Assert.DoesNotContain(run.RemovedFiles, f => f.Equals("REG-1.PDF", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(Path.Combine(Output, "reg-1.pdf")));
    }

    [Fact]
    public async Task OneFailed_RemovesNothing()
    {
        http.Status(Urls[1], HttpStatusCode.NotFound);

        DownloadRun run = await RunAsync();

        Assert.False(run.AllSucceeded);
        Assert.Empty(run.RemovedFiles);
        Assert.True(File.Exists(Path.Combine(Output, "stary.pdf")));
        Assert.True(File.Exists(Path.Combine(Output, "STARY2.PDF")));
        Assert.True(File.Exists(Path.Combine(Output, "z.pdf.part")));
    }

    [Fact]
    public async Task FileThatCannotBeRemoved_ThrowsDownloadDirectoryException()
    {
        // Only Windows refuses to delete a file that is open without FileShare.Delete.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "deleting an open file succeeds outside Windows");
        using FileStream locked = new(Path.Combine(Output, "stary.pdf"), FileMode.Open, FileAccess.Read, FileShare.None);

        await Assert.ThrowsAsync<DownloadDirectoryException>(RunAsync);
    }

    private string Output => temp.Combine("out");

    private async Task<DownloadRun> RunAsync() =>
        await new DocumentDownloader(new HttpClient(http, false), new DownloadOptions { OutputDirectory = Output, AllowedHosts = ["example.test"] })
            .DownloadAllAsync([.. Urls.Select(u => new Uri(u))], cancellationToken: TestContext.Current.CancellationToken);
}
