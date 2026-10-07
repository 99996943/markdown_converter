using System.Text;

namespace LegalAgent.PdfParser.Text;

/// <summary>Expansion of typographic ligatures into plain letters (research R7). NFKC is deliberately not used.</summary>
internal static class Ligatures
{
    /// <summary>Expands ligature characters (ff, fi, fl, ffi, ffl, long-s t, st) in <paramref name="text"/>.</summary>
    /// <param name="text">Text to expand.</param>
    public static string Expand(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.AsSpan().IndexOfAnyInRange('ﬀ', 'ﬆ') < 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length + 4);
        foreach (char c in text)
        {
            sb.Append(c switch
            {
                'ﬀ' => "ff",
                'ﬁ' => "fi",
                'ﬂ' => "fl",
                'ﬃ' => "ffi",
                'ﬄ' => "ffl",
                'ﬅ' => "st",
                'ﬆ' => "st",
                _ => c.ToString(),
            });
        }

        return sb.ToString();
    }
}
