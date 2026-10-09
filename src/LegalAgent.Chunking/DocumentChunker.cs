using System.Globalization;
using System.Text.RegularExpressions;
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
public sealed partial class DocumentChunker : IDocumentChunker
{
    private readonly IPdfMarkdownConverter _converter;
    private readonly IMarkdownRenderer _renderer;
    private readonly IOptions<ChunkingOptions> _options;
    private readonly IOptions<PdfParserOptions> _parserOptions;
    private readonly ChunkingOptionsValidator _validator = new();

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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversion);
        ValidateMetadata(metadata);
        ChunkingOptions options = ResolveOptions(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new ChunkedDocument(Header(conversion, metadata), []));
    }

    /// <inheritdoc />
    public Task<ChunkedDocument> ChunkAsync(
        Stream pdf,
        DocumentMetadata metadata,
        ChunkingRequest? request = null,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    private static void ValidateMetadata(DocumentMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.DocumentId is null || !DocumentIdPattern().IsMatch(metadata.DocumentId))
        {
            throw new ArgumentException(
                $"Identyfikator dokumentu „{metadata.DocumentId}” musi pasować do ^[A-Za-z0-9][A-Za-z0-9._-]{{0,99}}$.",
                nameof(metadata));
        }

        if (metadata.Version is <= 0)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Numer wersji musi być dodatni (jest {metadata.Version})."),
                nameof(metadata));
        }

        if (metadata.ValidFrom is { } from && metadata.ValidTo is { } to && to < from)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Data końca obowiązywania {to:yyyy-MM-dd} jest wcześniejsza niż data początku {from:yyyy-MM-dd}."),
                nameof(metadata));
        }
    }

    private static ChunkedDocumentHeader Header(PdfConversionResult conversion, DocumentMetadata metadata)
    {
        LegalDocument document = conversion.Document;
        var source = new ChunkSource(
            document.Source.PageCount,
            document.Source.Sha256,
            conversion.IsComplete,
            conversion.Report.SkippedPages.Select(p => p.PageNumber).Order().ToList());
        return new ChunkedDocumentHeader(
            metadata,
            metadata.Title ?? document.Title,
            document.Title,
            metadata.Designation ?? metadata.DocumentId,
            source);
    }

    private ChunkingOptions ResolveOptions(ChunkingRequest? request)
    {
        ChunkingOptions options = _options.Value.Clone();
        request?.ConfigureOptions?.Invoke(options);

        ValidateOptionsResult validation = _validator.Validate(Options.DefaultName, options);
        if (validation.Failed)
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(ChunkingOptions), validation.Failures);
        }

        return options;
    }

    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9._-]{0,99}\z", RegexOptions.CultureInvariant)]
    private static partial Regex DocumentIdPattern();
}
