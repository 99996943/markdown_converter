using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>A parsed value or the problem that prevented parsing.</summary>
/// <typeparam name="T">Type of the value.</typeparam>
/// <param name="Value">The value; <c>null</c> when <paramref name="Problem"/> is set.</param>
/// <param name="Problem">Why the text was rejected.</param>
internal sealed record Parsed<T>(T? Value, string? Problem)
    where T : class;

/// <summary>An item of the selection response before validation.</summary>
/// <param name="Question">Question as returned.</param>
/// <param name="Answer">Answer as returned.</param>
/// <param name="BasedOn">Candidate identifiers as returned.</param>
/// <param name="Sources">Sources; a blank unit is <c>null</c>.</param>
internal sealed record ParsedItem(string Question, string Answer, IReadOnlyList<string> BasedOn, IReadOnlyList<FaqSource> Sources);

/// <summary>Parses the JSON responses of both steps (contracts/model-exchange.md) without trusting the service schema.</summary>
internal static class FaqResponseParser
{
    /// <summary>Problem reported for any text that is not JSON of the expected shape.</summary>
    public const string NotJson = "odpowiedź nie jest poprawnym JSON-em zgodnym ze schematem";

    /// <summary>Parses a candidate response; candidates get identifiers <c>&lt;documentId&gt;-K1</c>… in order.</summary>
    public static Parsed<IReadOnlyList<FaqCandidate>> ParseCandidates(string text, string documentId) =>
        throw new NotImplementedException();

    /// <summary>Parses a selection response.</summary>
    public static Parsed<IReadOnlyList<ParsedItem>> ParseSelection(string text) =>
        throw new NotImplementedException();
}
