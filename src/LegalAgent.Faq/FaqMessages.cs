using System.Globalization;
using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>Polish messages of the library exceptions; numbers in pl-PL.</summary>
internal static class FaqMessages
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static string InputTooLong(string documentName, int characters, int estimatedTokens, int limit) =>
        string.Create(
            Polish,
            $"Dokument {documentName} jest za długi dla modelu: {characters:N0} znaków (~{estimatedTokens:N0} tokenów), limit {limit:N0} tokenów.");

    public static string ResponseRejected(FaqStep step, string? documentId, IReadOnlyList<string> problems) =>
        $"Odpowiedź modelu odrzucona ({StepName(step, documentId)}):\n" + string.Join("\n", problems.Select(p => "  - " + p));

    /// <summary>„krok wyboru” or „krok kandydatów, D2”.</summary>
    public static string StepName(FaqStep step, string? documentId) => step switch
    {
        FaqStep.Selection => "krok wyboru",
        _ when documentId is not null => $"krok kandydatów, {documentId}",
        _ => "krok kandydatów",
    };
}
