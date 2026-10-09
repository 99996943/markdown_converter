using LegalAgent.Corpus.Manifest;
using LegalAgent.Corpus.Output;
using LegalAgent.Corpus.Planning;

namespace LegalAgent.Corpus.Tests.Corpus;

/// <summary>
/// Sample of the committed corpus (research R10): the first document of every (type, layout) pair of the recorded run
/// is rebuilt in memory and must equal the committed PDF, Markdown and manifest entry (FR-165) and meet the quality
/// metrics SC-022 – SC-026 (FR-164).
/// </summary>
public sealed class CorpusSampleTests
{
    internal static string RepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LegalAgent.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    internal static RunParameters RecordedParameters() => RunParameters.Load(Path.Combine(RepoRoot(), "corpus", "przebieg.json"));

    internal static CorpusGeneratorOptions Options() => new() { BaseDirectory = RepoRoot() };

    /// <summary>
    /// The first document of every (type, layout) pair, in plan order, and every version of the first versioned
    /// document (research R10).
    /// </summary>
    internal static IReadOnlyList<DocumentPlan> Sample(CorpusPlan plan)
    {
        var sample = plan.Documents.GroupBy(d => (d.Type, d.Layout)).Select(g => g.First()).ToList();
        if (plan.Documents.FirstOrDefault(d => d.PreviousVersionId is not null) is { } versioned)
        {
            sample.AddRange(plan.Documents.Where(d => d.Designation == versioned.Designation && !sample.Contains(d)));
        }

        // The first poisoned document of every (type, kind) pair.
        sample.AddRange(plan.Documents.Where(d => d.Poison is not null).GroupBy(d => (d.Type, d.Poison!.Kind)).Select(g => g.First()));

        return sample;
    }

    [Fact]
    public async Task SampleDocuments_EqualTheCommittedFiles()
    {
        string corpus = Path.Combine(RepoRoot(), "corpus");
        string manifestPath = Path.Combine(corpus, "manifest.json");
        Assert.True(File.Exists(Path.Combine(corpus, "regulaminy", "REG-01.pdf")), "brak pliku corpus/regulaminy/REG-01.pdf");
        Assert.True(File.Exists(manifestPath), "brak pliku corpus/manifest.json");

        RunParameters parameters = RecordedParameters();
        CorpusPlan plan = CorpusGenerator.Plan(parameters, Options());
        IReadOnlyList<DocumentPlan> sample = Sample(plan);
        Assert.Contains(sample, d => d.Layout == "dwie-kolumny");
        Assert.Contains(sample, d => d.Layout == "tabela-dokument");
        Assert.Contains(sample, d => d.Layout == "taryfa-bez-siatki");

        Manifest.Manifest manifest = ManifestWriter.Read(await File.ReadAllTextAsync(manifestPath, TestContext.Current.CancellationToken));
        var quality = new List<string>();
        foreach (DocumentPlan doc in sample)
        {
            GeneratedDocument built = await CorpusGenerator.BuildDocumentAsync(parameters, doc.Id, Options(), TestContext.Current.CancellationToken);

            string pdf = Path.Combine(corpus, built.Entry.Pdf);
            string md = Path.Combine(corpus, built.Entry.Markdown);
            Assert.True(File.Exists(pdf), "brak pliku corpus/" + built.Entry.Pdf);
            byte[] pdfOnDisk = await File.ReadAllBytesAsync(pdf, TestContext.Current.CancellationToken);
            byte[] mdOnDisk = await File.ReadAllBytesAsync(md, TestContext.Current.CancellationToken);
            Assert.True(built.Pdf.SequenceEqual(pdfOnDisk), "PDF różni się od odtworzonego: " + built.Entry.Pdf);
            Assert.True(CorpusWriter.TextBytes(built.Markdown).SequenceEqual(mdOnDisk), "Markdown różni się od wyniku biblioteki: " + built.Entry.Markdown);
            Assert.InRange(built.Entry.Pages, parameters.Pages.Min, parameters.Pages.Max);

            ManifestDocument entry = Assert.Single(manifest.Documents, d => d.Id == doc.Id);
            Assert.Equal(built.Entry with { Changes = null, Contradictions = null, Poison = null }, entry with { Changes = null, Contradictions = null, Poison = null });
            Assert.Equal(built.Entry.Changes ?? [], entry.Changes ?? []);
            Assert.Equal(built.Entry.Contradictions ?? [], entry.Contradictions ?? []);
            Assert.Equal(built.Entry.Poison is null, entry.Poison is null);
            if (built.Entry.Poison is { } poison)
            {
                Assert.Equal(poison with { Places = [] }, entry.Poison! with { Places = [] });
                Assert.Equal(poison.Places, entry.Poison!.Places);
            }
            Assert.False(string.IsNullOrEmpty(entry.Title));
            Assert.NotNull(entry.Designation);
            Assert.NotNull(entry.ValidFrom);

            quality.AddRange(CorpusFullTests.ThresholdFailures(doc, QualityMetrics.Measure(doc.Id, built.Fit.Typeset.Truth, built.Markdown)));
        }

        Assert.True(quality.Count == 0, string.Join("\n", quality));
    }
}
