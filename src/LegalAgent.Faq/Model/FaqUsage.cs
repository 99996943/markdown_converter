namespace LegalAgent.Faq.Model;

/// <summary>Token usage reported by the service.</summary>
/// <param name="InputTokens">Input (prompt) tokens.</param>
/// <param name="OutputTokens">Output (completion) tokens.</param>
public sealed record FaqUsage(long InputTokens, long OutputTokens);
