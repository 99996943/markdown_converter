using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser;

/// <summary>Converts PDF documents (Polish legal acts, bank terms and tariffs) to a structured model and Markdown.</summary>
public interface IPdfMarkdownConverter
{
    /// <summary>
    /// Converts the PDF read from <paramref name="pdf"/> (from its current position to the end). The stream is not
    /// closed. Safe to call concurrently on one instance.
    /// </summary>
    /// <exception cref="PdfParserException">The input is invalid, encrypted, has no text, a page cannot be read or a limit is exceeded.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    Task<PdfConversionResult> ConvertAsync(
        Stream pdf,
        PdfConversionRequest? request = null,
        CancellationToken cancellationToken = default);
}
