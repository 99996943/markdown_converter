using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>
/// Checks that a candidate is grounded in the text of its unit (T067d): the quote occurs in it and so does every number
/// of the answer. Texts are compared after <see cref="Normalize"/>, so Markdown, list labels and quotation marks do not
/// matter.
/// </summary>
internal static partial class FaqGrounding
{
    /// <summary>Fewest words of a quote.</summary>
    public const int MinQuoteWords = 3;

    /// <summary>Problems of the candidate; empty when it is grounded.</summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="markdown">Markdown of the candidate's document.</param>
    /// <returns>Problems, each starting with „kandydat &lt;id&gt;:”.</returns>
    public static IReadOnlyList<string> Problems(FaqCandidate candidate, string markdown)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(markdown);
        string? section = candidate.Unit is { } unit ? UnitText(unit, markdown) : null;
        string scope = section is null
            ? Invariant($"w dokumencie {candidate.DocumentId}")
            : Invariant($"w jednostce „{candidate.Unit}”");
        string text = Normalize(section ?? markdown);
        var problems = new List<string>();
        string quote = Normalize(candidate.Quote ?? string.Empty);
        if (quote.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < MinQuoteWords)
        {
            problems.Add(Invariant($"kandydat {candidate.Id}: cytat ma mniej niż {MinQuoteWords} słowa"));
        }
        else if (!(" " + text + " ").Contains(" " + quote + " ", StringComparison.Ordinal))
        {
            problems.Add(Invariant($"kandydat {candidate.Id}: cytat nie występuje {scope}"));
        }

        var numbers = new HashSet<string>(Numbers(text), StringComparer.Ordinal);
        foreach (string number in Numbers(candidate.Answer).Distinct(StringComparer.Ordinal))
        {
            if (!numbers.Contains(number))
            {
                problems.Add(Invariant($"kandydat {candidate.Id}: liczba „{number}” nie występuje {scope}"));
            }
        }

        return problems;
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
    /// Text of the sections whose heading matches the unit (<see cref="UnitMatcher"/>), each up to the next heading of
    /// the same or a higher level; <c>null</c> when no heading matches.
    /// </summary>
    private static string? UnitText(string unit, string markdown)
    {
        string[] lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var text = new StringBuilder();
        bool found = false;
        int level = 0;
        foreach (string line in lines)
        {
            Match heading = Heading().Match(line);
            if (heading.Success)
            {
                int lineLevel = heading.Groups[1].Length;
                if (level > 0 && lineLevel <= level)
                {
                    level = 0;
                }

                string headingText = heading.Groups[2].Value.Replace("*", string.Empty, StringComparison.Ordinal)
                    .Replace("\\", string.Empty, StringComparison.Ordinal);
                if (level == 0 && UnitMatcher.Matches(unit, [headingText]))
                {
                    level = lineLevel;
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
}
