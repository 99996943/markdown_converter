namespace LegalAgent.Chunking.Identity;

/// <summary>
/// Builds unit keys (research R6): the designation shared by all versions, then the shortest suffix of the unit's
/// segment path that is unique in the document; identical full paths get " #n" from the second occurrence.
/// </summary>
internal static class UnitKeyBuilder
{
    /// <summary>Segment of the preamble (metadata only, never content).</summary>
    public const string PreambleSegment = "~wstep";

    /// <summary>Path segment of a section: its designation, or its heading text when it has none, normalised.</summary>
    public static string Segment(string? designation, string headingText) => string.Empty;

    /// <summary>Keys of the units of one document, given their segment paths from the root, in document order.</summary>
    public static IReadOnlyList<string> Build(string seriesKey, IReadOnlyList<IReadOnlyList<string>> paths) =>
        paths.Select(_ => string.Empty).ToList();
}
