using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>
/// Checks that a candidate is grounded in the text of its unit (T067d, T067h): its quote occurs there (near verbatim)
/// and so does every number of the answer. Texts are compared after <see cref="Normalize"/>, so Markdown, list labels
/// and quotation marks do not matter.
/// </summary>
internal static partial class FaqGrounding
{
    /// <summary>Fewest words of a quote.</summary>
    public const int MinQuoteWords = 3;

    /// <summary>Share of the quote's word trigrams that must occur in the unit text.</summary>
    public const double MinQuoteCoverage = 0.8;

    private const int ShownQuoteLength = 60;

    /// <summary>Why the candidate is not grounded, as one line „kandydat &lt;id&gt;: reason; reason”; <c>null</c> when it is.</summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="markdown">Markdown of the candidate's document.</param>
    /// <returns>The problem or <c>null</c>.</returns>
    public static string? Problem(FaqCandidate candidate, string markdown)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(markdown);
        string? section = candidate.Unit is { } unit ? UnitText(unit, markdown) : null;
        string scope = section is null
            ? Invariant($"w dokumencie {candidate.DocumentId}")
            : Invariant($"w jednostce „{candidate.Unit}”");
        string text = Normalize(section ?? markdown);
        var reasons = new List<string>();
        string quote = (candidate.Quote ?? string.Empty).Trim();
        switch (QuoteFound(quote, Normalize(ListLabel().Replace(section ?? markdown, " "))))
        {
            case null:
                reasons.Add(Invariant($"cytat ma mniej niż {MinQuoteWords} słowa"));
                break;
            case false:
                reasons.Add(Invariant($"cytat „{Shorten(quote)}” nie występuje {scope}"));
                break;
        }

        var numbers = new HashSet<string>(Numbers(text), StringComparer.Ordinal);
        string[] missing = [.. Numbers(candidate.Answer).Distinct(StringComparer.Ordinal).Where(n => !numbers.Contains(n))];
        if (missing.Length == 1)
        {
            reasons.Add(Invariant($"liczba „{missing[0]}” nie występuje {scope}"));
        }
        else if (missing.Length > 1)
        {
            reasons.Add(Invariant($"liczby {string.Join(", ", missing.Select(n => "„" + n + "”"))} nie występują {scope}"));
        }

        return reasons.Count == 0 ? null : Invariant($"kandydat {candidate.Id}: {string.Join("; ", reasons)}");
    }

    /// <summary>Runs of digits in order („13:00” gives 13 and 00).</summary>
    public static IReadOnlyList<string> Numbers(string text) =>
        [.. Digits().Matches(text).Select(m => m.Value)];

    /// <summary>Lower-case letters and digits; any other run (Markdown, punctuation, page markers) is one space.</summary>
    internal static string Normalize(string text)
    {
        string withoutComments = Comment().Replace(text, " ");
        var builder = new StringBuilder(withoutComments.Length);
        bool space = false;
        foreach (char c in withoutComments)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (space && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(char.ToLowerInvariant(c));
                space = false;
            }
            else
            {
                space = true;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Whether at least <see cref="MinQuoteCoverage"/> of the word trigrams of the quote's fragments (split at
    /// ellipses) occur in the text; <c>null</c> when the quote has fewer than <see cref="MinQuoteWords"/> words.
    /// </summary>
    private static bool? QuoteFound(string quote, string normalizedText)
    {
        string[][] fragments =
        [
            .. Ellipsis().Split(quote).Select(f => Normalize(ListLabel().Replace(f, " ")).Split(' ', StringSplitOptions.RemoveEmptyEntries)),
        ];
        if (fragments.Sum(f => f.Length) < MinQuoteWords)
        {
            return null;
        }

        string[] words = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var trigrams = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i + 2 < words.Length; i++)
        {
            trigrams.Add(Trigram(words, i));
        }

        int total = 0;
        int found = 0;
        foreach (string[] fragment in fragments)
        {
            for (int i = 0; i + 2 < fragment.Length; i++)
            {
                total++;
                found += trigrams.Contains(Trigram(fragment, i)) ? 1 : 0;
            }
        }

        return total > 0 && found >= MinQuoteCoverage * total;
    }

    private static string Trigram(string[] words, int start) => words[start] + " " + words[start + 1] + " " + words[start + 2];

    private static string Shorten(string quote) =>
        quote.Length <= ShownQuoteLength ? quote : quote[..(ShownQuoteLength - 3)] + "…";

    /// <summary>
    /// Text of the sections whose heading matches the unit (<see cref="UnitMatcher"/>). A section ends at the next
    /// heading of a higher level, or of the same level — except that a numbered or designated heading („6. …”, „§ 5”)
    /// also spans the unnumbered headings of its level after it („Dodatkowe wyjaśnienia”, T067h). <c>null</c> when no
    /// heading matches.
    /// </summary>
    private static string? UnitText(string unit, string markdown)
    {
        string[] lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var text = new StringBuilder();
        bool found = false;
        int level = 0;
        bool designated = false;
        foreach (string line in lines)
        {
            Match heading = Heading().Match(line);
            if (heading.Success)
            {
                int lineLevel = heading.Groups[1].Length;
                string headingText = heading.Groups[2].Value.Replace("*", string.Empty, StringComparison.Ordinal)
                    .Replace("\\", string.Empty, StringComparison.Ordinal).Trim();
                bool lineDesignated = Designation().IsMatch(headingText);
                if (level > 0 && (lineLevel < level || (lineLevel == level && (!designated || lineDesignated))))
                {
                    level = 0;
                }

                if (level == 0 && UnitMatcher.Matches(unit, [headingText]))
                {
                    level = lineLevel;
                    designated = lineDesignated;
                    found = true;
                }
            }

            if (level > 0)
            {
                text.Append(line).Append('\n');
            }
        }

        return found ? text.ToString() : null;
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex Digits();

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Comment();

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex Heading();

    [GeneratedRegex(@"…|\.{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex Ellipsis();

    [GeneratedRegex(@"^(\d+(\.\d+)*\.?\s|§|(art|rozdział|dział|część|oddział|tytuł|załącznik)\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Designation();

    /// <summary>A list label („1)”, „a)”, Markdown „1\)”), removed before comparing quotes (T067m).</summary>
    [GeneratedRegex(@"(?<![\p{L}\p{N}])(\d{1,2}|\p{L})\\?\)", RegexOptions.CultureInvariant)]
    private static partial Regex ListLabel();
}
