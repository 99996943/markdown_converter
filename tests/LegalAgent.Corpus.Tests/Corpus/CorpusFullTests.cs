using System.Globalization;
using System.Text;
using LegalAgent.Corpus.Manifest;
using LegalAgent.Corpus.Output;
using LegalAgent.Corpus.Planning;

namespace LegalAgent.Corpus.Tests.Corpus;

/// <summary>
/// The whole committed corpus (research R10, FR-164): every document rebuilt in memory equals the committed files and
/// meets the quality metrics SC-022 – SC-026; runs when <c>LEGALAGENT_CORPUS_FULL</c> is set (CI). With
/// <c>LEGALAGENT_CORPUS_REPORT</c> set to a file path, the per-document measurement is written there as a table.
/// </summary>
[Trait("Category", "CorpusFull")]
public sealed class CorpusFullTests
{
    private const double MinWordCompleteness = 0.995;
    private const double MinHeadingRecall = 0.98;
    private const double MaxFalseHeadingShare = 0.01;
    private const double MinListRecall = 0.98;
    private const double MinCellAgreement = 0.98;

    private static void SkipUnlessEnabled()
    {
        if (Environment.GetEnvironmentVariable("LEGALAGENT_CORPUS_FULL") is null)
        {
            Assert.Skip("LEGALAGENT_CORPUS_FULL is not set.");
        }
    }

    [Fact]
    public void Corpus_HasTheRequestedNumberOfDocumentsPerType_WithinThePageRange()
    {
        SkipUnlessEnabled();
        RunParameters parameters = CorpusSampleTests.RecordedParameters();
        Manifest.Manifest manifest = ManifestWriter.Read(File.ReadAllText(Path.Combine(CorpusSampleTests.RepoRoot(), "corpus", "manifest.json")));

        foreach (string type in new[] { "regulaminy", "taryfy", "procedury" })
        {
            var documents = manifest.Documents.Where(d => d.Type == type && !d.Id.StartsWith("ZAT-", StringComparison.Ordinal) && !d.Id.Contains("-w", StringComparison.Ordinal)).ToList();
            Assert.Equal(parameters.DocumentsPerType, documents.Count);
            Assert.All(documents, d => Assert.InRange(d.Pages, parameters.Pages.Min, parameters.Pages.Max));
            Assert.All(documents, d => Assert.True(File.Exists(Path.Combine(CorpusSampleTests.RepoRoot(), "corpus", d.Pdf)), d.Pdf));
        }
    }

