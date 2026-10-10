using System.Text;
using System.Text.RegularExpressions;

namespace LegalAgent.Faq;

/// <summary>Matches a unit cited by the model against the units of a document (data-model.md, „Dopasowanie jednostki”).</summary>
public static partial class UnitMatcher
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
            if (StartsWithUnit(normalized, candidate) || StartsWithUnit(normalized, HeadingNumber(candidate)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithUnit(string cited, string unit) =>
        unit.Length > 0
        && cited.StartsWith(unit, StringComparison.Ordinal)
        && (cited.Length == unit.Length || cited[unit.Length] is ' ' or ',');

    /// <summary>The number of a numbered heading („6” in „6. jakie …”, „2.1” in „2.1. …”), or empty.</summary>
    private static string HeadingNumber(string unit)
    {
        Match match = NumberedHeading().Match(unit);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    [GeneratedRegex(@"^(\d+(?:\.\d+)*)\. ", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedHeading();

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
