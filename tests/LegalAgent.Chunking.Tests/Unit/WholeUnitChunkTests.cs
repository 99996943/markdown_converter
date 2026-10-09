using LegalAgent.Chunking.Identity;
using LegalAgent.Chunking.Model;
using LegalAgent.PdfParser.Model;
using Microsoft.Extensions.DependencyInjection;
using static LegalAgent.Chunking.Tests.Fixtures.Doc;

namespace LegalAgent.Chunking.Tests.Unit;

/// <summary>T014: one chunk per unit within the limit (FR-211, FR-220, FR-221, FR-230, FR-240, FR-241, FR-243, FR-244).</summary>
public sealed class WholeUnitChunkTests
{
    private static readonly LegalDocument Regulation = Document(
        "Regulamin rachunku",
        [Para("Postanowienia wstępne.")],
        Section(
            2,
            SectionKind.Chapter,
            "Rozdział 1",
            "Rozdział 1 Opłaty",
            1,
            3,
            children:
            [
                Paragraph(13, 2, 3, Para("Bank pobiera opłaty.", 2), List(3, 3, Item("1.", ListLabelKind.ArabicDot, "Opłata za przelew."))),
                Section(3, SectionKind.Article, "Art. 5", "Art. 5. (uchylony)", 3, 3, [Para("(uchylony)", 3)]),
            ]));

    private static readonly DocumentMetadata Metadata = new("REG-05-w1") { Designation = "BP/REG/05", Title = "Regulamin z metadanych" };

    [Fact]
    public async Task ChunkAsync_EveryUnitWithinTheLimitIsExactlyOneChunk()
    {
        ChunkedDocument result = await Chunk(Regulation, Metadata);

        Assert.Equal(3, result.Chunks.Count);
        Assert.All(result.Chunks, c =>
        {
            Assert.Equal(1, c.Part);
            Assert.Equal(1, c.PartCount);
            Assert.Empty(c.ListLabels);
            Assert.False(c.ExceedsLimit);
            Assert.Equal(c.Content.Length, c.Length);
            Assert.DoesNotContain("<!--", c.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("Regulamin", c.Content, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ChunkAsync_SectionChunkStartsWithItsHeadingAndCarriesItsMetadata()
    {
        ChunkedDocument result = await Chunk(Regulation, Metadata);
        Model.Chunk paragraph = result.Chunks[1];

        Assert.Equal("### § 13.\n\nBank pobiera opłaty.\n\n- 1\\. Opłata za przelew.", paragraph.Content);
        Assert.Equal(ChunkUnitKind.Paragraph, paragraph.UnitKind);
        Assert.Equal("§ 13", paragraph.Citation);
        Assert.Equal(["Rozdział 1 Opłaty", "§ 13."], paragraph.SectionPath);
        Assert.Equal("BP/REG/05 | § 13", paragraph.UnitKey);
        Assert.Equal(ChunkIdBuilder.Build("REG-05-w1", "BP/REG/05 | § 13", 1), paragraph.ChunkId);
        Assert.Equal(new PageSpan(2, 3), paragraph.Pages);
    }

    [Fact]
    public async Task ChunkAsync_PreambleChunkHasNoHeadingAndNoCitation()
    {
        ChunkedDocument result = await Chunk(Regulation, Metadata);
        Model.Chunk preamble = result.Chunks[0];

        Assert.Equal("Postanowienia wstępne.", preamble.Content);
        Assert.Equal(ChunkUnitKind.Preamble, preamble.UnitKind);
        Assert.Null(preamble.Citation);
        Assert.Empty(preamble.SectionPath);
        Assert.Equal("BP/REG/05 | ~preamble", preamble.UnitKey);
        Assert.Equal(new PageSpan(1, 1), preamble.Pages);
    }

    [Fact]
    public async Task ChunkAsync_VeryShortUnitIsNotJoinedWithItsNeighbour()
    {
        ChunkedDocument result = await Chunk(Regulation, Metadata);
        Model.Chunk article = result.Chunks[2];

        Assert.Equal("### Art. 5. (uchylony)\n\n(uchylony)", article.Content);
        Assert.Equal("Art. 5", article.Citation);
        Assert.DoesNotContain("(uchylony)", result.Chunks[1].Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChunkAsync_NoChunkContainsTwoSections()
    {
        ChunkedDocument result = await Chunk(Regulation, Metadata);

        Assert.All(result.Chunks.Skip(1), c => Assert.Single(c.Content.Split('\n'), line => line.StartsWith('#')));
    }

    [Fact]
    public async Task ChunkAsync_HeaderTakesTitleFromMetadataAndSourceFromTheResult()
    {
        IReadOnlyList<SkippedPage> skipped = [new SkippedPage(4, SkipReason.NoTextLayer, "brak tekstu"), new SkippedPage(2, SkipReason.PageReadError, "błąd")];
        PdfConversionResult conversion = Result(Regulation, skipped);

        ChunkedDocument result = await Chunker().ChunkAsync(conversion, Metadata, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Regulamin z metadanych", result.Header.Title);
        Assert.Equal("Regulamin rachunku", result.Header.DetectedTitle);
        Assert.Equal("BP/REG/05", result.Header.SeriesKey);
        Assert.Equal(3, result.Header.Source.PageCount);
        Assert.Equal(new string('0', 64), result.Header.Source.Sha256);
        Assert.False(result.Header.Source.IsComplete);
        Assert.Equal([2, 4], result.Header.Source.SkippedPages);
    }

    [Fact]
    public async Task ChunkAsync_MetadataWithoutVersionAndDatesWorksAndTitleFallsBackToTheParser()
    {
        ChunkedDocument result = await Chunk(Regulation, new DocumentMetadata("dz-u-2024-30"));

        Assert.Equal("Regulamin rachunku", result.Header.Title);
        Assert.Equal("dz-u-2024-30 | § 13", result.Chunks[1].UnitKey);
        Assert.StartsWith("dz-u-2024-30_", result.Chunks[1].ChunkId, StringComparison.Ordinal);
    }

    private static Task<ChunkedDocument> Chunk(LegalDocument document, DocumentMetadata metadata) =>
        Chunker().ChunkAsync(Result(document), metadata, cancellationToken: TestContext.Current.CancellationToken);

    private static IDocumentChunker Chunker() =>
        new ServiceCollection().AddLegalAgentChunking().BuildServiceProvider().GetRequiredService<IDocumentChunker>();
}
