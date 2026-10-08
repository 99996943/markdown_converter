using System.Text;
using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Model;
using UglyToad.PdfPig;

namespace LegalAgent.PdfParser.Tests.Corpus;

/// <summary>
/// T095 — quality metrics of the success criteria measured against reference data independent of the pipeline:
/// <c>*.artifacts.txt</c> (running header/footer patterns read off the PDFs), <c>*.structure.txt</c> (legal unit
/// designations from PdfPig's raw content-order text, see <c>REVIEW.md</c>) and the raw text itself.
/// </summary>
public sealed partial class QualityMetricsTests
{
    private static readonly string ActsDirectory = Path.Combine(AppContext.BaseDirectory, "Corpus", "acts");

    public static TheoryData<string> Acts() => GoldenTests.Acts();

    private static async Task<PdfConversionResult> ConvertAsync(string name)
    {
        await using FileStream stream = File.OpenRead(Path.Combine(ActsDirectory, name + ".pdf"));
        return await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static Regex[] ArtifactPatterns(string name) =>
        File.ReadAllLines(Path.Combine(ActsDirectory, name + ".artifacts.txt"))
            .Where(l => l.Length > 0)
            .Select(l => new Regex(l, RegexOptions.CultureInvariant))
            .ToArray();

    /// <summary>SC-001: running headers, footers and page numbers never reach the Markdown.</summary>
    [Theory]
    [MemberData(nameof(Acts))]
    public async Task RunningArtifactsAreRemoved_SC001(string name)
    {
        PdfConversionResult result = await ConvertAsync(name);

        // Patterns are anchored to whole PDF lines; in Markdown an artifact could only survive inside a paragraph.
        Regex[] patterns = ArtifactPatterns(name)
            .Select(p => new Regex(p.ToString().TrimStart('^').TrimEnd('$'), RegexOptions.CultureInvariant))
            .ToArray();
        int leaked = patterns.Sum(p => p.Count(result.Markdown));

        Assert.True(leaked == 0, $"{leaked} running artifacts left in {name} (pages: {result.Report.PageCount}).");
    }

    /// <summary>SC-002: at least 99.5% of the words of the PDF (without artifacts) are in the Markdown.</summary>
    [Theory]
    [MemberData(nameof(Acts))]
    public async Task WordsAreKept_SC002(string name)
    {
        PdfConversionResult result = await ConvertAsync(name);
        Dictionary<string, int> reference = Count(Words(RawText(name, ArtifactPatterns(name))));
        Dictionary<string, int> output = Count(Words(Unescape(result.Markdown)));

        // PdfPig's reference segmentation is imperfect both ways (kerned words split, unspaced words glued), so a word of
        // two or more characters missing as a word still counts when it occurs in the output with spaces removed.
        string spaceless = string.Concat(Words(Unescape(result.Markdown)));
        Dictionary<string, int> lost = reference.ToDictionary(
            kv => kv.Key,
            kv =>
            {
                int exact = Math.Min(kv.Value, output.GetValueOrDefault(kv.Key));
                int found = kv.Key.Length > 1 && exact < kv.Value ? Math.Min(kv.Value - exact, Occurrences(spaceless, kv.Key)) : 0;
                return kv.Value - exact - found;
            },
            StringComparer.Ordinal);
        int total = reference.Values.Sum();
        int kept = total - lost.Values.Sum();
        double recall = (double)kept / total;

        TestContext.Current.SendDiagnosticMessage($"{name}: word recall {recall:P2} ({kept}/{total})");
        Assert.True(recall >= 0.995, $"{name}: word recall {recall:P2} ({kept}/{total}); most missing: {MissingWords(lost)}");
    }

    /// <summary>SC-003: at least 95% of the legal units are headings and at most 2% of unit headings are false.</summary>
    [Theory]
    [MemberData(nameof(Acts))]
    public async Task LegalUnitsBecomeHeadings_SC003(string name)
    {
        PdfConversionResult result = await ConvertAsync(name);
        Dictionary<string, int> reference = Count(File.ReadAllLines(Path.Combine(ActsDirectory, name + ".structure.txt"))
            .Where(l => l.Length > 0)
            .Select(Designation));
        Dictionary<string, int> output = Count(Sections(result.Document.Sections)
            .Where(s => s.Designation is not null)
            .Select(s => Designation(s.Designation!)));

        int referenceTotal = reference.Values.Sum();
        int outputTotal = output.Values.Sum();
        int matched = reference.Sum(kv => Math.Min(kv.Value, output.GetValueOrDefault(kv.Key)));
        double recall = (double)matched / referenceTotal;
        double falseRate = outputTotal == 0 ? 0 : (double)(outputTotal - matched) / outputTotal;

        TestContext.Current.SendDiagnosticMessage($"{name}: unit recall {recall:P2}, false {falseRate:P2}");
        Assert.True(recall >= 0.95, $"{name}: legal unit recall {recall:P2} ({matched}/{referenceTotal}); missing: {Missing(reference, output)}");
        Assert.True(falseRate <= 0.02, $"{name}: false legal unit headings {falseRate:P2}; extra: {Missing(output, reference)}");
    }

    private static string MissingWords(Dictionary<string, int> lost) =>
        string.Join(", ", lost
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(25)
            .Select(kv => $"{kv.Key}×{kv.Value}"));

    private static string Missing(Dictionary<string, int> from, Dictionary<string, int> other) =>
        string.Join(", ", from.Where(kv => kv.Value > other.GetValueOrDefault(kv.Key)).Select(kv => kv.Key).Take(15));

    private static IEnumerable<Section> Sections(IEnumerable<Section> sections) =>
        sections.SelectMany(s => new[] { s }.Concat(Sections(s.Children)));

    /// <summary>Normalised designation: single spaces, superscript digits as plain digits.</summary>
    private static string Designation(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            int superscript = "⁰¹²³⁴⁵⁶⁷⁸⁹".IndexOf(c, StringComparison.Ordinal);
            sb.Append(superscript >= 0 ? (char)('0' + superscript) : c);
        }

        return Spaces().Replace(sb.ToString().Trim(), " ");
    }

