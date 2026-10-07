using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Text;

/// <summary>A legal unit designation found at the start of a line (FR-043).</summary>
/// <param name="Kind">Kind of the unit.</param>
/// <param name="Designation">Designation without the trailing period, e.g. „Art. 12a”, „Rozdział 3”, „§ 5¹”.</param>
/// <param name="Number">Number part, e.g. „12a”, „3”, „II”, „PIERWSZA”.</param>
/// <param name="Rest">Text following the designation (and its separating period), trimmed; empty when none.</param>
/// <param name="Prefix">Amendment bracket printed before the unit („[” repealed, „&lt;” future wording), or empty.</param>
internal sealed record LegalUnitMatch(SectionKind Kind, string Designation, string Number, string Rest, string Prefix = "");

/// <summary>Recognition of Polish legal unit designations (Księga, Część, Dział, Rozdział, Oddział, Art., §).</summary>
/// <remarks>
/// Structural units require an upper-case keyword (as printed in acts) and a numeral; articles and paragraphs require
/// the period after the number, which distinguishes a unit („Art. 5.”) from a reference („Art. 5 ust. 2”).
/// </remarks>
internal static partial class LegalUnitPatterns
{
    private const string Superscripts = "¹²³⁰⁴⁵⁶⁷⁸⁹";

    /// <summary>Matches a legal unit designation at the start of <paramref name="line"/>.</summary>
    public static bool TryMatch(string line, [NotNullWhen(true)] out LegalUnitMatch? match)
    {
        ArgumentNullException.ThrowIfNull(line);
        match = null;

        string text = line.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        Match m = Article().Match(text);
        if (m.Success)
        {
            match = Unit(SectionKind.Article, "Art. " + m.Groups["n"].Value, m.Groups["n"].Value, text[m.Length..]);
            return true;
        }

        m = Paragraph().Match(text);
        if (m.Success)
        {
            match = Unit(SectionKind.Paragraph, "§ " + m.Groups["n"].Value, m.Groups["n"].Value, text[m.Length..]);
            return true;
        }

        m = Structural().Match(text);
        if (!m.Success)
        {
            return false;
        }

        SectionKind kind = m.Groups["k"].Value.ToUpperInvariant() switch
        {
            "KSIĘGA" => SectionKind.Book,
            "CZĘŚĆ" => SectionKind.Part,
            "DZIAŁ" => SectionKind.Division,
            "ROZDZIAŁ" => SectionKind.Chapter,
            _ => SectionKind.Subchapter,
        };

        string number = m.Groups["n"].Value;
        if (kind is SectionKind.Book or SectionKind.Part ? !IsRoman(number) && !IsOrdinalWord(number) : !IsNumeral(number))
        {
            return false;
        }

        match = Unit(kind, m.Groups["k"].Value + " " + number, number, text[m.Length..]);
        return true;
    }

    private static LegalUnitMatch Unit(SectionKind kind, string designation, string number, string rest)
    {
        string trimmed = rest.Trim();
        if (trimmed.StartsWith('.'))
        {
            trimmed = trimmed[1..].TrimStart();
        }

        return new LegalUnitMatch(kind, designation, number, trimmed);
    }

    private static bool IsNumeral(string number) => ArabicNumber().IsMatch(number) || IsRoman(number);

    private static bool IsRoman(string number) => RomanNumber().IsMatch(number);

    private static bool IsOrdinalWord(string word) => word.ToLowerInvariant() is
        "pierwsza" or "druga" or "trzecia" or "czwarta" or "piąta" or "szósta" or "siódma" or "ósma" or "dziewiąta"
        or "dziesiąta" or "ogólna" or "szczególna" or "wojskowa";

    [GeneratedRegex(@"^Art\.\s*(?<n>\d+[a-z]{0,3}[" + Superscripts + @"]*)\.(?=\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex Article();

    [GeneratedRegex(@"^§\s*(?<n>\d+[a-z]{0,3}[" + Superscripts + @"]*)\.(?=\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex Paragraph();

    [GeneratedRegex(@"^(?<k>KSIĘGA|Księga|CZĘŚĆ|Część|DZIAŁ|Dział|ROZDZIAŁ|Rozdział|ODDZIAŁ|Oddział)\s+(?<n>[\p{L}\d]+)(?=\s|\.|$)", RegexOptions.CultureInvariant)]
    private static partial Regex Structural();

    [GeneratedRegex(@"^\d{1,4}[a-z]?$", RegexOptions.CultureInvariant)]
    private static partial Regex ArabicNumber();

    [GeneratedRegex(@"^[IVXLC]{1,7}[a-z]?$", RegexOptions.CultureInvariant)]
    private static partial Regex RomanNumber();
}
