namespace LegalAgent.Chunking.Model;

/// <summary>Inclusive range of 1-based source pages.</summary>
/// <param name="First">First page; the page a viewer opens for the chunk.</param>
/// <param name="Last">Last page, not less than <paramref name="First"/>.</param>
public sealed record PageSpan(int First, int Last);
