using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Layout;

/// <summary>A footnote definition found on a page, before global numbering (FR-026).</summary>
/// <param name="id">Detection-order identifier, unique within one conversion.</param>
/// <param name="label">Label as printed in the PDF, e.g. „1)” or „*”.</param>
/// <param name="page">Page of the definition.</param>
public sealed class FootnoteDraft(int id, string label, int page)
{
    /// <summary>Detection-order identifier, unique within one conversion.</summary>
    public int Id { get; } = id;

    /// <summary>Label as printed in the PDF.</summary>
    public string Label { get; } = label;

    /// <summary>Page of the definition (its first line).</summary>
    public int Page { get; } = page;

    /// <summary>Content of the definition.</summary>
    public IList<Inline> Inlines { get; } = [];

    /// <summary>True when no reference to this footnote was found in the text.</summary>
    public bool IsOrphan { get; set; }
}
