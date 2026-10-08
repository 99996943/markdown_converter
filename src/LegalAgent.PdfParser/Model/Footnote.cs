namespace LegalAgent.PdfParser.Model;

/// <summary>Footnote definition.</summary>
/// <param name="Number">Document-wide unique number, in order of first reference.</param>
/// <param name="OriginalLabel">Label from the PDF (for example "1)" or "*").</param>
/// <param name="Inlines">Footnote content.</param>
/// <param name="Page">Source page of the definition.</param>
/// <param name="IsOrphan">True when no reference to the footnote was found.</param>
public sealed record Footnote(int Number, string OriginalLabel, IReadOnlyList<Inline> Inlines, int Page, bool IsOrphan);
