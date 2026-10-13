using System.Runtime.CompilerServices;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Corpus;

/// <summary>
/// T094 — every document of the public corpus converts to its reviewed golden Markdown: the acts in <c>Corpus/acts</c>
/// (PDFs committed) and the synthetic banking documents generated in memory by <see cref="BankingCorpusGenerator"/>
/// (golden files in <c>Corpus/banking</c>). Run with <c>UPDATE_GOLDEN=1</c> to rewrite the golden files, then review
/// the diff (see <c>Corpus/REVIEW.md</c>).
/// </summary>
public sealed class GoldenTests
{
    private static string CorpusDirectory([CallerFilePath] string thisFile = "") => Path.GetDirectoryName(thisFile)!;

    public static TheoryData<string> Acts()
    {
        var data = new TheoryData<string>();
        foreach (string pdf in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Corpus", "acts"), "*.pdf").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(pdf));
        }

        return data;
    }

    public static TheoryData<string> Banking()
    {
        var data = new TheoryData<string>();
        foreach ((string name, byte[] _) in BankingCorpusGenerator.Documents())
        {
            data.Add(name);
        }

        return data;
    }

    private static async Task<PdfConversionResult> ConvertAsync(byte[] pdf, string sourceId)
    {
        using var stream = new MemoryStream(pdf);
        return await PdfMarkdownConverter.CreateDefault()
            .ConvertAsync(stream, new PdfConversionRequest { SourceId = sourceId }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(Acts))]
    public async Task Act_MatchesGolden(string name)
    {
        byte[] pdf = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Corpus", "acts", name + ".pdf"), TestContext.Current.CancellationToken);

        PdfConversionResult result = await ConvertAsync(pdf, name + ".pdf");

        Assert.True(result.IsComplete);
        GoldenFile.AssertMatches(result.Markdown, Path.Combine(CorpusDirectory(), "acts", name + ".expected.md"));
    }

    // The counts pin the FTN001 warnings of every act (spec 011 FR-810); a changed count must be recorded in the
    // spec handoff, never just updated.
    private static readonly (string Name, int Expected)[] OrphanFootnoteCounts =
    [
        ("dz-u-2020-287-prawa-konsumenta", 2),
        ("dz-u-2024-1497-kredyt-konsumencki", 5),
        ("dz-u-2024-1646-prawo-bankowe", 24),
        ("dz-u-2024-30-uslugi-platnicze", 10),
        ("dz-u-2026-1298-obwieszczenie-msz", 6),
        ("ustawa-o-sluzbie-cywilnej", 11),
    ];

    public static TheoryData<string, int> ActOrphanFootnoteCounts()
    {
        var data = new TheoryData<string, int>();
        foreach ((string name, int expected) in OrphanFootnoteCounts)
        {
            data.Add(name, expected);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ActOrphanFootnoteCounts))]
    public async Task Act_OrphanFootnoteWarnings_MatchExpectedCount(string name, int expected)
    {
        byte[] pdf = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Corpus", "acts", name + ".pdf"), TestContext.Current.CancellationToken);

        PdfConversionResult result = await ConvertAsync(pdf, name + ".pdf");

        Assert.Equal(expected, result.Report.Warnings.Count(w => w.Code == "FTN001_OrphanFootnote"));
    }

    [Fact]
    public void Act_OrphanFootnoteWarnings_CoverEveryAct()
    {
        string[] acts = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Corpus", "acts"), "*.pdf")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal)
            .ToArray()!;

        Assert.Equal(acts, OrphanFootnoteCounts.Select(c => c.Name).Order(StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [MemberData(nameof(Banking))]
    public async Task BankingDocument_MatchesGolden(string name)
    {
        byte[] pdf = BankingCorpusGenerator.Documents().Single(d => d.Name == name).Pdf;

        PdfConversionResult result = await ConvertAsync(pdf, name + ".pdf");

        Assert.True(result.IsComplete);
        GoldenFile.AssertMatches(result.Markdown, Path.Combine(CorpusDirectory(), "banking", name + ".expected.md"));
    }
}
