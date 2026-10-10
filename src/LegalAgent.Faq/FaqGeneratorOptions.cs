namespace LegalAgent.Faq;

/// <summary>Options of <see cref="FaqGenerator"/>.</summary>
public sealed record FaqGeneratorOptions
{
    /// <summary>Maximum number of candidates per document, 1–30.</summary>
    public int CandidatesPerDocument { get; init; } = 10;

    /// <summary>Number of final items, at least 1.</summary>
    public int ItemCount { get; init; } = 10;

    /// <summary>Maximum estimated tokens of one document, greater than 0.</summary>
    public int MaxDocumentTokens { get; init; } = 100_000;

    /// <summary>Characters per token used by the estimate, greater than 0.</summary>
    public double CharactersPerToken { get; init; } = 3.0;
}
