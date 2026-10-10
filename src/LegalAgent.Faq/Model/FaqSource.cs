namespace LegalAgent.Faq.Model;

/// <summary>A source of an FAQ item.</summary>
/// <param name="DocumentId">Document identifier, e.g. <c>D2</c>.</param>
/// <param name="Unit">Cited unit (e.g. „§ 12”) after trimming; <c>null</c> when none.</param>
public sealed record FaqSource(string DocumentId, string? Unit);
