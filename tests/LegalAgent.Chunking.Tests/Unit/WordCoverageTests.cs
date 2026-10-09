using LegalAgent.Chunking.Model;
using LegalAgent.Chunking.Tests.Fixtures;
using LegalAgent.PdfParser;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Rendering;
using Microsoft.Extensions.DependencyInjection;
using static LegalAgent.Chunking.Tests.Fixtures.Doc;
using static LegalAgent.Chunking.Tests.Unit.SplitParagraphsTests;

namespace LegalAgent.Chunking.Tests.Unit;

/// <summary>T033: chunks cover every word of the document and add none (FR-234, SC-041).</summary>
public sealed class WordCoverageTests
{
    [Fact]
    public async Task ChunkAsync_SplitDocumentWithListsTablesFootnotesAndSkippedPageCoversItsWords()
    {
        string[] w = [.. Enumerable.Range(1, 12).Select(i => Words(i, 80))];
        var skipped = new SkippedPageBlock(new PageRange(3, 3), 3, SkipReason.NoTextLayer);
        LegalDocument document = Document(
            "Regulamin testowy",
            [Para(w[0]), Para(1, 2, Text(w[1][..40]), Break(2), Text(w[1][40..]))],
            Section(
                2,
                SectionKind.Chapter,
                "Rozdział 1",
                "Rozdział 1 Opłaty",
                2,
                5,
                [Para(w[2], 2)],
                children:
                [
                    Section(
                        3,
                        SectionKind.Paragraph,
                        "§ 1",
                        "§ 1.",
                        2,
                        4,
                        [
                            Para(2, 2, Text(w[3]), Ref(1)),
                            List(2, 4, Item("1.", ListLabelKind.ArabicDot, w[4], List(2, 2, Item("a)", ListLabelKind.LetterParen, w[5]), Item("b)", ListLabelKind.LetterParen, [Text(w[6]), Ref(2)]))), Item("2.", ListLabelKind.ArabicDot, [Break(4), Text(w[7])])),
                            skipped,
                            Table(4, 4, Row("Usługa", "Stawka") with { Page = 4 }, Row(w[8][..60], "5,00 zł") with { Page = 4 }, Row(w[9][..60], "7,00 zł") with { Page = 4 }, Row(w[10][..60], "9,00 zł") with { Page = 4 }),
                        ],
                        [Footnote(1, "Przypis pierwszy."), Footnote(2, "Przypis drugi."), Footnote(3, "Przypis bez odwołania.") with { IsOrphan = true }]),
                    Paragraph(2, 5, 5, Para(w[11], 5)),
                ]));

        IReadOnlyList<Model.Chunk> chunks = await Chunk(document, 200);

        Assert.True(chunks.Count > 8, $"{chunks.Count} chunks");
        WordCoverage.AssertCovers(new MarkdownRenderer().Render(document), hasTitle: true, chunks);
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(300)]
    public async Task ChunkAsync_CorpusTariffCoversItsWords(int limit)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ServiceProvider provider = new ServiceCollection().AddLegalAgentChunking(o => o.MaxChunkLength = limit).BuildServiceProvider();
        await using FileStream pdf = File.OpenRead(RepoPaths.Corpus("taryfy/TAR-04.pdf"));
        PdfConversionResult conversion = await provider.GetRequiredService<IPdfMarkdownConverter>().ConvertAsync(pdf, cancellationToken: ct);

        ChunkedDocument result = await provider.GetRequiredService<IDocumentChunker>().ChunkAsync(conversion, new DocumentMetadata("TAR-04"), cancellationToken: ct);

        WordCoverage.AssertCovers(conversion.Markdown, conversion.Document.Title is not null, result.Chunks);
        Assert.All(result.Chunks, c => Assert.True(c.Length <= limit || c.ExceedsLimit, c.ChunkId));
    }

    private static async Task<IReadOnlyList<Model.Chunk>> Chunk(LegalDocument document, int limit)
    {
        IDocumentChunker chunker = new ServiceCollection().AddLegalAgentChunking(o => o.MaxChunkLength = limit).BuildServiceProvider().GetRequiredService<IDocumentChunker>();
        ChunkedDocument result = await chunker.ChunkAsync(Result(document), new DocumentMetadata("T-1"), cancellationToken: TestContext.Current.CancellationToken);
        return result.Chunks;
    }
}
