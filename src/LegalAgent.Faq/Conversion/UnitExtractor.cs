using LegalAgent.PdfParser.Model;

namespace LegalAgent.Faq.Conversion;

/// <summary>Collects the units a model may cite from a parsed document.</summary>
public static class UnitExtractor
{
    /// <summary>Designations and heading texts of all sections (recursively), without duplicates, in document order.</summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>The units.</returns>
    public static IReadOnlyList<string> FromDocument(LegalDocument document) => throw new NotImplementedException();
}
