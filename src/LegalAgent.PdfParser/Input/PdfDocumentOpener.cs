using LegalAgent.PdfParser.Model;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

namespace LegalAgent.PdfParser.Input;

/// <summary>An opened PDF together with its source metadata; disposing releases the PdfPig document.</summary>
internal sealed class OpenedPdf : IDisposable
{
    /// <summary>Creates the holder.</summary>
    /// <param name="document">The opened document.</param>
    /// <param name="source">Source metadata.</param>
    public OpenedPdf(PdfDocument document, SourceInfo source)
    {
        Document = document;
        Source = source;
    }

    /// <summary>The opened PdfPig document.</summary>
    public PdfDocument Document { get; }

    /// <summary>Source metadata.</summary>
    public SourceInfo Source { get; }

    /// <inheritdoc />
    public void Dispose() => Document.Dispose();
}

/// <summary>Opens a buffered PDF with PdfPig and maps its failures to library exceptions (research R4).</summary>
internal static class PdfDocumentOpener
{
    /// <summary>
    /// Parsing options. Lenient parsing lets PdfPig repair small structural defects (bad xref offsets,
    /// missing endobj) that real-world PDFs contain, so a file is rejected as corrupted (FR-009) only
    /// when even the repair fails. Content errors of a single page surface later, when the page is
    /// read, and are handled as FR-009a. See research R4 "Decyzja T037".
    /// </summary>
    internal static ParsingOptions CreateParsingOptions() => new()
    {
        UseLenientParsing = true,
        SkipMissingFonts = false,
    };

    /// <summary>Opens the PDF and checks the page limit before any page is read.</summary>
    /// <param name="input">Buffered input.</param>
    /// <param name="sourceId">Caller-supplied source identifier.</param>
    /// <param name="limits">Resource limits.</param>
    /// <exception cref="InvalidPdfException">The structure is damaged and cannot be repaired.</exception>
    /// <exception cref="PdfEncryptedException">A password is required.</exception>
    /// <exception cref="PdfLimitExceededException">The page count exceeds <see cref="LimitsOptions.MaxPages"/>.</exception>
    public static OpenedPdf Open(PdfInput input, string? sourceId, LimitsOptions limits)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(limits);

        PdfDocument document;
        try
        {
            document = PdfDocument.Open(input.Bytes, CreateParsingOptions());
        }
        catch (PdfDocumentEncryptedException ex)
        {
            throw new PdfEncryptedException(innerException: ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidPdfException(InvalidPdfReason.Corrupted, innerException: ex);
        }

        try
        {
            int pageCount = document.NumberOfPages;
            if (limits.MaxPages is int maxPages && pageCount > maxPages)
            {
                throw new PdfLimitExceededException(PdfLimit.PageCount, maxPages, pageCount);
            }

            string? title = document.Information.Title;
            if (string.IsNullOrWhiteSpace(title))
            {
                title = null;
            }

            var source = new SourceInfo(sourceId, pageCount, title, input.ByteLength, input.Sha256);
            return new OpenedPdf(document, source);
        }
        catch (PdfLimitExceededException)
        {
            document.Dispose();
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            document.Dispose();
            throw new InvalidPdfException(InvalidPdfReason.Corrupted, innerException: ex);
        }
    }
}
