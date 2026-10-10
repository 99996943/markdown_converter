namespace LegalAgent.Faq.Model;

/// <summary>A candidate question and answer from the first step.</summary>
/// <param name="Id">Identifier, e.g. <c>D2-K3</c>, numbered in response order.</param>
/// <param name="DocumentId">Document identifier.</param>
/// <param name="Question">Question.</param>
/// <param name="Answer">Answer.</param>
/// <param name="Unit">Cited unit; <c>null</c> when none.</param>
public sealed record FaqCandidate(string Id, string DocumentId, string Question, string Answer, string? Unit);
