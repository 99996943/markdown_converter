namespace LegalAgent.PdfParser;

/// <summary>Base class of all errors raised by the PDF parser.</summary>
public class PdfParserException : Exception
{
    /// <summary>Creates an exception with a default message.</summary>
    public PdfParserException()
        : base("Błąd konwersji dokumentu PDF.")
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    /// <param name="message">Error message.</param>
    public PdfParserException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">The cause.</param>
    public PdfParserException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Why the input is not a valid PDF.</summary>
public enum InvalidPdfReason
{
    /// <summary>The stream is empty.</summary>
    Empty,

    /// <summary>The data does not start with a PDF header.</summary>
    NotPdf,

    /// <summary>The PDF structure is damaged.</summary>
    Corrupted,
}

/// <summary>The input is empty, is not a PDF, or its structure is damaged.</summary>
public sealed class InvalidPdfException : PdfParserException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="reason">Why the input is invalid.</param>
    /// <param name="message">Error message; a default Polish message is used when null.</param>
    /// <param name="innerException">The cause, if any.</param>
    public InvalidPdfException(InvalidPdfReason reason, string? message = null, Exception? innerException = null)
        : base(message ?? DefaultMessage(reason), innerException)
    {
        Reason = reason;
    }

    /// <summary>Why the input is invalid.</summary>
    public InvalidPdfReason Reason { get; }

    private static string DefaultMessage(InvalidPdfReason reason) => reason switch
    {
        InvalidPdfReason.Empty => "Strumień wejściowy jest pusty.",
        InvalidPdfReason.NotPdf => "Dane wejściowe nie są dokumentem PDF (brak nagłówka %PDF).",
        _ => "Struktura dokumentu PDF jest uszkodzona.",
    };
}

/// <summary>The PDF requires a password to be opened.</summary>
public sealed class PdfEncryptedException : PdfParserException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Error message; a default Polish message is used when null.</param>
    /// <param name="innerException">The cause, if any.</param>
    public PdfEncryptedException(string? message = null, Exception? innerException = null)
        : base(message ?? "Dokument PDF jest zaszyfrowany i wymaga hasła.", innerException)
    {
    }
}

/// <summary>A page could not be read (and partial results are not allowed), or all pages were skipped.</summary>
public sealed class PdfPageReadException : PdfParserException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="pageNumber">The page that failed (1-based).</param>
    /// <param name="message">Error message; a default Polish message is used when null.</param>
    /// <param name="innerException">The cause, if any.</param>
    public PdfPageReadException(int pageNumber, string? message = null, Exception? innerException = null)
        : base(
            message ?? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Nie można odczytać strony {pageNumber}."),
            innerException)
    {
        PageNumber = pageNumber;
    }

    /// <summary>The page that failed (1-based).</summary>
    public int PageNumber { get; }
}

/// <summary>Which resource limit was exceeded.</summary>
public enum PdfLimit
{
    /// <summary>Input size in bytes.</summary>
    InputSize,

    /// <summary>Number of pages.</summary>
    PageCount,

    /// <summary>Conversion duration.</summary>
    Duration,
}

/// <summary>A configured resource limit was exceeded.</summary>
public sealed class PdfLimitExceededException : PdfParserException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="limit">Which limit was exceeded.</param>
    /// <param name="configured">The configured limit (bytes, pages or milliseconds).</param>
    /// <param name="measured">The measured value (for input size at least the configured limit plus one).</param>
    /// <param name="message">Error message; a default Polish message is used when null.</param>
    public PdfLimitExceededException(PdfLimit limit, long configured, long measured, string? message = null)
        : base(message ?? DefaultMessage(limit, configured, measured))
    {
        Limit = limit;
        Configured = configured;
        Measured = measured;
    }

    /// <summary>Which limit was exceeded.</summary>
    public PdfLimit Limit { get; }

    /// <summary>The configured limit (bytes, pages or milliseconds).</summary>
    public long Configured { get; }

    /// <summary>The measured value.</summary>
    public long Measured { get; }

    private static string DefaultMessage(PdfLimit limit, long configured, long measured)
    {
        var c = System.Globalization.CultureInfo.InvariantCulture;
        return limit switch
        {
            PdfLimit.InputSize => string.Create(c, $"Przekroczono limit rozmiaru danych wejściowych: {measured} B > {configured} B."),
            PdfLimit.PageCount => string.Create(c, $"Przekroczono limit liczby stron: {measured} > {configured}."),
            _ => string.Create(c, $"Przekroczono limit czasu konwersji: {configured} ms."),
        };
    }
}

/// <summary>The document contains no text at all.</summary>
public sealed class PdfNoTextException : PdfParserException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Error message; a default Polish message is used when null.</param>
    public PdfNoTextException(string? message = null)
        : base(message ?? "Dokument PDF nie zawiera warstwy tekstowej (np. skan bez OCR).")
    {
    }
}
