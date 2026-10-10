namespace MBank.FaqGenerator;

/// <summary>Removes the API key from messages before they are printed (FR-411, research R8).</summary>
internal static class SecretRedactor
{
    /// <summary>Replaces every occurrence of <paramref name="secret"/> with <c>***</c> (ordinal, literal).</summary>
    public static string Redact(string text, string? secret) =>
        string.IsNullOrEmpty(secret) ? text : text.Replace(secret, "***", StringComparison.Ordinal);
}
