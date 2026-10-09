namespace LegalAgent.PdfParser.Text;

/// <summary>Font family of a PDF font name (R10).</summary>
internal static class FontFamily
{
    private const int SubsetPrefixLength = 7;

    /// <summary>
    /// The font name without the subset prefix „ABCDEF+” and without the style suffix after the first „-” or „,”;
    /// null when <paramref name="fontName"/> is null or empty.
    /// </summary>
    public static string? Of(string? fontName)
    {
        if (string.IsNullOrEmpty(fontName))
        {
            return null;
        }

        string name = HasSubsetPrefix(fontName) ? fontName[SubsetPrefixLength..] : fontName;
        int suffix = name.AsSpan().IndexOfAny('-', ',');
        return suffix > 0 ? name[..suffix] : name;
    }

    private static bool HasSubsetPrefix(string name)
    {
        if (name.Length <= SubsetPrefixLength || name[SubsetPrefixLength - 1] != '+')
        {
            return false;
        }

        for (int i = 0; i < SubsetPrefixLength - 1; i++)
        {
            if (name[i] is < 'A' or > 'Z')
            {
                return false;
            }
        }

        return true;
    }
}
