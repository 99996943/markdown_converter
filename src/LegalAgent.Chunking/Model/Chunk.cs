namespace LegalAgent.Chunking.Model;

/// <summary>A fragment of a document: one unit or a part of it (spec 004, FR-240).</summary>
/// <param name="ChunkId">Stable identifier: document id, hash of the unit key and part number.</param>
/// <param name="UnitKey">Key shared by the same unit in all versions of the document.</param>
/// <param name="Part">1-based part number within the unit.</param>
/// <param name="PartCount">Number of parts of the unit.</param>
/// <param name="UnitKind">Kind of the unit.</param>
/// <param name="Citation">Designation of the unit as printed ("§ 13", "Art. 5") or its heading; null for the preamble.</param>
/// <param name="ListLabels">Literal labels of the list item the part starts with and of its ancestors, outermost first.</param>
/// <param name="SectionPath">Heading texts from the root to the unit; empty for the preamble.</param>
/// <param name="Pages">Source pages of the content.</param>
/// <param name="Length">Number of UTF-16 characters of <paramref name="Content"/>.</param>
/// <param name="ExceedsLimit">True when a single indivisible element is longer than the limit.</param>
/// <param name="Content">Markdown of the part: the unit heading, content and footnotes; no page markers, no added words.</param>
public sealed record Chunk(
    string ChunkId,
    string UnitKey,
    int Part,
    int PartCount,
    ChunkUnitKind UnitKind,
    string? Citation,
    IReadOnlyList<string> ListLabels,
    IReadOnlyList<string> SectionPath,
    PageSpan Pages,
    int Length,
    bool ExceedsLimit,
    string Content);
