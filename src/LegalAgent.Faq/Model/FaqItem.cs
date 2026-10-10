namespace LegalAgent.Faq.Model;

/// <summary>A final FAQ item.</summary>
/// <param name="Number">Position 1…n in the model's order.</param>
/// <param name="Question">Question.</param>
/// <param name="Answer">Answer.</param>
/// <param name="Sources">Sources, at least one.</param>
/// <param name="BasedOn">Identifiers of the candidates the item is based on, at least one.</param>
public sealed record FaqItem(int Number, string Question, string Answer, IReadOnlyList<FaqSource> Sources, IReadOnlyList<string> BasedOn);
