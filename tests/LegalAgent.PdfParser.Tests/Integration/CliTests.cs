using System.Runtime.CompilerServices;
using System.Text.Json;
using LegalAgent.PdfParser.Cli;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>T089 — CLI (contracts/cli.md).</summary>
public sealed class CliTests : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string?> NoEnv = new Dictionary<string, string?>();

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cli-tests-" + Guid.NewGuid().ToString("N"));

    public CliTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string Fixture(string name, [CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "Corpus", "errors", name);

    private string WritePdf(string name, byte[] bytes)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string TwoPagePdf() => WritePdf(
        "two.pdf",
        new SyntheticPdfBuilder().Page().Text(72, 100, "Pierwsza strona.").Page().Text(72, 100, "Druga strona.").Build());

    private static async Task<(int Code, string Out, string Err)> RunAsync(
        string[] args,
        IReadOnlyDictionary<string, string?>? env = null,
        CancellationToken ct = default)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int code = await Program.RunAsync(args, stdout, stderr, env ?? NoEnv, ct);
        return (code, stdout.ToString(), stderr.ToString());
    }

    private static string[] FileNames(string dir) =>
        Directory.GetFiles(dir).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task WithoutOutput_WritesMarkdownToStdout()
    {
        string pdf = TwoPagePdf();

        (int code, string stdout, _) = await RunAsync(["convert", pdf], ct: TestContext.Current.CancellationToken);

        using FileStream stream = File.OpenRead(pdf);
        PdfConversionResult expected = await PdfMarkdownConverter.CreateDefault().ConvertAsync(
            stream, new PdfConversionRequest { SourceId = "two.pdf" }, TestContext.Current.CancellationToken);
        Assert.Equal(0, code);
        Assert.Equal(expected.Markdown, stdout);
        Assert.DoesNotContain('\r', stdout);
    }

    [Fact]
    public async Task Output_WritesFile_AndReplacesExisting()
    {
        string pdf = TwoPagePdf();
        string target = Path.Combine(_dir, "out.md");
        await File.WriteAllTextAsync(target, "stare", TestContext.Current.CancellationToken);

        (int code, string stdout, _) = await RunAsync(["convert", pdf, "-o", target], ct: TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stdout);
        string text = await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken);
        Assert.Contains("Pierwsza strona.", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
        Assert.NotEqual(0xEF, File.ReadAllBytes(target)[0]);
        Assert.Equal(["out.md", "two.pdf"], FileNames(_dir));
    }

    [Fact]
    public async Task FailedConversion_LeavesExistingTargetUntouched_AndNoTempFiles()
    {
        string target = Path.Combine(_dir, "out.md");
        await File.WriteAllTextAsync(target, "stare", TestContext.Current.CancellationToken);
        string input = Path.Combine(_dir, "not-a-pdf.txt");
        File.Copy(Fixture("not-a-pdf.txt"), input);

        (int code, _, _) = await RunAsync(["convert", input, "--output", target], ct: TestContext.Current.CancellationToken);

        Assert.Equal(3, code);
        Assert.Equal("stare", await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken));
        Assert.Equal(["not-a-pdf.txt", "out.md"], FileNames(_dir));
    }

    [Fact]
    public async Task Report_WritesCamelCaseJson()
    {
        string pdf = TwoPagePdf();
        string report = Path.Combine(_dir, "r.json");

        (int code, _, _) = await RunAsync(["convert", pdf, "--report", report], ct: TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        using JsonDocument json = JsonDocument.Parse(await File.ReadAllTextAsync(report, TestContext.Current.CancellationToken));
        Assert.Equal(2, json.RootElement.GetProperty("pageCount").GetInt32());
        Assert.Equal(JsonValueKind.Number, json.RootElement.GetProperty("elapsed").ValueKind);
    }

    [Fact]
    public async Task NoPageMarkers_RemovesMarkers()
    {
        string pdf = TwoPagePdf();

        (_, string withMarkers, _) = await RunAsync(["convert", pdf], ct: TestContext.Current.CancellationToken);
        (int code, string without, _) = await RunAsync(["convert", pdf, "--no-page-markers"], ct: TestContext.Current.CancellationToken);

        Assert.Contains("<!-- page:", withMarkers, StringComparison.Ordinal);
        Assert.Equal(0, code);
        Assert.DoesNotContain("<!-- page:", without, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BrokenPage_FailsWithoutAllowPartial_AndExits6With()
    {
        string pdf = Fixture("broken-page.pdf");
        string target = Path.Combine(_dir, "out.md");

        (int failed, _, _) = await RunAsync(["convert", pdf], ct: TestContext.Current.CancellationToken);
        (int partial, _, _) = await RunAsync(["convert", pdf, "--allow-partial", "-o", target], ct: TestContext.Current.CancellationToken);

        Assert.Equal(4, failed);
        Assert.Equal(6, partial);
        Assert.True(new FileInfo(target).Length > 0);
    }

    [Fact]
    public async Task EnvironmentLimit_IsApplied()
    {
        string pdf = TwoPagePdf();
        var env = new Dictionary<string, string?> { ["PDFPARSER__Limits__MaxPages"] = "1", ["PATH"] = "x" };

        (int code, _, _) = await RunAsync(["convert", pdf], env, TestContext.Current.CancellationToken);

        Assert.Equal(5, code);
    }

    [Theory]
    [InlineData("not-a-pdf.txt")]
    [InlineData("encrypted.pdf")]
    public async Task InvalidOrEncrypted_Exits3(string fixture)
    {
        (int code, _, string stderr) = await RunAsync(["convert", Fixture(fixture)], ct: TestContext.Current.CancellationToken);

        Assert.Equal(3, code);
        Assert.NotEmpty(stderr);
    }

    [Theory]
    [InlineData("convert", "brak.pdf")]
    [InlineData("convert", "--nieznana")]
    [InlineData("convert")]
    [InlineData("convert", "a.pdf", "b.pdf")]
    [InlineData("convert", "a.pdf", "-o")]
    [InlineData("nieznane")]
    public async Task BadArguments_Exit2(params string[] args)
    {
        (int code, _, string stderr) = await RunAsync(args, ct: TestContext.Current.CancellationToken);

        Assert.Equal(2, code);
        Assert.NotEmpty(stderr);
    }

    [Fact]
    public async Task Help_PrintsUsageToStdout()
    {
        (int code, string stdout, _) = await RunAsync(["--help"], ct: TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Contains("convert", stdout, StringComparison.Ordinal);
        Assert.Contains("--allow-partial", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Version_Exits0()
    {
        (int code, string stdout, _) = await RunAsync(["--version"], ct: TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.NotEmpty(stdout.Trim());
    }

    [Fact]
    public async Task CancelledToken_Exits130()
    {
        string pdf = TwoPagePdf();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        (int code, _, _) = await RunAsync(["convert", pdf], ct: cts.Token);

        Assert.Equal(130, code);
    }

    [Fact]
    public async Task Warnings_GoToStderrInPolish()
    {
        string pdf = WritePdf(
            "img.pdf",
            new SyntheticPdfBuilder().Page().Text(72, 100, "Tekst obok obrazu.").Image(72, 200, 100, 100).Build());

        (int code, _, string stderr) = await RunAsync(["convert", pdf], ct: TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Contains("ostrzeżenie IMG001_ImagesIgnored (strona 1):", stderr, StringComparison.Ordinal);
    }
}
