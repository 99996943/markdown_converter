using LegalAgent.Chunking.Model;
using LegalAgent.PdfParser.Model;
using Microsoft.Extensions.DependencyInjection;
using static LegalAgent.Chunking.Tests.Fixtures.Doc;
using static LegalAgent.Chunking.Tests.Unit.SplitParagraphsTests;

namespace LegalAgent.Chunking.Tests.Unit;

/// <summary>T018: lists split between items of any depth, never inside an item (research R3, FR-222, FR-224, FR-241).</summary>
public sealed class SplitListsTests
{
    private const int Limit = 200;

    [Fact]
    public async Task ChunkAsync_ListIsSplitBetweenTopLevelItemsAndContinuationCarriesTheItemLabel()
    {
        string[] t = [.. Enumerable.Range(1, 4).Select(i => Words(i, 80))];
        ListBlock list = List(1, 1, [.. t.Select((text, i) => Item($"{i + 1}.", ListLabelKind.ArabicDot, text))]);

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, Paragraph(9, 1, 1, list)));

        Assert.Equal(
            [$"### § 9.\n\n- 1\\. {t[0]}\n- 2\\. {t[1]}", $"### § 9.\n\n- 3\\. {t[2]}\n- 4\\. {t[3]}"],
            chunks.Select(c => c.Content));
        Assert.Empty(chunks[0].ListLabels);
        Assert.Equal(["3."], chunks[1].ListLabels);
        Assert.All(chunks, c => Assert.Equal("§ 9", c.Citation));
    }

    [Fact]
    public async Task ChunkAsync_ItemLongerThanThePartIsSplitBetweenItsNestedItemsStartingAtColumnZero()
    {
        string i1 = Words(1, 80);
        string i2 = Words(2, 80);
        string i3 = Words(3, 80);
        string a = Words(4, 70);
        string b = Words(5, 70);
        string c = Words(6, 70);
        ListBlock list = List(
            1,
            1,
            Item("1.", ListLabelKind.ArabicDot, i1),
            Item("2.", ListLabelKind.ArabicDot, i2, List(1, 1, Item("a)", ListLabelKind.LetterParen, a), Item("b)", ListLabelKind.LetterParen, b), Item("c)", ListLabelKind.LetterParen, c))),
            Item("3.", ListLabelKind.ArabicDot, i3));

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, Paragraph(9, 1, 1, list)));

        Assert.Equal(
            [
                $"### § 9.\n\n- 1\\. {i1}\n- 2\\. {i2}",
                $"### § 9.\n\n- a\\) {a}\n- b\\) {b}",
                $"### § 9.\n\n- c\\) {c}\n\n- 3\\. {i3}",
            ],
            chunks.Select(ch => ch.Content));
        Assert.Equal(["2.", "a)"], chunks[1].ListLabels);
        Assert.Equal(["2.", "c)"], chunks[2].ListLabels);
        Assert.All(chunks, ch => Assert.False(ch.ExceedsLimit));
    }

    [Fact]
    public async Task ChunkAsync_ItemWithParagraphChildrenIsOneAtom()
    {
        string own = Words(1, 80);
        string child = Words(2, 80);
        string next = Words(3, 80);
        ListBlock list = List(1, 1, Item("1)", ListLabelKind.ArabicParen, own, Para(child)), Item("2)", ListLabelKind.ArabicParen, next));

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, Paragraph(9, 1, 1, list)));

        Assert.Equal([$"### § 9.\n\n- 1\\) {own}\n\n  {child}", $"### § 9.\n\n- 2\\) {next}"], chunks.Select(ch => ch.Content));
        Assert.Equal(["2)"], chunks[1].ListLabels);
    }

    [Fact]
    public async Task ChunkAsync_SingleItemLongerThanTheLimitIsAPartOfItsOwnMarkedAsExceeding()
    {
        string first = Words(1, 80);
        string huge = Words(2, 300);
        ListBlock list = List(1, 1, Item("1.", ListLabelKind.ArabicDot, first), Item("2.", ListLabelKind.ArabicDot, huge));

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, Paragraph(9, 1, 1, list)));

        Assert.Equal([$"### § 9.\n\n- 1\\. {first}", $"### § 9.\n\n- 2\\. {huge}"], chunks.Select(ch => ch.Content));
        Assert.Equal([false, true], chunks.Select(ch => ch.ExceedsLimit));
        Assert.Equal(["2."], chunks[1].ListLabels);
    }

    [Fact]
    public async Task ChunkAsync_ParagraphBeforeAListIsPackedWithItsFirstItems()
    {
        string intro = Words(1, 80);
        string[] t = [.. Enumerable.Range(2, 3).Select(i => Words(i, 80))];
        ListBlock list = List(1, 1, [.. t.Select((text, i) => Item($"{i + 1})", ListLabelKind.ArabicParen, text))]);

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, Paragraph(9, 1, 1, Para(intro), list)));

        Assert.Equal([$"### § 9.\n\n{intro}\n\n- 1\\) {t[0]}", $"### § 9.\n\n- 2\\) {t[1]}\n- 3\\) {t[2]}"], chunks.Select(ch => ch.Content));
        Assert.Equal(["2)"], chunks[1].ListLabels);
    }

    private static async Task<IReadOnlyList<Model.Chunk>> Chunk(LegalDocument document)
    {
        IDocumentChunker chunker = new ServiceCollection().AddLegalAgentChunking(o => o.MaxChunkLength = Limit).BuildServiceProvider().GetRequiredService<IDocumentChunker>();
        ChunkedDocument result = await chunker.ChunkAsync(Result(document), new DocumentMetadata("T-1"), cancellationToken: TestContext.Current.CancellationToken);
        return result.Chunks;
    }
}
