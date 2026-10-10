using LegalAgent.PdfParser.Model;

namespace LegalAgent.Faq.Conversion;

/// <summary>Collects the units a model may cite from a parsed document.</summary>
public static class UnitExtractor
{
    /// <summary>Designations and heading texts of all sections (recursively), without duplicates, in document order.</summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>The units.</returns>
    public static IReadOnlyList<string> FromDocument(LegalDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var units = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Collect(document.Sections, units, seen);
        return units;
    }

    private static void Collect(IReadOnlyList<Section> sections, List<string> units, HashSet<string> seen)
    {
        foreach (Section section in sections)
        {
            Add(section.Designation, units, seen);
            Add(section.HeadingText, units, seen);
            Collect(section.Children, units, seen);
        }
    }

    private static void Add(string? unit, List<string> units, HashSet<string> seen)
    {
        if (!string.IsNullOrWhiteSpace(unit) && seen.Add(unit))
        {
            units.Add(unit);
        }
    }
}
