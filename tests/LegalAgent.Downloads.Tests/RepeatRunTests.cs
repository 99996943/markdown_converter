using LegalAgent.Downloads.Model;
using LegalAgent.Downloads.Tests.Fakes;

namespace LegalAgent.Downloads.Tests;

public sealed class RepeatRunTests : IDisposable
{
    private static readonly Uri[] Addresses =
    [
        new("https://www.example.test/a/regulamin.pdf"),
        new("https://www.example.test/b/regulamin.pdf"),
        new("https://www.example.test/taryfa"),
    ];

    private readonly TempDirectory temp = new();
    private readonly FakeHttpHandler http = new();

    public RepeatRunTests()
    {
        foreach (Uri address in Addresses)
        {
            http.Pdf(address.AbsoluteUri);
        }
    }

    public void Dispose()
    {
        http.Dispose();
        temp.Dispose();
    }

    [Fact]
    public async Task SecondRun_OverwritesSameNames_WithIdenticalContent()
    {
        await RunAsync(Output);
        Dictionary<string, byte[]> first = Snapshot();

        await RunAsync(Output);
        Dictionary<string, byte[]> second = Snapshot();

        Assert.Equal(["manifest.json", "regulamin-2.pdf", "regulamin.pdf", "taryfa.pdf"], first.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(first.Keys.Order(StringComparer.Ordinal), second.Keys.Order(StringComparer.Ordinal));
        foreach ((string name, byte[] content) in first)
        {
            Assert.Equal(content, second[name]);
        }
    }

    [Fact]
    public async Task OutputPathIsAFile_ThrowsBeforeAnyRequest()
    {
        string file = temp.Combine("plik");
        await File.WriteAllTextAsync(file, "x", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<DownloadDirectoryException>(() => RunAsync(file));

        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task ManifestCannotBeWritten_ThrowsDownloadDirectoryException()
    {
        Directory.CreateDirectory(Path.Combine(Output, "manifest.json"));

        await Assert.ThrowsAsync<DownloadDirectoryException>(() => RunAsync(Output));
    }

    private string Output => temp.Combine("out");

    private Dictionary<string, byte[]> Snapshot() =>
        temp.FileNames("out").ToDictionary(n => n, n => File.ReadAllBytes(Path.Combine(Output, n)), StringComparer.Ordinal);

    private Task<DownloadRun> RunAsync(string output) =>
        new DocumentDownloader(new HttpClient(http, false), new DownloadOptions { OutputDirectory = output, AllowedHosts = ["example.test"] })
            .DownloadAllAsync(Addresses, cancellationToken: TestContext.Current.CancellationToken);
}
