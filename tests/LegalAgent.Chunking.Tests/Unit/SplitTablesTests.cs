using LegalAgent.Chunking.Model;
using LegalAgent.PdfParser.Model;
using Microsoft.Extensions.DependencyInjection;
using static LegalAgent.Chunking.Tests.Fixtures.Doc;
using static LegalAgent.Chunking.Tests.Unit.SplitParagraphsTests;

namespace LegalAgent.Chunking.Tests.Unit;

/// <summary>T020: tables split between whole rows, header row repeated (research R3, FR-223, FR-224).</summary>
public sealed class SplitTablesTests
{
    private const int Limit = 200;
    private const string Header = "| Usługa | Stawka |\n| --- | --- |";

    [Fact]
    public async Task ChunkAsync_TableIsSplitBetweenRowsAndEveryPartStartsWithTheHeaderRow()
    {
        string[] t = [.. Enumerable.Range(1, 5).Select(i => Words(i, 60))];
        TableBlock table = Table(1, 1, Row("Usługa", "Stawka"), [.. t.Select(x => Row(x, "5,00 zł"))]);

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, Paragraph(4, 1, 1, table)));

        Assert.Equal(
            [
                $"### § 4.\n\n{Header}\n{R(t[0])}\n{R(t[1])}",
                $"### § 4.\n\n{Header}\n{R(t[2])}\n{R(t[3])}",
                $"### § 4.\n\n{Header}\n{R(t[4])}",
            ],
            chunks.Select(c => c.Content));
        Assert.All(chunks, c => Assert.Empty(c.ListLabels));
    }

    [Fact]
    public async Task ChunkAsync_TableWithoutHeaderRowRepeatsNothing()
    {
        string[] t = [.. Enumerable.Range(1, 4).Select(i => Words(i, 60))];
        TableBlock table = Table(1, 1, null, [.. t.Select(x => Row(x, "5,00 zł"))]);

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, Paragraph(4, 1, 1, table)));

        Assert.Equal(
            [
                $"### § 4.\n\n{R(t[0])}\n| --- | --- |\n{R(t[1])}",
                $"### § 4.\n\n{R(t[2])}\n| --- | --- |\n{R(t[3])}",
            ],
            chunks.Select(c => c.Content));
    }

    [Fact]
    public async Task ChunkAsync_FallbackTableIsSplitBetweenRows()
    {
        string[] t = [.. Enumerable.Range(1, 4).Select(i => Words(i, 70))];
        TableBlock table = FallbackTable(1, 1, [.. t.Select(x => Row(x, "5,00 zł"))]);

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, Paragraph(4, 1, 1, table)));

        Assert.Equal(
            [
                $"### § 4.\n\n{t[0]} \\| 5,00 zł\n\n{t[1]} \\| 5,00 zł",
                $"### § 4.\n\n{t[2]} \\| 5,00 zł\n\n{t[3]} \\| 5,00 zł",
            ],
            chunks.Select(c => c.Content));
    }

    [Fact]
    public async Task ChunkAsync_RowLongerThanTheLimitIsAPartOfItsOwnMarkedAsExceeding()
    {
        string small = Words(1, 60);
        string wide = Words(2, 250);
        TableBlock table = Table(1, 1, Row("Usługa", "Stawka"), Row(small, "5,00 zł"), Row(wide, "5,00 zł"));

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, Paragraph(4, 1, 1, table)));

        Assert.Equal([$"### § 4.\n\n{Header}\n{R(small)}", $"### § 4.\n\n{Header}\n{R(wide)}"], chunks.Select(c => c.Content));
        Assert.Equal([false, true], chunks.Select(c => c.ExceedsLimit));
    }

    [Fact]
    public async Task ChunkAsync_ParagraphBeforeATableIsPackedWithItsFirstRows()
    {
        string intro = Words(1, 70);
        string[] t = [.. Enumerable.Range(2, 2).Select(i => Words(i, 60))];
        TableBlock table = Table(1, 1, Row("Usługa", "Stawka"), [.. t.Select(x => Row(x, "5,00 zł"))]);

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, Paragraph(4, 1, 1, Para(intro), table)));

        Assert.Equal([$"### § 4.\n\n{intro}\n\n{Header}\n{R(t[0])}", $"### § 4.\n\n{Header}\n{R(t[1])}"], chunks.Select(c => c.Content));
    }

    private static string R(string text) => $"| {text} | 5,00 zł |";

    private static async Task<IReadOnlyList<Model.Chunk>> Chunk(LegalDocument document)
    {
        IDocumentChunker chunker = new ServiceCollection().AddLegalAgentChunking(o => o.MaxChunkLength = Limit).BuildServiceProvider().GetRequiredService<IDocumentChunker>();
        ChunkedDocument result = await chunker.ChunkAsync(Result(document), new DocumentMetadata("T-1"), cancellationToken: TestContext.Current.CancellationToken);
        return result.Chunks;
    }
}
