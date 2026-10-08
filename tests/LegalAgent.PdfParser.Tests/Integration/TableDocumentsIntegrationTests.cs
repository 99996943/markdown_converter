using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// Spec 002 — table-documents end to end on the synthetic promotion terms (<c>regulamin-promocji-tabela</c>): a table
/// with a full grid over pages 2–5, section names on the left, their content on the right, cells crossing pages.
/// </summary>
public sealed partial class TableDocumentsIntegrationTests
{
    private const string Promotion = "regulamin-promocji-tabela";

    private static async Task<PdfConversionResult> ConvertAsync(byte[] pdf, Action<PdfParserOptions>? configure = null)
    {
        using var stream = new MemoryStream(pdf);
        var request = new PdfConversionRequest { ConfigureOptions = configure };
        return await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, request, TestContext.Current.CancellationToken);
    }

    /// <summary>Blocks of the Markdown separated by blank lines.</summary>
    private static List<string> Blocks(string markdown) =>
        markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(b => b.Trim()).ToList();

    [GeneratedRegex(@"^## (.+)$", RegexOptions.Multiline)]
    private static partial Regex SectionHeading();

    // ---------------------------------------------------------------- US1 (T017)

    [Fact]
    public async Task SectionNames_AreLevel2HeadingsInOrder_WithoutTablesOrTheColumnNameRow()
    {
        DocumentTruth truth = BankingCorpusGenerator.Truth(Promotion);
        string md = (await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion))).Markdown;

        Assert.Equal(truth.SectionNames, SectionHeading().Matches(md).Select(m => m.Groups[1].Value.Trim()));
        string body = md[md.IndexOf("## " + truth.SectionNames[0], StringComparison.Ordinal)..];
        Assert.DoesNotContain(body.Split('\n'), l => l.StartsWith('|'));
        Assert.All(truth.HeaderRowWords, w => Assert.DoesNotContain(w, md, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParagraphBrokenByAPageBoundary_IsOneParagraphWithThePageMarker()
    {
        string md = (await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion))).Markdown;

        string paragraph = Assert.Single(Blocks(md), b => b.Contains("Warunki promocyjne terminala", StringComparison.Ordinal));
        Assert.Contains("<!-- page: 4 -->", paragraph, StringComparison.Ordinal);
        Assert.EndsWith("w każdej placówce Banku.", paragraph, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BulletBrokenByAPageBoundary_IsOneItemWithThePageMarker()
    {
        string md = (await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion))).Markdown;

        string block = Assert.Single(Blocks(md), b => b.Contains("Jeśli spełnisz wszystkie warunki", StringComparison.Ordinal));
        Assert.Matches(@"(?m)^- Jeśli spełnisz wszystkie warunki[^\n]*(\n(?!- )[^\n]*)*<!-- page: 5 -->(\n(?!- )[^\n]*|[^\n])*w aplikacji mobilnej\.", block);
    }

    [Fact]
    public async Task SubBulletsSetInAnotherFont_AreNestedUnderTheirBullet()
    {
        string md = (await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion))).Markdown;

        Assert.Matches(@"(?m)^- 1 zł netto miesięcznie[^\n]*\n  - pakiet Komfort – księgowość uproszczona,\n  - pakiet Start – księgowość pełna,", md);
        Assert.Matches(@"(?m)^- możliwość zamówienia terminala POS:\n  - 0 zł przez 24 miesiące od dnia podpisania umowy,\n  - 0 zł za instalację i aktywację terminala,", md);
    }

    [Fact]
    public async Task Definitions_AreSeparateParagraphs_AndAWebAddressKeepsItsHyphen()
    {
        string md = (await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion))).Markdown;
        List<string> blocks = Blocks(md);

        Assert.Contains("Bank – Bank Przykładowy S.A.", blocks);
        Assert.Contains("Rachunek bieżący – Rachunek Firmowy Standard", blocks);
        Assert.Contains("Regulamin promocji – ten regulamin", blocks);
        Assert.Contains("Terminal POS – urządzenie do przyjmowania płatności kartami", blocks);
        Assert.Contains("Kod rabatowy – e-kod na zakup pierścienia płatniczego", blocks);
        Assert.Contains("https://example.org/products/pierscien-platniczy-mastercard", md, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Model_HasTableDocumentSections_AndTheReportDescribesTheTableDocument()
    {
        PdfConversionResult result = await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion));

        List<Section> sections = Flatten(result.Document.Sections).Where(s => s.Kind == SectionKind.TableDocumentSection).ToList();
        Assert.Equal(BankingCorpusGenerator.Truth(Promotion).SectionNames.Count, sections.Count);
        TableDocumentSummary summary = Assert.Single(result.Report.TableDocuments);
        Assert.Equal(new TableDocumentSummary(2, 5, 6, "Definicje | Wyjaśnienie", 3), summary);
        Assert.Equal(0, result.Report.TableCount);
    }

    private static IEnumerable<Section> Flatten(IEnumerable<Section> sections) =>
        sections.SelectMany(s => new[] { s }.Concat(Flatten(s.Children)));
}
