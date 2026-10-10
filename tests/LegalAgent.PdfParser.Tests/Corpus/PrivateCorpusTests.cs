using System.Globalization;
using System.Text;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Corpus;

/// <summary>
/// T094 — optional private corpus (documents that cannot be committed, e.g. real bank regulations): every <c>*.pdf</c>
/// in the directory named by <c>LEGALAGENT_PRIVATE_CORPUS</c> must convert completely, and must match its
/// <c>*.expected.md</c> when one lies next to it. Skipped when the variable is unset.
/// </summary>
/// <remarks>
/// Spec 007 (T006): with <c>LEGALAGENT_CORPUS_REPORT</c> set, the layout measures of every document are written as a
/// table to that file path (<c>1</c> means <c>layout-metrics.md</c> in the private directory). The untracked
/// <c>layout-labels.txt</c> next to the PDFs lists the documents of the hanging-label layout with their SC-080 limits
/// (<c>name;maxTbl001;maxPipeRows</c>); <see cref="LabelLayoutDocuments_MeetTheirLimits"/> checks them.
/// </remarks>
public sealed class PrivateCorpusTests
{
    private const string Variable = "LEGALAGENT_PRIVATE_CORPUS";
    private const string ReportVariable = "LEGALAGENT_CORPUS_REPORT";
    private const string LabelLayoutList = "layout-labels.txt";

    private static readonly Dictionary<string, Task<IReadOnlyList<Converted>>> Conversions = new(StringComparer.Ordinal);

    [Fact]
    public async Task PrivateDocuments_ConvertAndMatchTheirGoldens()
    {
        string directory = RequireDirectory();
        IReadOnlyList<Converted> documents = await ConvertAllAsync(directory);
        Assert.NotEmpty(documents);

        if (Environment.GetEnvironmentVariable(ReportVariable) is { Length: > 0 } report)
        {
            string path = report == "1" ? Path.Combine(directory, "layout-metrics.md") : report;
            await File.WriteAllTextAsync(path, MeasureTable(documents), TestContext.Current.CancellationToken);
        }

        foreach (Converted document in documents)
        {
            PdfConversionResult result = document.Result;
            Assert.True(result.IsComplete, $"{document.Name} converted incompletely.");

            // Spec 002 (SC-013): a table-document becomes sections — no GFM table rows, no fallback „ | ” rows.
            if (result.Report.TableDocuments.Count > 0)
            {
                string[] lines = result.Markdown.Split('\n');
                Assert.DoesNotContain(lines, l => l.StartsWith('|'));
                Assert.DoesNotContain(" \\| ", result.Markdown, StringComparison.Ordinal);
            }

            string expected = Path.ChangeExtension(document.Path, ".expected.md");
            if (File.Exists(expected))
            {
                GoldenFile.AssertMatches(result.Markdown, expected);
            }
        }
    }

    /// <summary>SC-080, SC-081 — hanging-label documents: TBL001 and fallback rows within limits, no „§ N” as text.</summary>
    [Fact]
    [Trait("Category", "Layout007")]
    public async Task LabelLayoutDocuments_MeetTheirLimits()
    {
        string directory = RequireDirectory();
        string list = Path.Combine(directory, LabelLayoutList);
        Assert.SkipUnless(File.Exists(list), $"{LabelLayoutList} is missing in {directory}.");

        IReadOnlyList<Converted> documents = await ConvertAllAsync(directory);
        var failures = new List<string>();
        foreach (string line in File.ReadAllLines(list))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string[] fields = line.Split(';');
            string name = fields[0].Trim();
            int maxTbl001 = int.Parse(fields[1], CultureInfo.InvariantCulture);
            int maxPipeRows = int.Parse(fields[2], CultureInfo.InvariantCulture);
            Converted? document = documents.FirstOrDefault(d => d.Name == name + ".pdf");
            if (document is null)
            {
                failures.Add($"{name}: no such PDF");
                continue;
            }

            LayoutMeasures m = LayoutMetrics.Measure(document.Result.Markdown, document.Result.Report);
            if (m.Tbl001 > maxTbl001)
            {
                failures.Add($"{name}: TBL001 {m.Tbl001} > {maxTbl001}");
            }

            if (m.PipeRows > maxPipeRows)
            {
                failures.Add($"{name}: fallback rows {m.PipeRows} > {maxPipeRows}");
            }

            if (m.ParagraphText > 0)
            {
                failures.Add($"{name}: „§ N” as text {m.ParagraphText}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private static string RequireDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable(Variable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(directory), $"{Variable} is not set.");
        Assert.True(Directory.Exists(directory), $"{Variable} points to a missing directory: {directory}");
        return directory!;
    }

    private static Task<IReadOnlyList<Converted>> ConvertAllAsync(string directory)
    {
        lock (Conversions)
        {
            if (!Conversions.TryGetValue(directory, out Task<IReadOnlyList<Converted>>? task))
            {
                task = ConvertDirectoryAsync(directory);
                Conversions[directory] = task;
            }

            return task;
        }
    }

    private static async Task<IReadOnlyList<Converted>> ConvertDirectoryAsync(string directory)
    {
        var documents = new List<Converted>();
        foreach (string pdf in Directory.GetFiles(directory, "*.pdf").Order(StringComparer.Ordinal))
        {
            await using FileStream stream = File.OpenRead(pdf);
            string name = Path.GetFileName(pdf);
            PdfConversionResult result = await PdfMarkdownConverter.CreateDefault()
                .ConvertAsync(stream, new PdfConversionRequest { SourceId = name }, CancellationToken.None);
            documents.Add(new Converted(pdf, name, result));
        }

        return documents;
    }

    private static string MeasureTable(IReadOnlyList<Converted> documents)
    {
        var table = new StringBuilder("| Dokument | TBL001 | Wiersze „ \\| ” | Etykiety poza listą | „§ N” jako tekst | Spis treści | „N/M” |\n|---|---|---|---|---|---|---|\n");
        foreach (Converted document in documents)
        {
            LayoutMeasures m = LayoutMetrics.Measure(document.Result.Markdown, document.Result.Report);
            table.Append(CultureInfo.InvariantCulture, $"| {document.Name} | {m.Tbl001} | {m.PipeRows} | {m.LooseLabelRows} | {m.ParagraphText} | {m.TocHeadings} | {m.PageFooters} |\n");
        }

        return table.ToString();
    }

    private sealed record Converted(string Path, string Name, PdfConversionResult Result);
}
