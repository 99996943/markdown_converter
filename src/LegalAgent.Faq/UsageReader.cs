using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>Reads token usage from response metadata without depending on connector types.</summary>
internal static class UsageReader
{
    /// <summary>Usage from <c>Metadata["Usage"]</c>; <c>null</c> when absent or of an unknown shape.</summary>
    public static FaqUsage? Read(IReadOnlyDictionary<string, object?>? metadata) => throw new NotImplementedException();
}
