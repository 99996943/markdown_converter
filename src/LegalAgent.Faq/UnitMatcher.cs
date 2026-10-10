using System.Text;

namespace LegalAgent.Faq;

/// <summary>Matches a unit cited by the model against the units of a document (data-model.md, „Dopasowanie jednostki”).</summary>
public static class UnitMatcher
{
    /// <summary>
    /// Whether <paramref name="cited"/> equals a unit, or starts with one followed by the end, a space or a comma, after
    /// normalisation (NBSP to space, collapsed whitespace, no trailing dot, case-insensitive).
    /// </summary>
    /// <param name="cited">Unit cited by the model, e.g. „§ 12 ust. 3”.</param>
    /// <param name="units">Units of the document.</param>
    /// <returns><c>true</c> when the unit exists in the document.</returns>
    public static bool Matches(string cited, IReadOnlyCollection<string> units)
    {
        ArgumentNullException.ThrowIfNull(cited);
        ArgumentNullException.ThrowIfNull(units);
        string normalized = Normalize(cited);
        if (normalized.Length == 0)
        {
            return false;
        }

        foreach (string unit in units)
        {
            string candidate = Normalize(unit);
            if (candidate.Length == 0 || !normalized.StartsWith(candidate, StringComparison.Ordinal))
            {
                continue;
            }

            if (normalized.Length == candidate.Length || normalized[candidate.Length] is ' ' or ',')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>NBSP to space, collapsed whitespace, trimmed, without a trailing dot, lower case (invariant).</summary>
    internal static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        bool space = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                space = builder.Length > 0;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        if (builder.Length > 0 && builder[^1] == '.')
        {
            builder.Length--;
        }

        return builder.ToString().TrimEnd();
    }
}
