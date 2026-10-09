using LegalAgent.Chunking.Model;
using LegalAgent.PdfParser.Model;
using Microsoft.Extensions.DependencyInjection;
using static LegalAgent.Chunking.Tests.Fixtures.Doc;
using static LegalAgent.Chunking.Tests.Unit.SplitParagraphsTests;

namespace LegalAgent.Chunking.Tests.Unit;

/// <summary>T042: the unit key joins the same unit across versions (US2 scenarios 1–4, FR-243, FR-244).</summary>
public sealed class CrossVersionKeyTests
{
    [Fact]
    public async Task ChunkAsync_SameParagraphInTwoVersionsHasTheSameKeyAndDifferentIds()
    {
        LegalDocument older = Document(
            null,
            null,
            Section(2, SectionKind.Chapter, "Rozdział 1", "Rozdział 1 Opłaty", 1, 2, children: [Paragraph(10, 1, 1, Para("Dziesiąty.")), Paragraph(11, 2, 2, Para("Termin wynosi 21 dni."))]));
        LegalDocument newer = Document(
            null,
            null,
            Section(2, SectionKind.Chapter, "Rozdział 1", "Rozdział 1 Opłaty", 1, 1, children: [Paragraph(10, 1, 1, Para("Dziesiąty."))]),
            Section(2, SectionKind.Chapter, "Rozdział 2", "Rozdział 2 Terminy", 2, 2, children: [Paragraph(11, 2, 2, Para("Termin wynosi 30 dni."))]));

        Model.Chunk before = (await Chunk(older, new DocumentMetadata("REG-05-w1") { Designation = "BP/REG/05", Version = 1 })).Single(c => c.Citation == "§ 11");
        Model.Chunk after = (await Chunk(newer, new DocumentMetadata("REG-05") { Designation = "BP/REG/05", Version = 2 })).Single(c => c.Citation == "§ 11");

        Assert.Equal(before.UnitKey, after.UnitKey);
        Assert.NotEqual(before.ChunkId, after.ChunkId);
        Assert.NotEqual(before.Content, after.Content);
    }

    [Fact]
    public async Task ChunkAsync_TariffItemKeepsTheKeyOfItsSectionAcrossVersions()
    {
        static LegalDocument Tariff(string rate) => Document(
            null,
            null,
            Section(2, SectionKind.Typographic, null, "II. Klienci indywidualni", 2, 2, [Table(2, 2, Row("Poz.", "Usługa", "Stawka"), Row("75", "Abonament", rate))]));

        Model.Chunk before = Assert.Single(await Chunk(Tariff("35,00 zł"), new DocumentMetadata("TAR-04-w1") { Designation = "BP/TAR/04" }));
        Model.Chunk after = Assert.Single(await Chunk(Tariff("29,00 zł"), new DocumentMetadata("TAR-04") { Designation = "BP/TAR/04" }));

        Assert.Equal("BP/TAR/04 | II. Klienci indywidualni", after.UnitKey);
        Assert.Equal(before.UnitKey, after.UnitKey);
    }

    [Fact]
    public async Task ChunkAsync_SameDesignationInDifferentDocumentsHasDifferentKeys()
    {
        LegalDocument document = Document(null, null, Paragraph(11, 1, 1, Para("Treść.")));

        Model.Chunk first = Assert.Single(await Chunk(document, new DocumentMetadata("REG-05") { Designation = "BP/REG/05" }));
        Model.Chunk second = Assert.Single(await Chunk(document, new DocumentMetadata("REG-06") { Designation = "BP/REG/06" }));

        Assert.NotEqual(first.UnitKey, second.UnitKey);
    }

    [Fact]
    public async Task ChunkAsync_PartsOfOneUnitShareTheKeyWithConsecutiveParts()
    {
        LegalDocument document = Document(null, null, Paragraph(11, 1, 1, [.. Enumerable.Range(1, 5).Select(i => Para(Words(i, 90)))]));

        IReadOnlyList<Model.Chunk> chunks = await Chunk(document, new DocumentMetadata("REG-05") { Designation = "BP/REG/05" }, 200);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.Equal("BP/REG/05 | § 11", c.UnitKey));
        Assert.Equal(Enumerable.Range(1, chunks.Count), chunks.Select(c => c.Part));
        Assert.Equal(chunks.Count, chunks.Select(c => c.ChunkId).Distinct(StringComparer.Ordinal).Count());
    }

    private static async Task<IReadOnlyList<Model.Chunk>> Chunk(LegalDocument document, DocumentMetadata metadata, int limit = 2000)
    {
        IDocumentChunker chunker = new ServiceCollection().AddLegalAgentChunking(o => o.MaxChunkLength = limit).BuildServiceProvider().GetRequiredService<IDocumentChunker>();
        ChunkedDocument result = await chunker.ChunkAsync(Result(document), metadata, cancellationToken: TestContext.Current.CancellationToken);
        return result.Chunks;
    }
}
