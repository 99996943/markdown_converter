using System.Globalization;
using System.Text.RegularExpressions;

namespace LegalAgent.PdfParser.Text;

/// <summary>Recognition of stand-alone page numbers (FR-023).</summary>
internal static partial class PageNumberPatterns
{
    /// <summary>
    /// Returns <c>true</c> when the whole <paramref name="text"/> is a page number in one of the formats
    /// <c>3</c>, <c>iv</c>, <c>- 3 -</c>, <c>– 3 –</c>, <c>3 / 40</c>, <c>Strona 3 z 40</c>, <c>Str. 3</c>, <c>s. 3/40</c>.
    /// Bare arabic numbers are limited to three digits so that years are not mistaken for page numbers.
    /// </summary>
    public static bool TryParse(string text, out int number)
    {
        ArgumentNullException.ThrowIfNull(text);
        number = 0;

        string trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        Match match = Arabic().Match(trimmed);
        if (match.Success)
        {
            return int.TryParse(match.Groups["n"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

        if (Roman().IsMatch(trimmed))
        {
            number = RomanToInt(trimmed);
            return number > 0;
        }

        return false;
    }

    /// <summary>
    /// Most frequent difference <c>printed - physical</c>; ties are broken by the smallest absolute offset,
    /// then the smallest value. Returns <c>null</c> when there are no observations.
    /// </summary>
    public static int? DetectOffset(IEnumerable<(int Physical, int Printed)> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var best = observations
            .GroupBy(o => o.Printed - o.Physical)
            .Select(g => (Offset: g.Key, Count: g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => Math.Abs(g.Offset))
            .ThenBy(g => g.Offset)
            .FirstOrDefault();

        return best.Count == 0 ? null : best.Offset;
    }

    private static int RomanToInt(string roman)
    {
        int total = 0;
        int previous = 0;
        for (int i = roman.Length - 1; i >= 0; i--)
        {
            int value = char.ToLowerInvariant(roman[i]) switch
            {
                'i' => 1,
                'v' => 5,
                'x' => 10,
                'l' => 50,
                'c' => 100,
                'd' => 500,
                'm' => 1000,
                _ => 0,
            };

            total += value < previous ? -value : value;
            previous = Math.Max(previous, value);
        }

        return total;
    }

    [GeneratedRegex(
        @"^(?:(?<n>\d{1,3})|[-–—]\s*(?<n>\d{1,4})\s*[-–—]|(?<n>\d{1,4})\s*/\s*\d{1,4}|strona\s+(?<n>\d{1,4})(?:\s+z\s+\d{1,4})?|str\.\s*(?<n>\d{1,4})|s\.\s*(?<n>\d{1,4})(?:\s*/\s*\d{1,4})?)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Arabic();

    [GeneratedRegex(
        @"^(?=[ivxlcdm])m{0,3}(?:cm|cd|d?c{0,3})(?:xc|xl|l?x{0,3})(?:ix|iv|v?i{0,3})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Roman();
}
