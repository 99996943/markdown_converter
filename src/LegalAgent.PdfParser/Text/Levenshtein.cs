namespace LegalAgent.PdfParser.Text;

/// <summary>Edit distance and normalised similarity of short strings (FR-022).</summary>
internal static class Levenshtein
{
    /// <summary>Number of single-character insertions, deletions or substitutions turning <paramref name="a"/> into <paramref name="b"/>.</summary>
    public static int Distance(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    /// <summary>Similarity in [0, 1]: <c>1 - distance / max(length)</c>; two empty strings are identical.</summary>
    public static double Similarity(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        int max = Math.Max(a.Length, b.Length);
        return max == 0 ? 1.0 : 1.0 - ((double)Distance(a, b) / max);
    }
}
