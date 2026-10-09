using System.Globalization;
using LegalAgent.Corpus.Cli;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Tests.Unit.Content;

namespace LegalAgent.Corpus.Tests.Cli;

/// <summary>Exit codes and messages of the corpus CLI (contracts/cli.md, "Kody wyjścia").</summary>
public sealed class ProgramTests : IDisposable
{
    private readonly string baseDirectory = Path.Combine(Path.GetTempPath(), "cli-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    public ProgramTests()
    {
        Directory.CreateDirectory(baseDirectory);
        CopyDirectory(MiniContent.Path, Path.Combine(baseDirectory, "zrodla"));

        // The mini acts have no PDF; acts are covered by ActsTests (refresh needs every act's PDF).
        File.Delete(Path.Combine(baseDirectory, "zrodla", "akty.yaml"));
        new RunParameters
        {
            DocumentsPerType = 2,
            Pages = new PageRange(1, 3),
            StrictUniqueness = false,
            MaxSharedShare = 100,
            VersionedShare = 0,
            OutdatedPerType = 0,
            ContradictionPairsPerType = 0,
            CrossTypeContradictionPairs = 0,
            OutputDirectory = "out",
            ContentDirectory = "zrodla",
        }.Save(Path.Combine(baseDirectory, "przebieg.json"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Generate_WritesManifest_Exit0()
    {
        var (code, _, err) = Invoke("generate", "--params", "przebieg.json");

        Assert.True(code == 0, err);
        Assert.True(File.Exists(Path.Combine(baseDirectory, "out", "manifest.json")));
    }

    [Fact]
    public void Verify_AfterGenerate_Exit0()
    {
        Assert.Equal(0, Invoke("generate", "--params", "przebieg.json").Code);

        var (code, _, err) = Invoke("verify", "--params", "przebieg.json");

        Assert.True(code == 0, err);
    }

    [Fact]
    public void Verify_AfterChangingMarkdown_Exit1_AndNamesPath()
    {
        Assert.Equal(0, Invoke("generate", "--params", "przebieg.json").Code);
        string md = Directory.GetFiles(Path.Combine(baseDirectory, "out"), "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal).First();
        File.AppendAllText(md, "x");

        var (code, _, err) = Invoke("verify", "--params", "przebieg.json");

        Assert.Equal(1, code);
        Assert.Contains(Path.GetFileName(md), err, StringComparison.Ordinal);
        Assert.Contains("różni się", err, StringComparison.Ordinal);
    }

    [Fact]
    public void Help_Exit0_MentionsCommands()
    {
        var (code, output, _) = Invoke("--help");

        Assert.Equal(0, code);
        Assert.Contains("generate", output, StringComparison.Ordinal);
        Assert.Contains("verify", output, StringComparison.Ordinal);
        Assert.Contains("check", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Version_Exit0_PrintsGeneratorVersion()
    {
        var (code, output, _) = Invoke("--version");

        Assert.Equal(0, code);
        Assert.Contains(CorpusGenerator.GeneratorVersion, output, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownOption_Exit2_PolishMessage()
    {
        var (code, _, err) = Invoke("generate", "--params", "przebieg.json", "--nieznana");

        Assert.Equal(2, code);
        Assert.StartsWith("błąd: ", err, StringComparison.Ordinal);
        Assert.Contains("--nieznana", err, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownCommand_Exit2()
    {
        Assert.Equal(2, Invoke("zrob").Code);
    }

    [Fact]
    public void MissingValue_Exit2()
    {
        Assert.Equal(2, Invoke("generate", "--count").Code);
    }

    [Fact]
    public void NonNumericCount_Exit2()
    {
        var (code, _, err) = Invoke("generate", "--params", "przebieg.json", "--count", "abc");

        Assert.Equal(2, code);
        Assert.StartsWith("błąd: ", err, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroCount_FailsValidation_Exit2()
    {
        var (code, _, err) = Invoke("generate", "--params", "przebieg.json", "--count", "0");

        Assert.Equal(2, code);
        Assert.Contains("documentsPerType", err, StringComparison.Ordinal);
    }

    [Fact]
    public void BrokenYaml_Exit2_NamesFile()
    {
        File.AppendAllText(Path.Combine(baseDirectory, "zrodla", "bloki", "regulaminy", "karty.yaml"), "\nbloki: [\n");

        var (code, _, err) = Invoke("generate", "--params", "przebieg.json");

        Assert.Equal(2, code);
        Assert.Contains("karty.yaml", err, StringComparison.Ordinal);
    }

    [Fact]
    public void UnreachablePageRange_Exit3()
    {
        var (code, _, err) = Invoke("generate", "--params", "przebieg.json", "--pages", "40-50");

        Assert.Equal(3, code);
        Assert.StartsWith("błąd: ", err, StringComparison.Ordinal);
    }

    [Fact]
    public void ForbiddenNameInContent_Exit4()
    {
        File.WriteAllText(Path.Combine(baseDirectory, "zrodla", "zabronione.yaml"), "zabronione: [\"Bank Fikcyjny Zakazany\", \"numer karty\"]\n");

        var (code, _, err) = Invoke("generate", "--params", "przebieg.json");

        Assert.Equal(4, code);
        Assert.Contains("numer karty", err, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_PrintsReportHeader()
    {
        var (_, output, err) = Invoke("check", "--template", "regulamin-karty", "--pages", "1-3", "--content", Path.Combine(baseDirectory, "zrodla"));

        Assert.True(output.Contains("regulamin-karty", StringComparison.Ordinal), err);
        Assert.Contains("Szablon:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Refresh_RestoresMarkdownAndManifest_Exit0()
    {
        Assert.Equal(0, Invoke("generate", "--params", "przebieg.json").Code);
        string manifestPath = Path.Combine(baseDirectory, "out", "manifest.json");
        string originalManifest = File.ReadAllText(manifestPath);
        var originalMarkdown = MarkdownFiles().ToDictionary(f => f, File.ReadAllBytes, StringComparer.Ordinal);
        Assert.True(originalMarkdown.Count >= 2);
        string[] files = [.. originalMarkdown.Keys.Order(StringComparer.Ordinal)];
        File.Delete(files[0]);
        File.AppendAllText(files[1], "x");
        File.WriteAllText(manifestPath, originalManifest.Replace("\"parserVersion\": \"", "\"parserVersion\": \"0.0.0-stara-", StringComparison.Ordinal));
        Assert.NotEqual(originalManifest, File.ReadAllText(manifestPath));

        var (code, _, err) = Invoke("refresh", "--params", "przebieg.json");

        Assert.True(code == 0, err);
        Assert.Equal(originalManifest, File.ReadAllText(manifestPath));
        foreach ((string file, byte[] bytes) in originalMarkdown)
        {
            Assert.Equal(bytes, File.ReadAllBytes(file));
        }

        Assert.Equal(0, Invoke("verify", "--params", "przebieg.json").Code);
    }

    [Fact]
    public void Refresh_RewritesPageCount()
    {
        Assert.Equal(0, Invoke("generate", "--params", "przebieg.json").Code);
        string manifestPath = Path.Combine(baseDirectory, "out", "manifest.json");
        string originalManifest = File.ReadAllText(manifestPath);
        File.WriteAllText(manifestPath, System.Text.RegularExpressions.Regex.Replace(originalManifest, "\"pages\": [0-9]+", "\"pages\": 99"));

        var (code, _, err) = Invoke("refresh", "--params", "przebieg.json");

        Assert.True(code == 0, err);
        Assert.Equal(originalManifest, File.ReadAllText(manifestPath));
    }

    [Fact]
    public void Refresh_DoesNotTouchPdfs_AndNeedsNoContentDirectory()
    {
        Assert.Equal(0, Invoke("generate", "--params", "przebieg.json").Code);
        var pdfs = Directory.GetFiles(Path.Combine(baseDirectory, "out"), "*.pdf", SearchOption.AllDirectories)
            .ToDictionary(f => f, File.ReadAllBytes, StringComparer.Ordinal);
        Directory.Delete(Path.Combine(baseDirectory, "zrodla"), recursive: true);

        var (code, _, err) = Invoke("refresh", "--params", "przebieg.json");

        Assert.True(code == 0, err);
        foreach ((string file, byte[] bytes) in pdfs)
        {
            Assert.Equal(bytes, File.ReadAllBytes(file));
        }
    }

    [Fact]
    public void Refresh_WithoutManifest_Exit2_NamesManifest()
    {
        var (code, _, err) = Invoke("refresh", "--params", "przebieg.json");

        Assert.Equal(2, code);
        Assert.StartsWith("błąd: ", err, StringComparison.Ordinal);
        Assert.Contains("manifest.json", err, StringComparison.Ordinal);
    }

    [Fact]
    public void Refresh_MissingPdf_Exit6_NamesPath()
    {
        Assert.Equal(0, Invoke("generate", "--params", "przebieg.json").Code);
        string pdf = Directory.GetFiles(Path.Combine(baseDirectory, "out"), "*.pdf", SearchOption.AllDirectories).Order(StringComparer.Ordinal).First();
        File.Delete(pdf);

        var (code, _, err) = Invoke("refresh", "--params", "przebieg.json");

        Assert.Equal(6, code);
        Assert.Contains(Path.GetFileName(pdf), err, StringComparison.Ordinal);
    }

    [Fact]
    public void Refresh_RejectsGenerateOnlyOption_Exit2()
    {
        Assert.Equal(2, Invoke("refresh", "--params", "przebieg.json", "--save-params").Code);
    }

    [Fact]
    public void Help_MentionsRefresh()
    {
        Assert.Contains("refresh", Invoke("--help").Output, StringComparison.Ordinal);
    }

    private string[] MarkdownFiles() => Directory.GetFiles(Path.Combine(baseDirectory, "out"), "*.md", SearchOption.AllDirectories);

    private static void CopyDirectory(string source, string target)
    {
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }

    private (int Code, string Output, string Error) Invoke(params string[] args)
    {
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);
        int code = Program.Run(args, stdout, stderr, baseDirectory);
        return (code, stdout.ToString(), stderr.ToString());
    }
}
