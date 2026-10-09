using LegalAgent.Chunking.Model;
using LegalAgent.PdfParser.Model;
using Microsoft.Extensions.DependencyInjection;
using static LegalAgent.Chunking.Tests.Fixtures.Doc;
using static LegalAgent.Chunking.Tests.Unit.SplitParagraphsTests;

namespace LegalAgent.Chunking.Tests.Unit;

/// <summary>T022: footnote definitions follow their references (research R5, FR-232).</summary>
public sealed class FootnoteSelectionTests
{
    private const int Limit = 200;

    [Fact]
    public async Task ChunkAsync_EveryPartHasTheDefinitionsItRefersToAndOnlyThose()
    {
        string[] t = [.. Enumerable.Range(1, 3).Select(i => Words(i, 90))];
        Section unit = Section(
            3,
            SectionKind.Paragraph,
            "§ 6",
            "§ 6.",
            1,
            1,
            [Para(1, 1, Text(t[0]), Ref(2)), Para(1, 1, Text(t[1]), Ref(1)), Para(1, 1, Text(t[2]), Ref(2))],
            [Footnote(1, "Przypis pierwszy."), Footnote(2, "Przypis drugi.")]);

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, unit));

        Assert.Equal(
            [
                $"### § 6.\n\n{t[0]}[^2]\n\n[^2]: Przypis drugi.",
                $"### § 6.\n\n{t[1]}[^1]\n\n[^1]: Przypis pierwszy.",
                $"### § 6.\n\n{t[2]}[^2]\n\n[^2]: Przypis drugi.",
            ],
            chunks.Select(c => c.Content));
    }

    [Fact]
    public async Task ChunkAsync_FootnotesWithoutReferenceGoToTheLastPartOnly()
    {
        string[] t = [.. Enumerable.Range(1, 3).Select(i => Words(i, 90))];
        Footnote orphan = Footnote(3, "Przypis bez odwołania.") with { IsOrphan = true };
        Section unit = Section(3, SectionKind.Paragraph, "§ 6", "§ 6.", 1, 1, [.. t.Select(x => Para(x))], [orphan]);

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, unit));

        Assert.Equal([$"### § 6.\n\n{t[0]}\n\n{t[1]}", $"### § 6.\n\n{t[2]}\n\n[^3]: Przypis bez odwołania."], chunks.Select(c => c.Content));
    }

    [Fact]
    public async Task ChunkAsync_UnreferencedFootnotesThatDoNotFitGoToFurtherPartsWithTheHeading()
    {
        string text = Words(1, 90);
        string third = Words(3, 120);
        string fourth = Words(4, 120);
        Section unit = Section(3, SectionKind.Paragraph, "§ 6", "§ 6.", 1, 2, [Para(text)], [Footnote(4, fourth, 2), Footnote(3, third, 2)]);

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, unit));

        Assert.Equal([$"### § 6.\n\n{text}", $"### § 6.\n\n[^3]: {third}", $"### § 6.\n\n[^4]: {fourth}"], chunks.Select(c => c.Content));
        Assert.All(chunks, c => Assert.False(c.ExceedsLimit, c.Content));
        Assert.Equal([new PageSpan(1, 1), new PageSpan(2, 2), new PageSpan(2, 2)], chunks.Select(c => c.Pages));
    }

    [Fact]
    public async Task ChunkAsync_ReferencesInListItemsAndTableCellsCount()
    {
        string a = Words(1, 90);
        string b = Words(2, 60);
        ListBlock list = List(1, 1, Item("1)", ListLabelKind.ArabicParen, [Text(a), Ref(1)]));
        TableBlock table = new(new PageRange(1, 1), Row("Usługa", "Stawka"), [new TableRow([new TableCell([Text(b), Ref(2)]), new TableCell([Text("5,00 zł")])])], 2, false);
        Section unit = Section(3, SectionKind.Paragraph, "§ 6", "§ 6.", 1, 1, [list, table], [Footnote(1, "Do pozycji."), Footnote(2, "Do stawki.")]);

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, unit));

        Assert.Equal(2, chunks.Count);
        Assert.EndsWith("[^1]: Do pozycji.", chunks[0].Content, StringComparison.Ordinal);
        Assert.DoesNotContain("[^2]:", chunks[0].Content, StringComparison.Ordinal);
        Assert.EndsWith("[^2]: Do stawki.", chunks[1].Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChunkAsync_FootnoteLengthCountsTowardsTheLimit()
    {
        string first = Words(1, 90);
        string second = Words(2, 90);
        string note = Words(3, 80);
        Section unit = Section(3, SectionKind.Paragraph, "§ 6", "§ 6.", 1, 1, [Para(1, 1, Text(first), Ref(1)), Para(second)], [Footnote(1, note)]);

        IReadOnlyList<Model.Chunk> chunks = await Chunk(Document(null, null, unit));

        Assert.Equal([$"### § 6.\n\n{first}[^1]\n\n[^1]: {note}", $"### § 6.\n\n{second}"], chunks.Select(c => c.Content));
    }

    [Fact]
    public async Task ChunkAsync_PreambleFootnotesGoWithThePreamble()
    {
        LegalDocument document = Document(null, [Para(1, 1, Text("Wstęp"), Ref(1))], Paragraph(1, 1, 1, Para("Pierwszy."))) with
        {
            PreambleFootnotes = [Footnote(1, "Przypis do wstępu.")],
        };

        IReadOnlyList<Model.Chunk> chunks = await Chunk(document);

        Assert.Equal("Wstęp[^1]\n\n[^1]: Przypis do wstępu.", chunks[0].Content);
        Assert.DoesNotContain("[^1]", chunks[1].Content, StringComparison.Ordinal);
    }

    private static async Task<IReadOnlyList<Model.Chunk>> Chunk(LegalDocument document)
    {
        IDocumentChunker chunker = new ServiceCollection().AddLegalAgentChunking(o => o.MaxChunkLength = Limit).BuildServiceProvider().GetRequiredService<IDocumentChunker>();
        ChunkedDocument result = await chunker.ChunkAsync(Result(document), new DocumentMetadata("T-1"), cancellationToken: TestContext.Current.CancellationToken);
        return result.Chunks;
    }
}
