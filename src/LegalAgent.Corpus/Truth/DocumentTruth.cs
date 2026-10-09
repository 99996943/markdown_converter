namespace LegalAgent.Corpus.Truth;

/// <summary>A heading of the reference truth; <see cref="Level"/> relative to the document title (1).</summary>
public sealed record TruthHeading(int Level, string? Label, string Text);

/// <summary>A list item of the reference truth: original label, nesting depth (0 = top) and its first words.</summary>
public sealed record TruthListItem(string Label, int Depth, string FirstWords);

/// <summary>A table of the reference truth after merging pages (no repeated header rows).</summary>
public sealed record TruthTable(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>Verbatim poison text and the element that carries it.</summary>
public sealed record TruthPoison(string ElementId, string Text);

/// <summary>
/// Reference truth of one generated document (FR-106): what the PDF contains, recorded while typesetting.
/// </summary>
public sealed class DocumentTruth
{
    /// <summary>Content words in reading order (page headers, footers and page numbers excluded).</summary>
    public List<string> Words { get; } = [];

    /// <summary>Headings in document order.</summary>
    public List<TruthHeading> Headings { get; } = [];

    /// <summary>List items in document order.</summary>
    public List<TruthListItem> ListItems { get; } = [];

    /// <summary>Tables in document order.</summary>
    public List<TruthTable> Tables { get; } = [];

    /// <summary>Running headers, footers and page numbers as printed.</summary>
    public List<string> Artifacts { get; } = [];

    /// <summary>Poison texts.</summary>
    public List<TruthPoison> PoisonTexts { get; } = [];
}
