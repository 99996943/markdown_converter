using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Layout;

/// <summary>A detected heading (FR-040 – FR-047, FR-043a).</summary>
/// <param name="Level">Markdown heading level, 1–6.</param>
/// <param name="Kind">Kind of the unit (legal unit or typographic heading).</param>
/// <param name="Designation">Literal designation, e.g. „Rozdział 3”, „Art. 12a”, „§ 5¹”; null for typographic headings.</param>
/// <param name="Number">Number part, e.g. „3”, „12a”, „II”; null for typographic headings.</param>
/// <param name="Title">Title of the unit, e.g. „Ochrona konsumenta”; null when absent.</param>
/// <param name="Text">Full heading text rendered after the <c>#</c> marks, e.g. „Rozdział 3. Ochrona konsumenta”, „Art. 5.”.</param>
public sealed record HeadingInfo(int Level, SectionKind Kind, string? Designation, string? Number, string? Title, string Text);
