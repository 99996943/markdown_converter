using LegalAgent.Chunking.Model;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.Chunking;

/// <summary>Splits converted documents into chunks for retrieval (spec 004). Safe to call concurrently on one instance.</summary>
public interface IDocumentChunker
{
    /// <summary>Splits the document of a parser result.</summary>
    /// <exception cref="ArgumentException">The metadata is invalid.</exception>
    /// <exception cref="Microsoft.Extensions.Options.OptionsValidationException">The options are invalid.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    Task<ChunkedDocument> ChunkAsync(
        PdfConversionResult conversion,
        DocumentMetadata metadata,
        ChunkingRequest? request = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts the PDF read from <paramref name="pdf"/> (from its current position; the stream is not closed) and
    /// splits the result. Parser exceptions are passed on unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">The metadata is invalid.</exception>
    /// <exception cref="Microsoft.Extensions.Options.OptionsValidationException">The options are invalid.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    Task<ChunkedDocument> ChunkAsync(
        Stream pdf,
        DocumentMetadata metadata,
        ChunkingRequest? request = null,
        CancellationToken cancellationToken = default);
}
