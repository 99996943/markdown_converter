using System.Reflection;
using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>
/// Reads token usage from response metadata without depending on connector types: the object under <c>Usage</c> is read
/// by property names (<c>InputTokenCount</c>/<c>OutputTokenCount</c>, or <c>PromptTokens</c>/<c>CompletionTokens</c>).
/// </summary>
internal static class UsageReader
{
    private static readonly (string Input, string Output)[] Shapes =
    [
        ("InputTokenCount", "OutputTokenCount"),
        ("PromptTokens", "CompletionTokens"),
    ];

    /// <summary>Usage from <c>Metadata["Usage"]</c>; <c>null</c> when absent or of an unknown shape.</summary>
    public static FaqUsage? Read(IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata is null || !metadata.TryGetValue("Usage", out object? usage) || usage is null)
        {
            return null;
        }

        Type type = usage.GetType();
        foreach ((string input, string output) in Shapes)
        {
            if (Number(type.GetProperty(input, BindingFlags.Public | BindingFlags.Instance), usage) is { } i
                && Number(type.GetProperty(output, BindingFlags.Public | BindingFlags.Instance), usage) is { } o)
            {
                return new FaqUsage(i, o);
            }
        }

        return null;
    }

    /// <summary>Sum of all requests; <c>null</c> when any request did not report usage.</summary>
    public static FaqUsage? Add(FaqUsage? total, FaqUsage? next, bool first) =>
        first ? next : total is null || next is null ? null : new FaqUsage(total.InputTokens + next.InputTokens, total.OutputTokens + next.OutputTokens);

    private static long? Number(PropertyInfo? property, object target) => property?.GetValue(target) switch
    {
        int v => v,
        long v => v,
        _ => null,
    };
}
