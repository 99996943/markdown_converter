using System.Globalization;
using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Corpus;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Rendering;

/// <summary>
/// The invariants of contracts/markdown-output.md checked on the Markdown of every corpus document (acts and synthetic
/// banking documents): one blank line at most, no trailing spaces, footnotes numbered 1..n with one definition each,
/// heading levels growing by at most one, page markers never going back, and markers removable without other changes.
/// </summary>
public sealed partial class MarkdownInvariantsTests
{
    public static TheoryData<string> Documents()
    {
        var data = new TheoryData<string>();
        foreach (string act in GoldenTests.Acts().Select(row => row.Data))
        {
            data.Add("acts/" + act);
        }

        foreach (string doc in GoldenTests.Banking().Select(row => row.Data))
        {
            data.Add("banking/" + doc);
        }

        return data;
    }

    private const string TableDocument = "regulamin-promocji-tabela";

    private static async Task<(string Markdown, string WithoutMarkers)> ConvertAsync(string id)
    {
        byte[] pdf = id.StartsWith("acts/", StringComparison.Ordinal)
            ? await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Corpus", "acts", id[5..] + ".pdf"), TestContext.Current.CancellationToken)
            : BankingCorpusGenerator.Pdf(id[8..]);
        var converter = PdfMarkdownConverter.CreateDefault();
        using var first = new MemoryStream(pdf);
        PdfConversionResult result = await converter.ConvertAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        using var second = new MemoryStream(pdf);
        PdfConversionResult plain = await converter.ConvertAsync(
            second,
            new PdfConversionRequest { ConfigureOptions = o => o.Rendering.PageMarkers = false },
            TestContext.Current.CancellationToken);
        return (result.Markdown, plain.Markdown);
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public async Task CorpusMarkdown_KeepsTheContractInvariants(string id)
    {
        (string markdown, string withoutMarkers) = await ConvertAsync(id);
        string[] lines = markdown.Split('\n');

        // 1. No two consecutive blank lines; 2. no line ends with a space; LF only and a single final newline.
        Assert.DoesNotContain("\n\n\n", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain(lines, l => l.EndsWith(' '));
        Assert.DoesNotContain('\r', markdown);
        Assert.EndsWith("\n", markdown, StringComparison.Ordinal);
        Assert.False(markdown.EndsWith("\n\n", StringComparison.Ordinal));

        // 3. Every [^n] has exactly one definition, numbering is continuous from 1.
        int[] definitions = lines.Select(l => FootnoteDefinition().Match(l)).Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
        int[] references = FootnoteReference().Matches(markdown)
            .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Distinct().ToArray();
        Assert.Equal(Enumerable.Range(1, definitions.Length), definitions.Order());
        Assert.All(references, r => Assert.Contains(r, definitions));

        // 4. A heading level grows by at most one relative to the previous heading.
        int previous = 0;
        foreach (int level in lines.Select(l => HeadingLine().Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value.Length))
        {
            Assert.True(level <= previous + 1 || previous == 0, $"heading level jumps from {previous} to {level}");
            previous = level;
        }

        // 6. Page marker numbers never go back.
        int[] pages = PageMarker().Matches(markdown).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(pages.Order(), pages);

        // 5. Removing the markers gives the rendering without markers (modulo single spaces around inline markers).
        string stripped = PageMarkerLine().Replace(markdown, string.Empty);
        stripped = Spaces().Replace(PageMarker().Replace(stripped, " "), " ");
        Assert.Equal(Spaces().Replace(withoutMarkers, " "), stripped);
    }

    /// <summary>Invariants 7-9 of spec 002: no tables, no column-name row and only section names as headings after the first section.</summary>
    [Fact]
    public async Task TableDocumentMarkdown_KeepsTheTableDocumentInvariants_7to9()
    {
        DocumentTruth truth = BankingCorpusGenerator.Truth(TableDocument);
        (string markdown, _) = await ConvertAsync("banking/" + TableDocument);
        string[] lines = markdown.Split('\n');
        int first = Array.FindIndex(lines, l => l == "## " + truth.SectionNames[0]);
        Assert.True(first >= 0, "the first section name is not a heading");
        string[] fromFirstSection = lines[first..];

        // 7. Neither a GFM table nor a line with the " \| " separator from the first section heading to the end.
        Assert.DoesNotContain(fromFirstSection, l => l.TrimStart().StartsWith('|'));
        Assert.DoesNotContain(fromFirstSection, l => l.Contains(" \\| ",StringComparison.Ordinal));

        // 8. The column-name row is not in the output (its words do not occur in the document content).
        Assert.All(truth.HeaderRowWords, w => Assert.DoesNotContain(w, markdown, StringComparison.Ordinal));

        // 9. Every heading after the first section is a section name or an editorial unit (FR-087).
        foreach (string line in fromFirstSection.Where(l => HeadingLine().IsMatch(l)))
        {
            string text = line.TrimStart('#').Trim();
            Assert.True(
                truth.SectionNames.Contains(text) || EditorialUnit().IsMatch(text),
                $"heading after the first section is neither a section name nor an editorial unit: {line}");
        }
    }

    [GeneratedRegex(@"^(Art\.|§|Rozdział|Dział|Tytuł|Księga|Część|Oddział)\s", RegexOptions.CultureInvariant)]
    private static partial Regex EditorialUnit();

    [GeneratedRegex(@"^\[\^(\d+)\]: ", RegexOptions.CultureInvariant)]
    private static partial Regex FootnoteDefinition();

    [GeneratedRegex(@"\[\^(\d+)\](?!:)", RegexOptions.CultureInvariant)]
    private static partial Regex FootnoteReference();

    [GeneratedRegex(@"^(#{1,6}) ", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingLine();

    [GeneratedRegex(@"<!-- page: (\d+) -->", RegexOptions.CultureInvariant)]
    private static partial Regex PageMarker();

    [GeneratedRegex(@"(?m)^[ ]*<!-- page: \d+ -->\n", RegexOptions.CultureInvariant)]
    private static partial Regex PageMarkerLine();

    [GeneratedRegex(@"[ ]{2,}| +(?=\n)", RegexOptions.CultureInvariant)]
    private static partial Regex Spaces();
}
