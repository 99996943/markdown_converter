using System.Globalization;
using System.Text;
using LegalAgent.Chunking.Model;
using LegalAgent.Chunking.Tests.Fixtures;
using LegalAgent.PdfParser;
using LegalAgent.PdfParser.Model;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAgent.Chunking.Tests.Integration;

/// <summary>T028: PDF stream input (FR-202, FR-206) on a corpus document.</summary>
public sealed class PdfInputTests
{
    private static readonly DocumentMetadata Metadata = new("REG-06") { Designation = "BP/REG/06", Type = "regulaminy", Version = 3 };

    [Fact]
    public async Task ChunkAsync_StreamGivesTheSameResultAsTheConversionResult()
    {
        using ServiceProvider provider = Provider();
        IDocumentChunker chunker = provider.GetRequiredService<IDocumentChunker>();
        byte[] pdf = await File.ReadAllBytesAsync(RepoPaths.Corpus("regulaminy/REG-06.pdf"), TestContext.Current.CancellationToken);

        PdfConversionResult conversion = await provider.GetRequiredService<IPdfMarkdownConverter>().ConvertAsync(new MemoryStream(pdf), cancellationToken: TestContext.Current.CancellationToken);
        ChunkedDocument fromResult = await chunker.ChunkAsync(conversion, Metadata, cancellationToken: TestContext.Current.CancellationToken);
        using var stream = new MemoryStream(pdf);
        ChunkedDocument fromStream = await chunker.ChunkAsync(stream, Metadata, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(fromStream.Chunks.Count > 10, $"{fromStream.Chunks.Count} chunks");
        Assert.Equal(Describe(fromResult), Describe(fromStream));
        Assert.True(stream.CanRead, "the stream must not be closed");
    }

    [Fact]
    public async Task ChunkAsync_ParserRequestIsPassedToTheParserAndItsExceptionIsNotWrapped()
    {
        using ServiceProvider provider = Provider();
        await using FileStream pdf = File.OpenRead(RepoPaths.Corpus("regulaminy/REG-06.pdf"));
        var request = new ChunkingRequest { ParserRequest = new PdfConversionRequest { ConfigureOptions = o => o.Limits.MaxPages = 1 } };

        await Assert.ThrowsAsync<PdfLimitExceededException>(() => provider.GetRequiredService<IDocumentChunker>().ChunkAsync(pdf, Metadata, request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ChunkAsync_InvalidPdfRaisesTheParserException()
    {
        using ServiceProvider provider = Provider();
        using var notPdf = new MemoryStream(Encoding.ASCII.GetBytes("to nie jest PDF"));

        await Assert.ThrowsAsync<InvalidPdfException>(() => provider.GetRequiredService<IDocumentChunker>().ChunkAsync(notPdf, Metadata, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ChunkAsync_CancelledTokenEndsWithoutAResult()
    {
        using ServiceProvider provider = Provider();
        await using FileStream pdf = File.OpenRead(RepoPaths.Corpus("regulaminy/REG-06.pdf"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRequiredService<IDocumentChunker>().ChunkAsync(pdf, Metadata, cancellationToken: cancelled.Token));
    }

    [Fact]
    public async Task ChunkAsync_InvalidMetadataIsRejectedBeforeTheParserRuns()
    {
        using ServiceProvider provider = Provider();
        using var notPdf = new MemoryStream(Encoding.ASCII.GetBytes("to nie jest PDF"));

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetRequiredService<IDocumentChunker>().ChunkAsync(notPdf, new DocumentMetadata("REG 06"), cancellationToken: TestContext.Current.CancellationToken));
    }

    private static ServiceProvider Provider() => new ServiceCollection().AddLegalAgentChunking().BuildServiceProvider();

    private static string Describe(ChunkedDocument document)
    {
        var sb = new StringBuilder();
        ChunkSource source = document.Header.Source;
        sb.AppendLine(CultureInfo.InvariantCulture, $"{document.Header.Title}|{document.Header.SeriesKey}|{source.PageCount}|{source.Sha256}|{source.IsComplete}");
        foreach (Model.Chunk c in document.Chunks)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{c.ChunkId}|{c.UnitKey}|{c.Part}/{c.PartCount}|{c.UnitKind}|{c.Citation}|{string.Join(',', c.ListLabels)}|{string.Join('>', c.SectionPath)}|{c.Pages.First}-{c.Pages.Last}|{c.Length}|{c.ExceedsLimit}");
            sb.AppendLine(c.Content);
        }

        return sb.ToString();
    }
}