    [Fact]
    public async Task EveryDocument_EqualsTheCommittedFiles_AndMeetsTheQualityMetrics()
    {
        SkipUnlessEnabled();
        RunParameters parameters = CorpusSampleTests.RecordedParameters();
        CorpusPlan plan = CorpusGenerator.Plan(parameters, CorpusSampleTests.Options());
        string corpus = Path.Combine(CorpusSampleTests.RepoRoot(), "corpus");

        var failures = new List<string>();
        var details = new List<string>();
        var table = new StringBuilder("| Dokument | Układ | Słowa | Spoza PDF | Kolejność | Nagłówki | Fałszywe | Listy | Wiersze | Tabele GFM | Komórki |\n|---|---|---|---|---|---|---|---|---|---|---|\n");
        foreach (DocumentPlan doc in plan.Documents)
        {
            GeneratedDocument built = await CorpusGenerator.BuildDocumentAsync(parameters, doc.Id, CorpusSampleTests.Options(), TestContext.Current.CancellationToken);
            if (!built.Pdf.SequenceEqual(await File.ReadAllBytesAsync(Path.Combine(corpus, built.Entry.Pdf), TestContext.Current.CancellationToken)))
            {
                failures.Add($"{doc.Id}: PDF różni się od odtworzonego");
            }

            if (!CorpusWriter.TextBytes(built.Markdown).SequenceEqual(await File.ReadAllBytesAsync(Path.Combine(corpus, built.Entry.Markdown), TestContext.Current.CancellationToken)))
            {
                failures.Add($"{doc.Id}: Markdown różni się od wyniku biblioteki");
            }

            QualityReport q = QualityMetrics.Measure(doc.Id, built.Fit.Typeset.Truth, built.Markdown);
            details.AddRange(q.Failures);
            table.Append(CultureInfo.InvariantCulture, $"| {doc.Id} | {doc.Layout} | {q.WordCompleteness:P2} | {q.ExtraWords} | {q.ReadingOrder:P1} | {q.HeadingRecall:P1} | {q.FalseHeadingShare:P1} | {q.ListRecall:P1} | {q.RowsIntact:P1} | {q.TablesAsSingleGfm:P0} | {q.CellAgreement:P1} |\n");

            failures.AddRange(ThresholdFailures(doc, q));
        }

        if (Environment.GetEnvironmentVariable("LEGALAGENT_CORPUS_REPORT") is { } report)
        {
            await File.WriteAllTextAsync(report, table.ToString() + "\n" + string.Join("\n", failures) + "\n\n## Szczegóły\n\n" + string.Join("\n", details) + "\n", TestContext.Current.CancellationToken);
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// SC-028 and the manifest guarantees: versions of one designation have continuous, disjoint periods; every
    /// <c>previousVersion</c> and <c>contradictions[].with</c> names a document of the manifest.
    /// </summary>
    [Fact]
    public async Task Manifest_VersionsAreContinuous_AndReferencesExist()
    {
        SkipUnlessEnabled();
        string path = Path.Combine(CorpusSampleTests.RepoRoot(), "corpus", "manifest.json");
        Manifest.Manifest manifest = ManifestWriter.Read(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        var ids = manifest.Documents.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);

        foreach (ManifestDocument doc in manifest.Documents)
        {
            Assert.True(doc.PreviousVersion is null || ids.Contains(doc.PreviousVersion), $"{doc.Id}: previousVersion {doc.PreviousVersion}");
            Assert.All(doc.Contradictions ?? [], c => Assert.True(ids.Contains(c.With), $"{doc.Id}: contradiction with {c.With}"));
        }

        foreach (IGrouping<string?, ManifestDocument> versions in manifest.Documents.Where(d => d.Designation is not null && d.Type != "akty" && d.Poison is null).GroupBy(d => d.Designation))
        {
            var ordered = versions.OrderBy(d => d.Version).ToList();
            for (int k = 0; k + 1 < ordered.Count; k++)
            {
                Assert.Equal(ordered[k + 1].ValidFrom, ordered[k].ValidTo?.AddDays(1));
                Assert.Equal(ordered[k].Id, ordered[k + 1].PreviousVersion);
            }
        }
    }

    /// <summary>
    /// SC-023 and SC-028 for poisoned documents: at least the requested number per (type, kind) pair, each imitating an
    /// existing document of its type, and every poison text verbatim in the document's Markdown (after unescaping).
    /// </summary>
    [Fact]
    public async Task PoisonedDocuments_ImitateExistingDocuments_AndTheirTextsAreInTheMarkdown()
    {
        SkipUnlessEnabled();
        RunParameters parameters = CorpusSampleTests.RecordedParameters();
        string corpus = Path.Combine(CorpusSampleTests.RepoRoot(), "corpus");
        Manifest.Manifest manifest = ManifestWriter.Read(await File.ReadAllTextAsync(Path.Combine(corpus, "manifest.json"), TestContext.Current.CancellationToken));
        var byId = manifest.Documents.ToDictionary(d => d.Id, StringComparer.Ordinal);

        foreach (PoisonQuota quota in parameters.Poison)
        {
            foreach (string type in new[] { "regulaminy", "taryfy", "procedury" })
            {
                int count = manifest.Documents.Count(d => d.Type == type && d.Poison?.Kind == quota.Kind);
                Assert.True(count >= quota.PerType, $"{type} × {quota.Kind}: {count}");
            }
        }

        var failures = new List<string>();
        foreach (ManifestDocument doc in manifest.Documents.Where(d => d.Poison is not null))
        {
            Assert.True(byId.TryGetValue(doc.Poison!.Imitates, out ManifestDocument? imitated), $"{doc.Id}: imitates {doc.Poison.Imitates}");
            Assert.Equal(imitated!.Type, doc.Type);
            string markdown = Flat(await File.ReadAllTextAsync(Path.Combine(corpus, doc.Markdown), TestContext.Current.CancellationToken));
            foreach (PoisonPlace place in doc.Poison.Places)
            {
                if (!markdown.Contains(Flat(place.Text), StringComparison.Ordinal))
                {
                    failures.Add($"{doc.Id} ({place.Element}, s. {place.Page}): brak tekstu „{place.Text}”");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>Markdown or text without escapes, emphasis markers, page markers and line breaks; whitespace collapsed.</summary>
    internal static string Flat(string text)
    {
        string plain = System.Text.RegularExpressions.Regex.Replace(text, @"<!-- page: \d+ -->", " ");
        plain = System.Text.RegularExpressions.Regex.Replace(plain, @"\\(.)", "$1");
        plain = plain.Replace("**", string.Empty, StringComparison.Ordinal);
        return string.Join(' ', plain.Split((char[])[' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>The thresholds of SC-022 – SC-026 that <paramref name="q"/> misses, one message per metric.</summary>
    internal static List<string> ThresholdFailures(DocumentPlan doc, QualityReport q)
    {
        var failures = new List<string>();
        void Check(bool ok, string what)
        {
            if (!ok)
            {
                failures.Add($"{doc.Id} ({doc.Layout}): {what}");
            }
        }

        Check(q.WordCompleteness >= MinWordCompleteness, $"SC-022 kompletność słów {q.WordCompleteness:P2}");
        Check(q.ExtraWords == 0, $"SC-022 słowa spoza PDF: {q.ExtraWords} ({string.Join(", ", q.ExtraWordSamples.Take(5))})");
        Check(q.HeadingRecall >= MinHeadingRecall, $"SC-024 nagłówki {q.HeadingRecall:P1}");
        Check(q.FalseHeadingShare <= MaxFalseHeadingShare, $"SC-024 fałszywe nagłówki {q.FalseHeadingShare:P1}");
        Check(q.ListRecall >= MinListRecall, $"SC-025 listy {q.ListRecall:P1}");
        if (doc.Type == "taryfy")
        {
            Check(q.RowsIntact >= 1.0, $"SC-026 stawki w wierszu {q.RowsIntact:P1}");
            Check(q.TablesAsSingleGfm >= 1.0, $"SC-026 tabele jako jedna GFM {q.TablesAsSingleGfm:P0}");
            Check(q.CellAgreement >= MinCellAgreement, $"SC-026 komórki {q.CellAgreement:P1}");
        }

        return failures;
    }
}
