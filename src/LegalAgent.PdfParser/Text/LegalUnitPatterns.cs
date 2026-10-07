using System.Diagnostics.CodeAnalysis;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Text;

/// <summary>A legal unit designation found at the start of a line (FR-043).</summary>
/// <param name="Kind">Kind of the unit.</param>
/// <param name="Designation">Designation without the trailing period, e.g. „Art. 12a”, „Rozdział 3”, „§ 5¹”.</param>
/// <param name="Number">Number part, e.g. „12a”, „3”, „II”, „PIERWSZA”.</param>
/// <param name="Rest">Text following the designation (and its separating period), trimmed; empty when none.</param>
internal sealed record LegalUnitMatch(SectionKind Kind, string Designation, string Number, string Rest);

/// <summary>Recognition of Polish legal unit designations (Księga, Część, Dział, Rozdział, Oddział, Art., §).</summary>
internal static class LegalUnitPatterns
{
    /// <summary>Matches a legal unit designation at the start of <paramref name="line"/>.</summary>
    public static bool TryMatch(string line, [NotNullWhen(true)] out LegalUnitMatch? match) =>
        throw new NotImplementedException("T061");
}
