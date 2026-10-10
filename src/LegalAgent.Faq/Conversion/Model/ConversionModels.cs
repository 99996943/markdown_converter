using LegalAgent.PdfParser.Model;

namespace LegalAgent.Faq.Conversion.Model;

/// <summary>A downloaded PDF to convert.</summary>
/// <param name="Index">Position 1…n.</param>
/// <param name="Address">Address of the source as given by the user.</param>
/// <param name="PdfPath">Full path of the PDF file.</param>
public sealed record PdfSource(int Index, Uri Address, string PdfPath);

/// <summary>A successfully converted document.</summary>
/// <param name="Index">Position 1…n.</param>
/// <param name="Address">Address of the source.</param>
/// <param name="PdfFileName">Name of the PDF file.</param>
/// <param name="MarkdownFileName">Name of the written Markdown file (<c>&lt;name&gt;.md</c>).</param>
/// <param name="Title">Title detected by the parser; <c>null</c> when unknown.</param>
/// <param name="Markdown">Written Markdown.</param>
/// <param name="Units">Designations and heading texts of all sections, without duplicates, in document order.</param>
/// <param name="PageCount">Number of pages.</param>
/// <param name="Warnings">Parser warnings.</param>
public sealed record ConvertedDocument(
    int Index,
    Uri Address,
    string PdfFileName,
    string MarkdownFileName,
    string? Title,
    string Markdown,
    IReadOnlyList<string> Units,
    int PageCount,
    IReadOnlyList<ConversionWarning> Warnings);

/// <summary>A document that could not be converted.</summary>
/// <param name="Index">Position 1…n.</param>
/// <param name="PdfFileName">Name of the PDF file.</param>
/// <param name="Reason">Reason for the user (Polish).</param>
public sealed record ConversionFailure(int Index, string PdfFileName, string Reason);

/// <summary>Result of converting a set of documents.</summary>
/// <param name="Documents">Converted documents in index order.</param>
/// <param name="Failures">Failed documents in index order.</param>
/// <param name="RemovedMarkdownFiles">Markdown files removed because they are not part of the current set.</param>
public sealed record ConversionRun(
    IReadOnlyList<ConvertedDocument> Documents,
    IReadOnlyList<ConversionFailure> Failures,
    IReadOnlyList<string> RemovedMarkdownFiles)
{
    /// <summary>Whether every document was converted.</summary>
    public bool AllSucceeded => Failures.Count == 0;
}

/// <summary>Kind of a conversion event.</summary>
public enum ConversionEventKind
{
    /// <summary>The conversion of a file started.</summary>
    Started,

    /// <summary>The file was converted and its Markdown written.</summary>
    Converted,

    /// <summary>The file could not be converted.</summary>
    Failed,
}

/// <summary>Progress of the conversion.</summary>
/// <param name="Index">Position 1…n.</param>
/// <param name="FileName">Name of the PDF file.</param>
/// <param name="Kind">Kind of the event.</param>
/// <param name="Document">The converted document (Converted).</param>
/// <param name="Failure">The failure (Failed).</param>
public sealed record ConversionEvent(
    int Index,
    string FileName,
    ConversionEventKind Kind,
    ConvertedDocument? Document,
    ConversionFailure? Failure);
