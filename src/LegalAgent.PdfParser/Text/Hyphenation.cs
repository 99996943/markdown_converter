namespace LegalAgent.PdfParser.Text;

/// <summary>What to do with a hyphen that ends a line.</summary>
public enum HyphenJoin
{
    /// <summary>Not a word-wrap hyphen: join the lines with a normal space.</summary>
    None,

    /// <summary>Soft wrap: drop the hyphen and glue the two parts without a space.</summary>
    Remove,

    /// <summary>Compound word: keep the hyphen and glue the two parts without a space.</summary>
    Keep,
}

/// <summary>Decides how a line-ending hyphen is joined with the next line (FR-012).</summary>
internal static class Hyphenation
{
    private const int MaxAbbreviationLength = 4;

    /// <summary>
    /// Decides the join. The hyphen is removed when it is the last character of the line, follows a letter, and
    /// the next line starts with a lowercase letter. It is kept when the first part is a one-letter prefix, an
    /// abbreviation or a capitalised noun that is not the first word of the line, when the next line starts with an
    /// uppercase letter, or when the result is on the exception list.
    /// </summary>
    /// <param name="lineEnd">Text of the line that ends with the hyphen.</param>
    /// <param name="nextLineStart">Text of the following line.</param>
    /// <param name="exceptions">Known compounds with a hyphen (for example <c>e-mail</c>).</param>
    public static HyphenJoin Decide(string lineEnd, string nextLineStart, IEnumerable<string> exceptions)
    {
        ArgumentNullException.ThrowIfNull(lineEnd);
        ArgumentNullException.ThrowIfNull(nextLineStart);
        ArgumentNullException.ThrowIfNull(exceptions);

        string end = lineEnd.TrimEnd();
        string next = nextLineStart.TrimStart();
        if (end.Length < 2 || end[^1] != '-' || !char.IsLetter(end[^2]) || next.Length == 0 || !char.IsLetter(next[0]))
        {
            return HyphenJoin.None;
        }

        string prefix = TrailingLetters(end[..^1]);
        string nextWord = LeadingLetters(next);
        string compound = prefix + "-" + nextWord;
        foreach (string exception in exceptions)
        {
            if (string.Equals(exception, compound, StringComparison.OrdinalIgnoreCase))
            {
                return HyphenJoin.Keep;
            }
        }

        if (char.IsUpper(next[0]) || prefix.Length == 1)
        {
            return HyphenJoin.Keep;
        }

        if (prefix.Length <= MaxAbbreviationLength && prefix.All(char.IsUpper))
        {
            return HyphenJoin.Keep;
        }

        return HyphenJoin.Remove;
    }

    private static string TrailingLetters(string text)
    {
        int start = text.Length;
        while (start > 0 && char.IsLetter(text[start - 1]))
        {
            start--;
        }

        return text[start..];
    }

    private static string LeadingLetters(string text)
    {
        int end = 0;
        while (end < text.Length && char.IsLetter(text[end]))
        {
            end++;
        }

        return text[..end];
    }
}
