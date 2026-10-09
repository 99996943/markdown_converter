using LegalAgent.Chunking.Model;
using LegalAgent.PdfParser;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Rendering;
using Microsoft.Extensions.Options;

namespace LegalAgent.Chunking;

/// <summary>
/// Default <see cref="IDocumentChunker"/>: splits the <see cref="LegalDocument"/> of a parser result into units
/// (the preamble and the own content of each section) and long units into parts, renders every part with the
/// parser's Markdown renderer and builds the chunk metadata. Stateless; safe to call concurrently.
/// </summary>
public sealed class DocumentChunker : IDocumentChunker
{
    private readonly IPdfMarkdownConverter _converter;
    private readonly IMarkdownRenderer _renderer;
    private readonly IOptions<ChunkingOptions> _options;
    private readonly IOptions<PdfParserOptions> _parserOptions;

    /// <summary>Creates a chunker; intended for dependency injection.</summary>
    /// <param name="converter">Parser used for PDF input.</param>
    /// <param name="renderer">Markdown renderer of the parser.</param>
    /// <param name="options">Configured options; copied on every call.</param>
    /// <param name="parserOptions">Parser options; their rendering options are the default for chunk content.</param>
    public DocumentChunker(
        IPdfMarkdownConverter converter,
        IMarkdownRenderer renderer,
        IOptions<ChunkingOptions> options,
        IOptions<PdfParserOptions> parserOptions)
    {
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(parserOptions);
        _converter = converter;
        _renderer = renderer;
        _options = options;
        _parserOptions = parserOptions;
    }

    /// <inheritdoc />
    public Task<ChunkedDocument> ChunkAsync(
        PdfConversionResult conversion,
        DocumentMetadata metadata,
        ChunkingRequest? request = null,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<ChunkedDocument> ChunkAsync(
        Stream pdf,
        DocumentMetadata metadata,
        ChunkingRequest? request = null,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
