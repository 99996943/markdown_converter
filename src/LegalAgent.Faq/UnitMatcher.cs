namespace LegalAgent.Faq;

/// <summary>Matches a unit cited by the model against the units of a document (data-model.md, „Dopasowanie jednostki”).</summary>
public static class UnitMatcher
{
    /// <summary>
    /// Whether <paramref name="cited"/> equals a unit, or starts with one followed by the end, a space or a comma, after
    /// normalisation (NBSP to space, collapsed whitespace, no trailing dot, case-insensitive).
    /// </summary>
    /// <param name="cited">Unit cited by the model, e.g. „§ 12 ust. 3”.</param>
    /// <param name="units">Units of the document.</param>
    /// <returns><c>true</c> when the unit exists in the document.</returns>
    public static bool Matches(string cited, IReadOnlyCollection<string> units) => throw new NotImplementedException();
}
