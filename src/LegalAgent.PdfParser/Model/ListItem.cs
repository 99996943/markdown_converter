namespace LegalAgent.PdfParser.Model;

/// <summary>Kind of a list label.</summary>
public enum ListLabelKind
{
    /// <summary>Bullet character.</summary>
    Bullet,

    /// <summary>Dash (tiret).</summary>
    Dash,

    /// <summary>Number followed by a parenthesis, for example "1)".</summary>
    ArabicParen,

    /// <summary>Number followed by a dot, for example "2.".</summary>
    ArabicDot,

    /// <summary>Letter followed by a parenthesis, for example "a)".</summary>
    LetterParen,

    /// <summary>Roman numeral.</summary>
    Roman,

    /// <summary>Outline numbering such as "1.2.3".</summary>
    Outline,

    /// <summary>Number followed by a slash, for example "1/" or "1a/" (corporate regulations, spec 007).</summary>
    ArabicSlash,

    /// <summary>One or two lower-case letters followed by a slash, for example "a/" or "aa/" (spec 007).</summary>
    LetterSlash,
}

/// <summary>An item of a list.</summary>
/// <param name="Label">Literal label from the source, for example "1)", "a)", "2.", "-" or a bullet.</param>
/// <param name="LabelKind">Kind of the label.</param>
/// <param name="Inlines">Item content.</param>
/// <param name="Children">Nested lists and paragraphs belonging to the item.</param>
public sealed record ListItem(
    string Label,
    ListLabelKind LabelKind,
    IReadOnlyList<Inline> Inlines,
    IReadOnlyList<ContentBlock> Children);
