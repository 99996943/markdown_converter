using LegalAgent.Faq.Conversion.Model;
using LegalAgent.PdfParser;

namespace LegalAgent.Faq.Conversion;

/// <summary>
/// Converts a set of PDF files to Markdown written next to them (research R9): files in index order, atomic writes,
/// a failed file does not stop the others, stale <c>*.md</c> files are removed only when all succeeded.
/// </summary>
public sealed class DocumentSetConverter
{
    private readonly IPdfMarkdownConverter converter;

    /// <summary>Creates the converter.</summary>
    /// <param name="converter">The PDF parser.</param>
    public DocumentSetConverter(IPdfMarkdownConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        this.converter = converter;
    }

    /// <summary>Converts the files in turn and writes <c>&lt;name&gt;.md</c> next to each PDF.</summary>
    /// <param name="sources">Files to convert.</param>
    /// <param name="progress">Receives progress events.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Converted documents, failures and removed files.</returns>
    /// <exception cref="IOException">Writing or removing a file failed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<ConversionRun> ConvertAllAsync(
        IReadOnlyList<PdfSource> sources,
        IProgress<ConversionEvent>? progress = null,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
