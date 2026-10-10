namespace LegalAgent.Faq;

/// <summary>Estimates tokens from characters (research R3): no tokenizer, a cautious ratio for Polish text.</summary>
internal static class TokenEstimator
{
    /// <summary><c>ceil(characters / charactersPerToken)</c>.</summary>
    public static int Estimate(int characters, double charactersPerToken) =>
        (int)Math.Min(int.MaxValue, Math.Ceiling(characters / charactersPerToken));
}
