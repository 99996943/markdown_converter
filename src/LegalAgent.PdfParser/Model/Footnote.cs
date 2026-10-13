namespace LegalAgent.PdfParser.Model;

/// <summary>Footnote definition.</summary>
/// <param name="Number">Document-wide unique number, in order of first reference.</param>
/// <param name="OriginalLabel">Label from the PDF (for example "1)" or "*").</param>
/// <param name="Inlines">Footnote content.</param>
/// <param name="Page">Source page of the definition.</param>
/// <param name="IsOrphan">True when no reference to the footnote was found.</param>
public sealed record Footnote(int Number, string OriginalLabel, IReadOnlyList<Inline> Inlines, int Page, bool IsOrphan)
{
    /// <summary>
    /// Spec 012: the Markdown identifier (<c>[^Identifier]</c>) — the PDF label, the label with an occurrence suffix
    /// <c>-k</c> when it repeats in the document, or a substitute number for a label Markdown cannot carry. Empty means
    /// the renderer writes <see cref="Number"/> (a model built by a library client).
    /// </summary>
    public string Identifier { get; init; } = "";
}
