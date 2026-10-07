using System.Diagnostics.CodeAnalysis;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Text;

/// <summary>A list label found at the start of a line (FR-050).</summary>
/// <param name="Label">The label token as found, e.g. „1)”, „a)”, „–”.</param>
/// <param name="Kind">Classification of the label.</param>
/// <param name="Ordinal">Ordinal of the label within its sequence; null for bullets, dashes and outline labels.</param>
/// <param name="Rest">Text following the label, trimmed.</param>
internal sealed record ListLabelMatch(string Label, ListLabelKind Kind, int? Ordinal, string Rest);

/// <summary>Classification of list labels (bullets, tirets, „1)”, „a)”, „1.”, Roman numerals, outline numbers).</summary>
internal static partial class ListLabelPatterns
{
    /// <summary>Matches a list label at the start of <paramref name="line"/>.</summary>
    public static bool TryMatch(string line, [NotNullWhen(true)] out ListLabelMatch? match)
    {
        throw new NotImplementedException();
    }

    /// <summary>True when <paramref name="text"/> is a single bullet character (including symbol-font glyphs).</summary>
    public static bool IsBulletChar(string text)
    {
        throw new NotImplementedException();
    }
}
