using System.Text.Json.Serialization;

namespace LegalAgent.PdfParser.Model;

/// <summary>Inline text emphasis.</summary>
[Flags]
public enum TextStyle
{
    /// <summary>No emphasis.</summary>
    None = 0,

    /// <summary>Bold.</summary>
    Bold = 1,

    /// <summary>Italic.</summary>
    Italic = 2,
}

/// <summary>Inline element of a paragraph, list item, table cell or footnote.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TextRun), "text")]
[JsonDerivedType(typeof(FootnoteRef), "footnoteRef")]
[JsonDerivedType(typeof(PageBreak), "pageBreak")]
public abstract record Inline;

/// <summary>A run of text with uniform style.</summary>
/// <param name="Text">The text.</param>
/// <param name="Style">Emphasis flags.</param>
public sealed record TextRun(string Text, TextStyle Style = TextStyle.None) : Inline;

/// <summary>Reference to a footnote definition.</summary>
/// <param name="FootnoteNumber">Document-wide footnote number.</param>
public sealed record FootnoteRef(int FootnoteNumber) : Inline;

/// <summary>Marks the point where the source switches to another page.</summary>
/// <param name="PageNumber">The page that starts here (1-based).</param>
public sealed record PageBreak(int PageNumber) : Inline;
