namespace LegalAgent.PdfParser.Model;

/// <summary>Structured model of a converted legal document.</summary>
/// <param name="Source">Source metadata.</param>
/// <param name="Title">Detected title or PDF metadata title; null when unknown.</param>
/// <param name="Preamble">Content before the first section.</param>
/// <param name="PreambleFootnotes">Footnotes referenced from the title or preamble.</param>
/// <param name="Sections">Top-level sections.</param>
public sealed record LegalDocument(
    SourceInfo Source,
    string? Title,
    IReadOnlyList<ContentBlock> Preamble,
    IReadOnlyList<Footnote> PreambleFootnotes,
    IReadOnlyList<Section> Sections);
