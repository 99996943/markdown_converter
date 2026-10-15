namespace LegalAgent.Faq.Model;

/// <summary>A source document as identified in the result.</summary>
/// <param name="Id">Identifier <c>D1</c>…<c>Dn</c>, in input order.</param>
/// <param name="Name">Display name.</param>
/// <param name="Resource">Address of the source.</param>
public sealed record FaqSourceDocument(string Id, string Name, Uri Resource)
{
    /// <summary>Where the units of the document start, as given in the input (spec 013).</summary>
    public IReadOnlyList<FaqUnitLocation> UnitLocations { get; init; } = [];

    /// <summary>Address of the Markdown of the document relative to the FAQ file, as given in the input; <c>null</c> when unknown.</summary>
    public string? MarkdownLink { get; init; }
}
