namespace LegalAgent.Downloads.Model;

/// <summary>Kind of a download failure.</summary>
public enum DownloadErrorKind
{
    /// <summary>The server answered with an error status code.</summary>
    HttpStatus,

    /// <summary>The time limit elapsed.</summary>
    Timeout,

    /// <summary>Connection or name resolution failed.</summary>
    Connection,

    /// <summary>The content does not start with the PDF signature.</summary>
    NotPdf,

    /// <summary>The file exceeds the size limit.</summary>
    TooLarge,

    /// <summary>A redirect points to a host or scheme that is not allowed.</summary>
    RedirectNotAllowed,

    /// <summary>More redirects than allowed.</summary>
    TooManyRedirects,

    /// <summary>The file could not be written.</summary>
    WriteFailed,

    /// <summary>Cancelled by the user.</summary>
    Cancelled,
}

/// <summary>Why a download failed.</summary>
/// <param name="Kind">Kind of the failure.</param>
/// <param name="Message">User-facing message (Polish, from a fixed template; deterministic).</param>
/// <param name="HttpStatusCode">Status code, for <see cref="DownloadErrorKind.HttpStatus"/>.</param>
/// <param name="Detail">Exception text for the log; never written to the manifest.</param>
public sealed record DownloadError(DownloadErrorKind Kind, string Message, int? HttpStatusCode = null, string? Detail = null);
