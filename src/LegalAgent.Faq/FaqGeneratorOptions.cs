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

    /// <summary>Throws when a value is out of range.</summary>
    /// <exception cref="ArgumentException">A value is out of range.</exception>
    internal void Validate()
    {
        if (CandidatesPerDocument is < 1 or > 30)
        {
            throw new ArgumentException("CandidatesPerDocument musi mieścić się w zakresie 1–30.", nameof(CandidatesPerDocument));
        }

        if (ItemCount < 1)
        {
            throw new ArgumentException("ItemCount musi być ≥ 1.", nameof(ItemCount));
        }

        if (MaxDocumentTokens <= 0)
        {
            throw new ArgumentException("MaxDocumentTokens musi być > 0.", nameof(MaxDocumentTokens));
        }

        if (!(CharactersPerToken > 0) || !double.IsFinite(CharactersPerToken))
        {
            throw new ArgumentException("CharactersPerToken musi być > 0.", nameof(CharactersPerToken));
        }
    }
}
