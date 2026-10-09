using LegalAgent.Chunking.Model;
using LegalAgent.PdfParser.Model;
using Microsoft.Extensions.DependencyInjection;
using static LegalAgent.Chunking.Tests.Fixtures.Doc;

namespace LegalAgent.Chunking.Tests.Unit;

/// <summary>T016: long units split between paragraphs (research R3, FR-222, FR-224, FR-225, FR-231).</summary>
public sealed class SplitParagraphsTests
{
    private const int Limit = 200;

    [Fact]
    public async Task ChunkAsync_LongUnitIsPackedGreedilyBetweenParagraphsAndEveryPartStartsWithTheHeading()
    {
        string[] p = [.. Enumerable.Range(1, 5).Select(i => Words(i, 90))];
        LegalDocument document = Document(null, null, Paragraph(7, 1, 1, [.. p.Select(t => Para(t))]));

        IReadOnlyList<Model.Chunk> chunks = await Chunk(document);

        Assert.Equal(
            [$"### § 7.\n\n{p[0]}\n\n{p[1]}", $"### § 7.\n\n{p[2]}\n\n{p[3]}", $"### § 7.\n\n{p[4]}"],
            chunks.Select(c => c.Content));
        Assert.Equal([1, 2, 3], chunks.Select(c => c.Part));
        Assert.All(chunks, c =>
        {
            Assert.Equal(3, c.PartCount);
            Assert.Equal("§ 7", c.Citation);
            Assert.Equal(chunks[0].UnitKey, c.UnitKey);
            Assert.Equal(["§ 7."], c.SectionPath);
            Assert.True(c.Length <= Limit, c.Content);
            Assert.False(c.ExceedsLimit);
        });
        Assert.Equal(3, chunks.Select(c => c.ChunkId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task ChunkAsync_ParagraphLongerThanTheLimitIsAPartOfItsOwnMarkedAsExceeding()
    {
        string first = Words(1, 90);
        string longOne = Words(2, 300);
        string last = Words(3, 90);
        LegalDocument document = Document(null, null, Paragraph(7, 1, 1, Para(first), Para(longOne), Para(last)));

        IReadOnlyList<Model.Chunk> chunks = await Chunk(document);

        Assert.Equal([$"### § 7.\n\n{first}", $"### § 7.\n\n{longOne}", $"### § 7.\n\n{last}"], chunks.Select(c => c.Content));
        Assert.Equal([false, true, false], chunks.Select(c => c.ExceedsLimit));
    }

    [Fact]
    public async Task ChunkAsync_PreambleIsSplitWithoutAHeading()
    {
        string[] p = [.. Enumerable.Range(1, 3).Select(i => Words(i, 90))];
        LegalDocument document = Document(null, [.. p.Select(t => Para(t))]);

        IReadOnlyList<Model.Chunk> chunks = await Chunk(document);

        Assert.Equal([$"{p[0]}\n\n{p[1]}", p[2]], chunks.Select(c => c.Content));
        Assert.All(chunks, c => Assert.Equal(ChunkUnitKind.Preamble, c.UnitKind));
    }

    [Fact]
    public async Task ChunkAsync_ChunksFollowTheDocumentOrder()
    {
        string[] p = [.. Enumerable.Range(1, 6).Select(i => Words(i, 90))];
        LegalDocument document = Document(
            null,
            [Para(p[0])],
            Paragraph(7, 1, 1, Para(p[1]), Para(p[2]), Para(p[3])),
            Paragraph(8, 2, 2, Para(p[4]), Para(p[5])));

        IReadOnlyList<Model.Chunk> chunks = await Chunk(document);

        string joined = string.Join("\n\n", chunks.Select(c => c.Content));
        int[] positions = [.. p.Select(t => joined.IndexOf(t, StringComparison.Ordinal))];
        Assert.All(positions, i => Assert.True(i >= 0));
        Assert.Equal(positions.Order(), positions);
        Assert.Equal(["§ 7", "§ 7", "§ 8"], chunks.Skip(1).Select(c => c.Citation));
    }

    /// <summary>Plain words of exactly <paramref name="length"/> characters, distinct per <paramref name="n"/>.</summary>
    internal static string Words(int n, int length)
    {
        string text = $"Akapit{n} " + string.Concat(Enumerable.Repeat("tekst postanowienia ", (length / 20) + 2));
        return text[..(length - 1)].TrimEnd() .PadRight(length - 1, 'x') + "x";
    }

    private static async Task<IReadOnlyList<Model.Chunk>> Chunk(LegalDocument document)
    {
        IDocumentChunker chunker = new ServiceCollection().AddLegalAgentChunking(o => o.MaxChunkLength = Limit).BuildServiceProvider().GetRequiredService<IDocumentChunker>();
        ChunkedDocument result = await chunker.ChunkAsync(Result(document), new DocumentMetadata("T-1"), cancellationToken: TestContext.Current.CancellationToken);
        return result.Chunks;
    }
}
