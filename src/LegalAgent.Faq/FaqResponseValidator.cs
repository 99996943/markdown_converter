using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>Checks parsed responses against the rules of data-model.md („Reguły sprawdzania”, research R5).</summary>
internal static class FaqResponseValidator
{
    /// <summary>Checks the candidates of one document.</summary>
    /// <exception cref="FaqResponseException">With all problems found.</exception>
    public static void ValidateCandidates(
        IReadOnlyList<FaqCandidate> candidates,
        string documentId,
        IReadOnlyCollection<string> units,
        int maxCount) =>
        throw new NotImplementedException();

    /// <summary>Checks the selection and returns the numbered items with trimmed texts.</summary>
    /// <exception cref="FaqResponseException">With all problems found.</exception>
    public static IReadOnlyList<FaqItem> ValidateSelection(
        IReadOnlyList<ParsedItem> items,
        IReadOnlyList<FaqCandidate> candidates,
        IReadOnlyDictionary<string, IReadOnlyList<string>> unitsByDocument,
        int itemCount) =>
        throw new NotImplementedException();
}
