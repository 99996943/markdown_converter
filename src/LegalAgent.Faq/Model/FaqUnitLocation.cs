namespace LegalAgent.Faq.Model;

/// <summary>Where a unit of a document starts (spec 013, data-model.md).</summary>
/// <param name="Unit">The unit as listed in the units of the document (designation or heading text, first occurrence).</param>
/// <param name="Page">Page of the PDF on which the section of the unit starts; <c>null</c> when unknown.</param>
/// <param name="Anchor">Anchor of the section heading in the Markdown of the document (without <c>#</c>); <c>null</c> when it cannot be computed.</param>
public sealed record FaqUnitLocation(string Unit, int? Page, string? Anchor);
