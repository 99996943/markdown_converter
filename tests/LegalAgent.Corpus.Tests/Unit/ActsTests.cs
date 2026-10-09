using LegalAgent.Corpus.Cli;
using LegalAgent.Corpus.Manifest;
using LegalAgent.Corpus.Output;
using LegalAgent.Corpus.Pdf;
using LegalAgent.Corpus.Tests.Unit.Content;

namespace LegalAgent.Corpus.Tests.Unit;

/// <summary>US6 (FR-150 – FR-152): legal acts converted by <c>refresh</c>, listed in the manifest and in ZRODLA.md.</summary>
public sealed class ActsTests : IDisposable
{
    private const string ActId = "dz-u-2025-644-aml";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "corpus-acts-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Out => Path.Combine(_root, "out");

    private void Prepare(bool withPdf)
    {
        foreach (string file in Directory.EnumerateFiles(MiniContent.Path, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(_root, "zrodla", Path.GetRelativePath(MiniContent.Path, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }

        File.WriteAllText(Path.Combine(_root, "przebieg.json"), """
        {
          "seed": 4,
          "documentsPerType": 1,
          "types": ["regulaminy"],
          "pages": { "min": 1, "max": 3 },
          "versionedShare": 0,
          "outdatedPerType": 0,
          "contradictionPairsPerType": 0,
          "crossTypeContradictionPairs": 0,
          "strictUniqueness": false,
          "maxSharedShare": 100,
          "outputDirectory": "out",
          "contentDirectory": "zrodla"
        }
        """);
        Directory.CreateDirectory(Path.Combine(Out, "akty"));
        if (withPdf)
        {
            byte[] pdf = new SyntheticPdfBuilder()
                .Page()
                .Text(72, 80, "USTAWA", 14, bold: true)
                .Text(72, 100, "z dnia 1 marca 2018 r.", 11)
                .Text(72, 120, "o przeciwdziałaniu praniu pieniędzy oraz finansowaniu terroryzmu", 11, bold: true)
                .Text(72, 160, "Art. 1. Ustawa określa zasady i tryb przeciwdziałania praniu pieniędzy.", 11)
                .Build();
            File.WriteAllBytes(Path.Combine(Out, "akty", ActId + ".pdf"), pdf);
        }
    }

    private int Run(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int code = Program.Run([.. args, "--params", Path.Combine(_root, "przebieg.json")], stdout, stderr, _root);
        LastError = stderr.ToString();
        return code;
    }

    private string LastError { get; set; } = string.Empty;

    [Fact]
    public void Refresh_ConvertsTheActs_AndListsThemInTheManifestWithTheirSource()
    {
        Prepare(withPdf: true);
        Assert.Equal(0, Run("generate"));
        Assert.Equal(0, Run("refresh"));

        Assert.True(File.Exists(Path.Combine(Out, "akty", ActId + ".md")));
        Assert.Contains("Art. 1.", File.ReadAllText(Path.Combine(Out, "akty", ActId + ".md")), StringComparison.Ordinal);
        LegalAgent.Corpus.Manifest.Manifest manifest = ManifestWriter.Read(File.ReadAllText(Path.Combine(Out, "manifest.json")));
        ManifestDocument act = Assert.Single(manifest.Documents, d => d.Type == "akty");
        Assert.Equal(ActId, act.Id);
        Assert.Equal($"akty/{ActId}.pdf", act.Pdf);
        Assert.Equal($"akty/{ActId}.md", act.Markdown);
        Assert.Equal(1, act.Pages);
        Assert.Equal("Dz. U. 2025 poz. 644", act.Source!.Journal);
        Assert.Equal(new DateOnly(2025, 5, 9), act.Source.ConsolidatedTextDate);
        Assert.Equal("https://example.com/D2025000064401.pdf", act.Source.Url);
        Assert.Equal(new DateOnly(2026, 10, 8), act.Source.DownloadedOn);
        Assert.Equal("Przykładowa uwaga.", act.Source.Notes);
        Assert.Equal(0, manifest.Documents.ToList().FindIndex(d => d.Type == "akty"));
    }

    [Fact]
    public void Refresh_WritesZrodlaFromAktyYaml()
    {
        Prepare(withPdf: true);
        Assert.Equal(0, Run("generate"));
        Assert.Equal(0, Run("refresh"));

        string sources = File.ReadAllText(Path.Combine(Out, "akty", "ZRODLA.md"));
        Assert.Contains("| Plik | Akt | Publikator | Źródło | Pobrano | Uwagi |", sources, StringComparison.Ordinal);
        Assert.Contains($"| {ActId}.pdf | Ustawa z dnia 1 marca 2018 r. o przeciwdziałaniu praniu pieniędzy oraz finansowaniu terroryzmu | Dz. U. 2025 poz. 644 | https://example.com/D2025000064401.pdf | 2026-10-08 | Przykładowa uwaga. |", sources, StringComparison.Ordinal);
        Assert.Equal(CorpusWriter.TextBytes(sources), File.ReadAllBytes(Path.Combine(Out, "akty", "ZRODLA.md")));
    }

    [Fact]
    public void Refresh_WithAnActPdfMissing_ExitsWithCode6NamingTheFile()
    {
        Prepare(withPdf: false);
        Assert.Equal(0, Run("generate"));

        Assert.Equal(6, Run("refresh"));
        Assert.Contains(ActId + ".pdf", LastError, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_KeepsTheActsInTheManifest_AndVerifyAgrees()
    {
        Prepare(withPdf: true);
        Assert.Equal(0, Run("generate"));
        Assert.Equal(0, Run("refresh"));
        Assert.Equal(0, Run("generate"));

        LegalAgent.Corpus.Manifest.Manifest manifest = ManifestWriter.Read(File.ReadAllText(Path.Combine(Out, "manifest.json")));
        Assert.Single(manifest.Documents, d => d.Type == "akty");
        Assert.Equal(0, Run("verify"));
    }
}
