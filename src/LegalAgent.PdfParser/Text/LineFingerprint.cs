using System.Text;
using System.Text.RegularExpressions;

namespace LegalAgent.PdfParser.Text;

/// <summary>
/// Normalised form of a line used to recognise running headers and footers across pages (FR-021):
/// invariant lower case, digit runs replaced by <c>#</c>, whitespace collapsed, edge punctuation trimmed.
/// </summary>
internal static partial class LineFingerprint
{
    /// <summary>Placeholder that replaces every run of digits.</summary>
    public const char DigitPlaceholder = '#';

    /// <summary>Computes the fingerprint of <paramref name="text"/>.</summary>
    public static string Compute(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string lowered = text.ToLowerInvariant();
        string digitsReplaced = DigitRuns().Replace(lowered, "#");

        var sb = new StringBuilder(digitsReplaced.Length);
        bool pendingSpace = false;
        foreach (char c in digitsReplaced)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(c);
        }

        int start = 0;
        int end = sb.Length;
        while (start < end && !IsSignificant(sb[start]))
        {
            start++;
        }

        while (end > start && !IsSignificant(sb[end - 1]))
        {
            end--;
        }

        return sb.ToString(start, end - start);
    }

    private static bool IsSignificant(char c) => char.IsLetter(c) || c == DigitPlaceholder;

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex DigitRuns();
}
