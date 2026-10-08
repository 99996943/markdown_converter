using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Text;

/// <summary>A list label found at the start of a line (FR-050).</summary>
/// <param name="Label">The label token as found, e.g. „1)”, „a)”, „–”.</param>
/// <param name="Kind">Classification of the label.</param>
/// <param name="Ordinal">Ordinal of the label within its sequence; null for bullets, dashes and outline labels.</param>
/// <param name="Rest">Text following the label, trimmed.</param>
internal sealed record ListLabelMatch(string Label, ListLabelKind Kind, int? Ordinal, string Rest);

/// <summary>Classification of list labels (bullets, tirets, „1)”, „a)”, „1.”, Roman numerals, outline numbers).</summary>
/// <remarks>
/// The label is the leading whitespace-delimited token and must be followed by text. „N.” is only classified here
/// (<see cref="ListLabelKind.ArabicDot"/>); whether it really starts a list is decided later (FR-051).
/// </remarks>
internal static partial class ListLabelPatterns
{
    private static readonly HashSet<string> Bullets = new(StringComparer.Ordinal)
    {
        "•", "▪", "◦", "‣", "*", "●", "■", "□", "○", "✓", "✔",
        "", "", "", "", "", "", "",
    };

    private static readonly HashSet<string> Dashes = new(StringComparer.Ordinal) { "–", "—", "-" };

    /// <summary>Upper-case Roman numerals 1–39 mapped to their value (only well-formed numerals are present).</summary>
    private static readonly Dictionary<string, int> RomanValues = BuildRoman();

    /// <summary>Matches a list label at the start of <paramref name="line"/>.</summary>
    public static bool TryMatch(string line, [NotNullWhen(true)] out ListLabelMatch? match)
    {
        ArgumentNullException.ThrowIfNull(line);
        match = null;

        string text = line.Trim();
        int end = 0;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        if (end == 0 || end == text.Length)
        {
            return false;
        }

        string label = text[..end];
        string rest = text[end..].Trim();
        if (rest.Length == 0)
        {
            return false;
        }

        // „[1)” and „<2a.” mark repealed and future wording in consolidated texts; the bracket stays in the label.
        bool bracketed = label.Length > 1 && label[0] is '[' or '<';
        if (!Classify(bracketed ? label[1..] : label, out ListLabelKind kind, out int? ordinal)
            || (bracketed && kind is ListLabelKind.Bullet or ListLabelKind.Dash))
        {
            return false;
        }

        match = new ListLabelMatch(label, kind, ordinal, rest);
        return true;
    }

    /// <summary>True when <paramref name="text"/> is a single bullet character (including symbol-font glyphs).</summary>
    public static bool IsBulletChar(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Bullets.Contains(text);
    }

    private static bool Classify(string label, out ListLabelKind kind, out int? ordinal)
    {
        kind = default;
        ordinal = null;

        if (Bullets.Contains(label))
        {
            kind = ListLabelKind.Bullet;
            return true;
        }

        if (Dashes.Contains(label))
        {
            kind = ListLabelKind.Dash;
            return true;
        }

        Match m = OutlineLabel().Match(label);
        if (m.Success)
        {
            kind = ListLabelKind.Outline;
            return true;
        }

        m = ArabicLabel().Match(label);
        if (m.Success)
        {
            kind = m.Groups["end"].Value == ")" ? ListLabelKind.ArabicParen : ListLabelKind.ArabicDot;
            ordinal = int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);
            return true;
        }

        m = UpperRomanLabel().Match(label);
        if (m.Success && RomanValues.TryGetValue(m.Groups["r"].Value, out int upper))
        {
            kind = ListLabelKind.Roman;
            ordinal = upper;
            return true;
        }

        m = LowerRomanLabel().Match(label);
        if (m.Success && RomanValues.TryGetValue(m.Groups["r"].Value.ToUpperInvariant(), out int lower))
        {
            kind = ListLabelKind.Roman;
            ordinal = lower;
            return true;
        }

        m = LetterLabel().Match(label);
        if (m.Success)
        {
            string letters = m.Groups["l"].Value;
            kind = ListLabelKind.LetterParen;
            ordinal = letters.Length == 1
                ? letters[0] - 'a' + 1
                : (letters[1] - 'a' + 1) + (26 * (letters[0] - 'a' + 1));
            return true;
        }

        return false;
    }

    private static Dictionary<string, int> BuildRoman()
    {
        string[] tens = ["", "X", "XX", "XXX"];
        string[] units = ["", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX"];
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int t = 0; t < tens.Length; t++)
        {
            for (int u = 0; u < units.Length; u++)
            {
                int value = (t * 10) + u;
                if (value > 0)
                {
                    map[tens[t] + units[u]] = value;
                }
            }
        }

        return map;
    }

    [GeneratedRegex(@"^\d+(\.\d+)+\.?$", RegexOptions.CultureInvariant)]
    private static partial Regex OutlineLabel();

    // 1) 1a) 4ba) 5¹) 1. 2a. 3¹. — at most three digits, so years („2024.”) are not labels.
    [GeneratedRegex(@"^(?<n>\d{1,3})(?:[a-z]{1,2}|[¹²³⁴⁵⁶⁷⁸⁹⁰]+)?(?<end>[).])$", RegexOptions.CultureInvariant)]
    private static partial Regex ArabicLabel();

    [GeneratedRegex(@"^(?<r>[IVX]+)[.)]$", RegexOptions.CultureInvariant)]
    private static partial Regex UpperRomanLabel();

    [GeneratedRegex(@"^(?<r>[ivx]{2,})\)$", RegexOptions.CultureInvariant)]
    private static partial Regex LowerRomanLabel();

    // a) aa) zb) — ordinal is last + 26 × first, monotonic within a sequence (a…z, aa, ab, …).
    [GeneratedRegex(@"^(?<l>[a-z]{1,2})[¹²³⁴⁵⁶⁷⁸⁹⁰]*\)$", RegexOptions.CultureInvariant)]
    private static partial Regex LetterLabel();
}
