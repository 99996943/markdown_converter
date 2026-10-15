using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LegalAgent.Faq;

/// <summary>Heading anchors of a Markdown document as GitHub and VS Code compute them (<c>github-slugger</c>, research R3 of spec 013).</summary>
public static partial class MarkdownAnchors
{
    /// <summary>The anchor of one heading text, without the counter of repeats.</summary>
    /// <param name="headingText">Text of the heading without Markdown markers.</param>
    /// <returns>The anchor (without <c>#</c>).</returns>
    public static string Slug(string headingText)
    {
        ArgumentNullException.ThrowIfNull(headingText);
        var slug = new StringBuilder(headingText.Length);
        foreach (Rune rune in headingText.Trim().ToLowerInvariant().EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                slug.Append('-');
            }
            else if (rune.Value == '-' || Kept(Rune.GetUnicodeCategory(rune)))
            {
                slug.Append(rune.ToString());
            }
        }

        return slug.ToString();
    }

    /// <summary>Anchors of all ATX headings of the document in order, repeats suffixed <c>-1</c>, <c>-2</c>….</summary>
    /// <param name="markdown">The Markdown document.</param>
    /// <returns>The anchors (without <c>#</c>).</returns>
    public static IReadOnlyList<string> ForDocument(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var anchors = new List<string>();
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        string? fence = null;
        foreach (string line in markdown.Split('\n'))
        {
            string text = line.TrimEnd('\r');
            Match opening = Fence().Match(text);
            if (fence is null && opening.Success)
            {
                fence = opening.Groups[1].Value;
                continue;
            }

            if (fence is not null)
            {
                if (opening.Success && opening.Groups[1].Value.StartsWith(fence, StringComparison.Ordinal) && opening.Groups[2].Value.Trim().Length == 0)
                {
                    fence = null;
                }

                continue;
            }

            Match heading = Heading().Match(text);
            if (heading.Success)
            {
                anchors.Add(Unique(Slug(Inline(heading.Groups[1].Value)), occurrences));
            }
        }

        return anchors;
    }

    /// <summary>Letters, combining marks, decimal and letter numbers, connector punctuation (<c>_</c>).</summary>
    private static bool Kept(UnicodeCategory category) => category is
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
        or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter
        or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark
        or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber
        or UnicodeCategory.ConnectorPunctuation;

    /// <summary>The rendered text of a heading: backslash escapes resolved, emphasis markers <c>*</c> removed.</summary>
    private static string Inline(string text) =>
        Escape().Replace(text.Replace("\\*", "\u0000", StringComparison.Ordinal).Replace("*", string.Empty, StringComparison.Ordinal), "$1")
            .Replace('\u0000', '*');

    /// <summary>The slug, or the slug with the first free counter <c>-1</c>, <c>-2</c>… (as <c>github-slugger</c>).</summary>
    private static string Unique(string slug, Dictionary<string, int> occurrences)
    {
        string result = slug;
        while (occurrences.ContainsKey(result))
        {
            occurrences[slug]++;
            result = string.Create(CultureInfo.InvariantCulture, $"{slug}-{occurrences[slug]}");
        }

        occurrences[result] = 0;
        return result;
    }

    [GeneratedRegex(@"^ {0,3}#{1,6}(?:[ \t]+(.*?))?(?:[ \t]+#+)?[ \t]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Heading();

    [GeneratedRegex(@"^ {0,3}(`{3,}|~{3,})(.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex Fence();

    [GeneratedRegex(@"\\([!-/:-@\[-`{-~])", RegexOptions.CultureInvariant)]
    private static partial Regex Escape();
}
