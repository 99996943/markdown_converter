namespace LegalAgent.Faq.Model;

/// <summary>Size of one input document.</summary>
/// <param name="DocumentName">Document name.</param>
/// <param name="Characters">Number of characters of the Markdown.</param>
/// <param name="EstimatedTokens">Estimated number of tokens.</param>
public sealed record FaqInputEstimate(string DocumentName, int Characters, int EstimatedTokens);
