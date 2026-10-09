using LegalAgent.Chunking.Model;
using LegalAgent.PdfParser.Model;
using Microsoft.Extensions.DependencyInjection;
using static LegalAgent.Chunking.Tests.Fixtures.Doc;
using static LegalAgent.Chunking.Tests.Unit.SplitParagraphsTests;

namespace LegalAgent.Chunking.Tests.Unit;

/// <summary>T026: page ranges of chunks (research R4, FR-242).</summary>
public sealed class PageRangeTests
{
    private const int Limit = 200;

    [Fact]
    public async Task ChunkAsync_OwnContentOfAParentEndsWhereItsOwnBlocksEndNotWhereItsChildrenEnd()
    {
        LegalDocument document = Document(
            null,
            null,
            Section(2, SectionKind.Chapter, "Rozdział 1", "Rozdział 1", 1, 5, [Para("Tekst rozdziału.", 1)], children: [Paragraph(1, 2, 5, Para(2, 5, Text("Treść"), Break(5), Text("dalej.")))]));

        IReadOnlyList<Model.Chunk> chunks = await Chunk(document);

        Assert.Equal([new PageSpan(1, 1), new PageSpan(2, 5)], chunks.Select(c => c.Pages));
    }

    [Fact]
    public async Task ChunkAsync_FirstPartStartsAtTheHeadingAndAContinuationAtItsFirstItem()
    {
        string[] t = [.. Enumerable.Range(1, 4).Select(i => Words(i, 80))];
        ListBlock list = List(
            6,
            8,
            Item("1.", ListLabelKind.ArabicDot, t[0]),
            Item("2.", ListLabelKind.ArabicDot, t[1]),
            Item("3.", ListLabelKind.ArabicDot, [Break(7), Text(t[2])]),
            Item("4.", ListLabelKind.ArabicDot, [Text(t[3][..40]), Break(8), Text(t[3][40..])]));

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, Paragraph(9, 5, 8, list)));

        Assert.Equal([new PageSpan(5, 6), new PageSpan(7, 8)], chunks.Select(c => c.Pages));
    }

    [Fact]
    public async Task ChunkAsync_TablePartStartsOnThePageOfItsFirstBodyRow()
    {
        string[] t = [.. Enumerable.Range(1, 5).Select(i => Words(i, 60))];
        int[] pages = [3, 3, 4, 4, 5];
        TableBlock table = Table(3, 5, Row("Usługa", "Stawka") with { Page = 3 }, [.. t.Select((x, i) => Row(x, "5,00 zł") with { Page = pages[i] })]);

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, Paragraph(4, 3, 5, table)));

        Assert.Equal([new PageSpan(3, 3), new PageSpan(4, 4), new PageSpan(5, 5)], chunks.Select(c => c.Pages));
    }

    [Fact]
    public async Task ChunkAsync_RowsWithoutPagesFallBackToTheTablePages()
    {
        string[] t = [.. Enumerable.Range(1, 4).Select(i => Words(i, 60))];
        TableBlock table = Table(3, 5, Row("Usługa", "Stawka"), [.. t.Select(x => Row(x, "5,00 zł"))]);

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, Paragraph(4, 2, 5, Para("Wstęp.", 2), table)));

        Assert.Equal([new PageSpan(2, 5), new PageSpan(3, 5), new PageSpan(3, 5)], chunks.Select(c => c.Pages));
    }

    [Fact]
    public async Task ChunkAsync_RangeIsClippedToTheUnitPages()
    {
        LegalDocument document = Document(null, null, Paragraph(1, 2, 2, Para(1, 3, Text("Treść paragrafu."))));

        IReadOnlyList<Model.Chunk> chunks = await Chunk(document);

        Assert.Equal(new PageSpan(2, 2), Assert.Single(chunks).Pages);
    }

    [Fact]
    public async Task ChunkAsync_SkippedPageBetweenContentIsInsideTheRangeButNeverStartsAPart()
    {
        string first = Words(1, 90);
        string second = Words(2, 90);
        string third = Words(3, 90);
        var skipped = new SkippedPageBlock(new PageRange(5, 5), 5, SkipReason.NoTextLayer);

        IReadOnlyList<Model.Chunk> whole = await Chunk(Document(null, null, Paragraph(1, 4, 6, Para(Words(1, 20), 4), skipped, Para(Words(2, 20), 6))));
        IReadOnlyList<Model.Chunk> split = await Chunk(Document(null, null, Paragraph(1, 4, 6, Para(first, 4), Para(second, 4), skipped, Para(third, 6))));

        Assert.Equal(new PageSpan(4, 6), Assert.Single(whole).Pages);
        Assert.Equal([new PageSpan(4, 4), new PageSpan(6, 6)], split.Select(c => c.Pages));
    }

    [Fact]
    public async Task ChunkAsync_PreambleRangeComesFromItsBlocks()
    {
        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, [Para("Wstęp.", 1), Para("Ciąg dalszy wstępu.", 2)], Paragraph(1, 3, 3, Para("Pierwszy.", 3))));

        Assert.Equal(new PageSpan(1, 2), chunks[0].Pages);
    }

    private static async Task<IReadOnlyList<Model.Chunk>> Chunk(LegalDocument document)
    {
        IDocumentChunker chunker = new ServiceCollection().AddLegalAgentChunking(o => o.MaxChunkLength = Limit).BuildServiceProvider().GetRequiredService<IDocumentChunker>();
        ChunkedDocument result = await chunker.ChunkAsync(Result(document), new DocumentMetadata("T-1"), cancellationToken: TestContext.Current.CancellationToken);
        return result.Chunks;
    }
}