    /// <summary>PdfPig's raw words as lines, without artifact lines; hyphenated line ends are joined (FR-012).</summary>
    private static string RawText(string name, Regex[] artifacts)
    {
        using PdfDocument document = PdfDocument.Open(Path.Combine(ActsDirectory, name + ".pdf"));
        var lines = new List<string>();
        foreach (UglyToad.PdfPig.Content.Page page in document.GetPages())
        {
            // PdfPig's own gap-based word segmentation, grouped into lines by baseline, top to bottom.
            var pageLines = new List<List<UglyToad.PdfPig.Content.Word>>();
            foreach (UglyToad.PdfPig.Content.Word word in page.GetWords().OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left))
            {
                List<UglyToad.PdfPig.Content.Word>? line = pageLines.LastOrDefault();
                if (line is null || Math.Abs(line[0].BoundingBox.Bottom - word.BoundingBox.Bottom) > 2)
                {
                    pageLines.Add([word]);
                }
                else
                {
                    line.Add(word);
                }
            }

            lines.AddRange(pageLines
                .Select(l => string.Join(' ', l.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)).Trim())
                .Where(l => l.Length > 0 && !artifacts.Any(a => a.IsMatch(l))));
        }

        var sb = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            bool joins = line.EndsWith('-') && i + 1 < lines.Count && lines[i + 1].Length > 0 && char.IsLower(lines[i + 1][0]);
            sb.Append(joins ? line[..^1] : line + "\n");
        }

        return sb.ToString();
    }

    private static int Occurrences(string text, string value)
    {
        int count = 0;
        for (int i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string Unescape(string markdown) => markdown.Replace("\\", string.Empty, StringComparison.Ordinal);

    private static IEnumerable<string> Words(string text) =>
        WordPattern().Matches(text.Normalize(NormalizationForm.FormC)).Select(m => m.Value.ToLowerInvariant());

    private static Dictionary<string, int> Count(IEnumerable<string> items)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string item in items)
        {
            counts[item] = counts.GetValueOrDefault(item) + 1;
        }

        return counts;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Spaces();

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();

    public static TheoryData<string> BankingWithLists() => ["regulamin-rachunku", "regulamin-dwie-kolumny"];

    public static TheoryData<string> BankingWithTables() => ["taryfa-z-siatka", "taryfa-bez-siatki"];

    private static async Task<PdfConversionResult> ConvertBankingAsync(string name)
    {
        using var stream = new MemoryStream(Fixtures.BankingCorpusGenerator.Documents().Single(d => d.Name == name).Pdf);
        return await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static IEnumerable<ContentBlock> AllBlocks(LegalDocument document) =>
        document.Preamble.Concat(Sections(document.Sections).SelectMany(s => s.Blocks));

    private static IEnumerable<(string Label, int Depth)> ListItems(IEnumerable<ContentBlock> blocks, int depth) =>
        blocks.OfType<ListBlock>().SelectMany(list => list.Items.SelectMany(item =>
            new[] { (item.Label, depth) }.Concat(ListItems(item.Children, depth + 1))));

    /// <summary>SC-004: at least 95% of list items keep their label and nesting level.</summary>
    [Theory]
    [MemberData(nameof(BankingWithLists))]
    public async Task ListItemsKeepLabelAndLevel_SC004(string name)
    {
        PdfConversionResult result = await ConvertBankingAsync(name);
        var truth = Fixtures.BankingCorpusGenerator.Truth(name).ListItems.Select(i => (i.Label, i.Depth)).ToList();
        var items = ListItems(AllBlocks(result.Document), 0).ToList();

        double accuracy = (double)LongestCommonSubsequence(truth, items) / truth.Count;

        TestContext.Current.SendDiagnosticMessage($"{name}: list accuracy {accuracy:P2}");
        Assert.True(accuracy >= 0.95, $"{name}: list label+level accuracy {accuracy:P2} ({items.Count} items found, {truth.Count} expected).");
    }

    /// <summary>SC-005: every amount stays in the row of its service, and at least 80% of tables are full GFM tables.</summary>
    [Theory]
    [MemberData(nameof(BankingWithTables))]
    public async Task AmountsStayWithTheirService_SC005(string name)
    {
        PdfConversionResult result = await ConvertBankingAsync(name);
        TableBlock[] tables = AllBlocks(result.Document).OfType<TableBlock>().ToArray();
        string[][] rows = tables
            .SelectMany(t => (t.Header is null ? t.Rows : t.Rows.Prepend(t.Header)))
            .Select(r => r.Cells.Select(c => Spaces().Replace(string.Concat(c.Inlines.OfType<TextRun>().Select(t => t.Text)), " ").Trim()).ToArray())
            .ToArray();

        var lost = Fixtures.BankingCorpusGenerator.Truth(name).TableRows
            .Where(t => !rows.Any(r => r.Any(c => c.Contains(t.Service, StringComparison.Ordinal)) && r.Contains(t.Fee)))
            .Select(t => $"{t.Service} = {t.Fee}")
            .ToList();
        double gfm = tables.Length == 0 ? 0 : (double)tables.Count(t => !t.IsFallback) / tables.Length;

        Assert.True(lost.Count == 0, $"{name}: amounts not in their service row: {string.Join("; ", lost)}");
        Assert.True(gfm >= 0.8, $"{name}: full GFM tables {gfm:P0} of {tables.Length}.");
    }

    // ---------------------------------------------------------------- spec 002 (T036): table-document metrics

    private const string TableDocument = "regulamin-promocji-tabela";

    private static async Task<PdfConversionResult> ConvertTableDocumentAsync()
    {
        using var stream = new MemoryStream(Fixtures.BankingCorpusGenerator.Pdf(TableDocument));
        return await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Words of the PDF in content order, the column-name row removed, split into the section-name column (left cells
    /// of pages 2 and later) and everything else (the cover and the right cells).
    /// </summary>
    private static (List<string> Names, List<string> Content) TableDocumentPdfWords()
    {
        HashSet<string> header = Fixtures.BankingCorpusGenerator.Truth(TableDocument).HeaderRowWords.Select(w => w.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        using PdfDocument document = PdfDocument.Open(Fixtures.BankingCorpusGenerator.Pdf(TableDocument));
        var names = new List<string>();
        var content = new List<string>();
        foreach (UglyToad.PdfPig.Content.Page page in document.GetPages())
        {
            foreach (UglyToad.PdfPig.Content.Word word in page.GetWords().Where(w => !PageNumberOfTotal().IsMatch(w.Text) && !IsSubBulletMarker(w)))
            {
                // The running page number ("2/5") is an artifact, removed by design (SC-001).
                // The column-name row stands on pages 2, 3 and 5: drop every occurrence of its words.
                foreach (string w in Words(word.Text).Where(w => !header.Contains(w)))
                {
                    (page.Number is >= 2 and <= 5 && word.BoundingBox.Left < 180 ? names : content).Add(w);
                }
            }
        }

        return (names, content);
    }

    /// <summary>Words of the Markdown without page markers, in output order: the headings (section names) and the rest.</summary>
    private static (List<string> Names, List<string> Content) TableDocumentOutputWords(string markdown)
    {
        var names = new List<string>();
        var content = new List<string>();
        foreach (string line in PageMarkerComment().Replace(markdown, " ").Split('\n'))
        {
            bool section = line.StartsWith("## ", StringComparison.Ordinal);
            (section ? names : content).AddRange(Words(Unescape(section ? line[3..] : line.TrimStart('#'))));
        }

        
        return (names, content);
    }

    /// <summary>The "o" sub-bullet marker (set in a monospace font) becomes Markdown list syntax, not a word.</summary>
    private static bool IsSubBulletMarker(UglyToad.PdfPig.Content.Word word) =>
        word.Text == "o" && word.Letters.Any(l => (l.FontName ?? string.Empty).Contains("Mono", StringComparison.Ordinal));

    [GeneratedRegex(@"^\d+/\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex PageNumberOfTotal();

    [GeneratedRegex(@"<!-- page: \d+ -->", RegexOptions.CultureInvariant)]
    private static partial Regex PageMarkerComment();

    /// <summary>SC-010: every word of the PDF (except the column-name row) is in the Markdown, in order, and nothing else is.</summary>
    [Fact]
    public async Task TableDocument_KeepsEveryWordInOrderAndAddsNone_SC010()
    {
        PdfConversionResult result = await ConvertTableDocumentAsync();
        (List<string> pdfNames, List<string> pdfContent) = TableDocumentPdfWords();
        (List<string> outNames, List<string> outContent) = TableDocumentOutputWords(result.Markdown);

        AssertSameWords("section names", pdfNames, outNames);
        AssertSameWords("content", pdfContent, outContent);
    }

    private static void AssertSameWords(string what, List<string> pdf, List<string> output)
    {
        int common = 0;
        while (common < pdf.Count && common < output.Count && pdf[common] == output[common])
        {
            common++;
        }

        string Context(List<string> w) => string.Join(' ', w.Skip(Math.Max(0, common - 3)).Take(10));
        Assert.True(
            pdf.Count == output.Count && common == pdf.Count,
            $"{what}: PDF words ({pdf.Count}) and Markdown words ({output.Count}) differ from word {common}: PDF «{Context(pdf)}» / Markdown «{Context(output)}».");
    }

    private static string[] HeadingLines(string markdown) => markdown.Split('\n').Where(l => l.StartsWith('#')).ToArray();

    /// <summary>SC-011: the section names are the level-2 headings, whole and in order, each before its content.</summary>
    [Fact]
    public async Task TableDocument_SectionNamesAreLevel2HeadingsBeforeTheirContent_SC011()
    {
        PdfConversionResult result = await ConvertTableDocumentAsync();
        IReadOnlyList<string> names = Fixtures.BankingCorpusGenerator.Truth(TableDocument).SectionNames;

        string[] level2 = HeadingLines(result.Markdown).Where(l => l.StartsWith("## ", StringComparison.Ordinal)).Select(l => l[3..].Trim()).ToArray();
        Assert.Equal(names, level2);

        // The first words of the content of each cell stand after its heading and before the next heading.
        string[] firstContent =
        [
            "Promocję organizuje", "W promocji mogą uczestniczyć", "Bank – Bank Przykładowy", "Jeśli spełnisz warunki promocji",
            "Jeśli spełnisz wszystkie warunki", "Reklamacje związane z uczestnictwem",
        ];
        int previous = -1;
        for (int i = 0; i < names.Count; i++)
        {
            int heading = result.Markdown.IndexOf("## " + names[i] + "\n", StringComparison.Ordinal);
            int content = result.Markdown.IndexOf(firstContent[i], StringComparison.Ordinal);
            int next = i + 1 < names.Count ? result.Markdown.IndexOf("## " + names[i + 1] + "\n", StringComparison.Ordinal) : int.MaxValue;
            Assert.True(heading > previous && heading < content && content < next, $"section «{names[i]}»: heading {heading}, content {content}, next heading {next}");
            previous = heading;
        }
    }

    /// <summary>SC-012: the only headings are the title and the section names.</summary>
    [Fact]
    public async Task TableDocument_OnlyTheTitleAndSectionNamesAreHeadings_SC012()
    {
        PdfConversionResult result = await ConvertTableDocumentAsync();
        IReadOnlyList<string> names = Fixtures.BankingCorpusGenerator.Truth(TableDocument).SectionNames;

        string[] headings = HeadingLines(result.Markdown);
        Assert.Equal(names.Count + 1, headings.Length);
        Assert.StartsWith("# ", headings[0], StringComparison.Ordinal);
        Assert.Equal(names.Select(n => "## " + n), headings.Skip(1));
    }

    /// <summary>SC-013: no GFM tables, no " \| " separators, no TBL001 and no tables in the report.</summary>
    [Fact]
    public async Task TableDocument_HasNoTables_SC013()
    {
        PdfConversionResult result = await ConvertTableDocumentAsync();

        Assert.DoesNotContain(result.Markdown.Split('\n'), l => l.TrimStart().StartsWith('|'));
        Assert.DoesNotContain(" \\| ",result.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Report.Warnings, w => w.Code.StartsWith("TBL001", StringComparison.Ordinal));
        Assert.Equal(0, result.Report.TableCount);
    }

    /// <summary>SC-014: cells crossing a page boundary are one fragment, with the page marker inside.</summary>
    [Theory]
    [InlineData("Warunki promocyjne terminala", "w każdej placówce Banku.")]
    [InlineData("Jeśli spełnisz wszystkie warunki", "w aplikacji mobilnej.")]
    public async Task TableDocument_CellCrossingAPageIsOneFragment_SC014(string phrase, string end)
    {
        PdfConversionResult result = await ConvertTableDocumentAsync();
        string[] blocks = result.Markdown.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        string block = Assert.Single(blocks, b => b.Contains(phrase, StringComparison.Ordinal));
        Assert.Matches(@"<!-- page: \d+ -->", block);
        Assert.Contains(end, block, StringComparison.Ordinal);
    }

    /// <summary>SC-015: list labels and nesting depth of the right cells equal the ground truth.</summary>
    [Fact]
    public async Task TableDocument_ListLabelsAndDepthMatchTheTruth_SC015()
    {
        PdfConversionResult result = await ConvertTableDocumentAsync();
        var truth = Fixtures.BankingCorpusGenerator.Truth(TableDocument).ListItems.Select(i => (i.Label, i.Depth)).ToList();
        var items = ListItems(AllBlocks(result.Document), 0).ToList();

        Assert.Equal(truth, items);
    }

    private static int LongestCommonSubsequence(List<(string Label, int Depth)> a, List<(string Label, int Depth)> b)
    {
        var table = new int[a.Count + 1, b.Count + 1];
        for (int i = 1; i <= a.Count; i++)
        {
            for (int j = 1; j <= b.Count; j++)
            {
                table[i, j] = a[i - 1] == b[j - 1]
                    ? table[i - 1, j - 1] + 1
                    : Math.Max(table[i - 1, j], table[i, j - 1]);
            }
        }

        return table[a.Count, b.Count];
    }
}
