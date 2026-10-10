namespace LegalAgent.Faq.Model;

/// <summary>Result of the FAQ generation.</summary>
/// <param name="Items">Exactly <see cref="FaqGeneratorOptions.ItemCount"/> items.</param>
/// <param name="Documents">Source documents in input order.</param>
/// <param name="Candidates">All candidates (diagnostics; not rendered).</param>
/// <param name="Usage">Summed token usage; <c>null</c> when the service did not report it.</param>
public sealed record FaqResult(
    IReadOnlyList<FaqItem> Items,
    IReadOnlyList<FaqSourceDocument> Documents,
    IReadOnlyList<FaqCandidate> Candidates,
    FaqUsage? Usage);
