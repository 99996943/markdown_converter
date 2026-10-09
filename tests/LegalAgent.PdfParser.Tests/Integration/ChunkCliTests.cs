using System.Runtime.CompilerServices;
using LegalAgent.Chunking.Serialization;
using LegalAgent.PdfParser.Cli;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>Spec 004, T034 — the <c>chunk</c> command (specs/004-document-chunking/contracts/cli.md).</summary>
public sealed class ChunkCliTests : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string?> NoEnv = new Dictionary<string, string?>();

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "chunk-cli-tests-" + Guid.NewGuid().ToString("N"));

    public ChunkCliTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string Fixture(string name, [CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "Corpus", "errors", name);

    private static string CorpusPdf(string relative, [CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "..", "corpus", relative));

    private string Target => Path.Combine(_dir, "out.chunks.jsonl");

    private string TwoPagePdf(string name = "two.pdf")
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, new SyntheticPdfBuilder().Page().Text(72, 100, "Pierwsza strona.").Page().Text(72, 100, "Druga strona.").Build());
        return path;
    }

    private static async Task<(int Code, string Out, string Err)> RunAsync(string[] args, IReadOnlyDictionary<string, string?>? env = null)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int code = await Program.RunAsync(args, stdout, stderr, env ?? NoEnv, TestContext.Current.CancellationToken);
        return (code, stdout.ToString(), stderr.ToString());
    }

    private IReadOnlyList<ChunkRecord> Records() => ChunkJson.ReadLines(File.ReadAllText(Target));

    [Fact]
    public async Task Chunk_WritesJsonLinesWithTheGivenMetadata()
    {
        (int code, string stdout, _) = await RunAsync(
        [
            "chunk", TwoPagePdf(), "-o", Target,
            "--id", "REG-07-w1", "--designation", "BP/REG/07", "--type", "regulaminy", "--title", "Regulamin testowy",
            "--doc-version", "1", "--valid-from", "2025-01-01", "--valid-to", "2025-12-31", "--status", "nieaktualny",
            "--previous-version", "REG-07-w0",
        ]);

        Assert.Equal(0, code);
        IReadOnlyList<ChunkRecord> records = Records();
        Assert.NotEmpty(records);
        Chunking.Model.DocumentMetadata m = records[0].Document.Metadata;
        Assert.Equal(
            ("REG-07-w1", "BP/REG/07", "regulaminy", "Regulamin testowy", (int?)1, (DateOnly?)new DateOnly(2025, 1, 1), (DateOnly?)new DateOnly(2025, 12, 31), "nieaktualny", "REG-07-w0"),
            (m.DocumentId, m.Designation, m.Type, m.Title, m.Version, m.ValidFrom, m.ValidTo, m.Status, m.PreviousVersion));
        Assert.Contains("Pierwsza strona.", records[0].Chunk.Content, StringComparison.Ordinal);
        Assert.Contains($"Fragmenty: {records.Count}, przekraczające limit: 0", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', File.ReadAllText(Target));
    }

    [Fact]
    public async Task Chunk_IdDefaultsToTheFileNameWithUnsafeCharactersReplaced()
    {
        (int code, _, _) = await RunAsync(["chunk", TwoPagePdf("Regulamin konta (2025).pdf"), "-o", Target]);

        Assert.Equal(0, code);
        Assert.Equal("Regulamin-konta--2025-", Records()[0].Document.Metadata.DocumentId);
        Assert.Equal("Regulamin-konta--2025-", Records()[0].Document.SeriesKey);
    }

    [Fact]
    public async Task Chunk_LimitComesFromTheEnvironmentAndTheArgumentWins()
    {
        string pdf = CorpusPdf("regulaminy/REG-06.pdf");
        var env = new Dictionary<string, string?> { ["CHUNKING__MaxChunkLength"] = "400" };

        await RunAsync(["chunk", pdf, "-o", Target]);
        int byDefault = Records().Count;
        await RunAsync(["chunk", pdf, "-o", Target], env);
        int byEnvironment = Records().Count;
        await RunAsync(["chunk", pdf, "-o", Target, "--max-length", "2000"], env);
        int byArgument = Records().Count;

        Assert.True(byEnvironment > byDefault, $"{byEnvironment} > {byDefault}");
        Assert.Equal(byDefault, byArgument);
        Assert.All(Records(), r => Assert.True(r.Chunk.Length <= 2000 || r.Chunk.ExceedsLimit));
    }

    [Theory]
    [InlineData("--doc-version", "0")]
    [InlineData("--doc-version", "x")]
    [InlineData("--valid-from", "2025-13-01")]
    [InlineData("--max-length", "10")]
    [InlineData("--id", "REG 07")]
    [InlineData("--nieznana", "1")]
    public async Task Chunk_InvalidOptionExits2WithoutWritingTheFile(string option, string value)
    {
        (int code, _, string stderr) = await RunAsync(["chunk", TwoPagePdf(), "-o", Target, option, value]);

        Assert.Equal(2, code);
        Assert.Contains("Błąd", stderr, StringComparison.Ordinal);
        Assert.False(File.Exists(Target));
    }

    [Fact]
    public async Task Chunk_ValidToBeforeValidFromExits2()
    {
        (int code, _, string stderr) = await RunAsync(["chunk", TwoPagePdf(), "-o", Target, "--valid-from", "2025-06-01", "--valid-to", "2025-05-31"]);

        Assert.Equal(2, code);
        Assert.NotEmpty(stderr);
    }

    [Theory]
    [InlineData("chunk")]
    [InlineData("chunk", "brak.pdf", "-o", "x.jsonl")]
    public async Task Chunk_MissingInputExits2(params string[] args)
    {
        (int code, _, string stderr) = await RunAsync(args);

        Assert.Equal(2, code);
        Assert.NotEmpty(stderr);
    }

    [Fact]
    public async Task Chunk_MissingOutputExits2()
    {
        (int code, _, string stderr) = await RunAsync(["chunk", TwoPagePdf()]);

        Assert.Equal(2, code);
        Assert.Contains("-o", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chunk_InvalidPdfExits3WithoutAFile()
    {
        (int code, _, _) = await RunAsync(["chunk", Fixture("not-a-pdf.txt"), "-o", Target]);

        Assert.Equal(3, code);
        Assert.False(File.Exists(Target));
        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task Chunk_BrokenPageExits4OrWithAllowPartial6WithTheSkippedPageRecorded()
    {
        (int failed, _, _) = await RunAsync(["chunk", Fixture("broken-page.pdf"), "-o", Target]);
        bool written = File.Exists(Target);
        (int partial, _, _) = await RunAsync(["chunk", Fixture("broken-page.pdf"), "-o", Target, "--allow-partial"]);

        Assert.Equal(4, failed);
        Assert.False(written);
        Assert.Equal(6, partial);
        ChunkRecord record = Records()[0];
        Assert.False(record.Document.Source.IsComplete);
        Assert.NotEmpty(record.Document.Source.SkippedPages);
    }

    [Fact]
    public async Task Help_ListsTheChunkCommand()
    {
        (int code, string stdout, _) = await RunAsync(["--help"]);

        Assert.Equal(0, code);
        Assert.Contains("legalagent-pdf chunk", stdout, StringComparison.Ordinal);
        Assert.Contains("CHUNKING__", stdout, StringComparison.Ordinal);
    }
}
