using System.Globalization;
using System.Text;
using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>Messages of both steps (contracts/model-exchange.md); Polish, because the FAQ is Polish.</summary>
internal static class FaqPrompts
{
    /// <summary>System message of the candidate step.</summary>
    public static string CandidatesSystem(int maxCandidates) =>
        string.Create(CultureInfo.InvariantCulture, $"Przygotuj co najwyżej {maxCandidates} pytań i odpowiedzi na podstawie dokumentu.");

    /// <summary>User message of the candidate step: identifier, name, source and the full Markdown.</summary>
    public static string CandidatesUser(FaqSourceDocument document, string markdown) =>
        $"Dokument {document.Id}: {document.Name}\nŹródło: {document.Resource.OriginalString}\n\n{markdown}";

    /// <summary>System message of the selection step.</summary>
    public static string SelectionSystem(int itemCount) =>
        string.Create(CultureInfo.InvariantCulture, $"Wybierz dokładnie {itemCount} pytań z listy kandydatów.");

    /// <summary>User message of the selection step: the documents and all candidates, without the document texts.</summary>
    public static string SelectionUser(IReadOnlyList<FaqSourceDocument> documents, IReadOnlyList<FaqCandidate> candidates)
    {
        var text = new StringBuilder("Dokumenty:\n");
        foreach (FaqSourceDocument document in documents)
        {
            text.Append(document.Id).Append(": ").Append(document.Name).Append(" — ").Append(document.Resource.OriginalString).Append('\n');
        }

        text.Append("\nKandydaci:\n");
        foreach (FaqCandidate candidate in candidates)
        {
            string unit = candidate.Unit is null ? string.Empty : ", " + candidate.Unit;
            text.Append('[').Append(candidate.Id).Append("] (").Append(candidate.DocumentId).Append(unit).Append(") Pytanie: ")
                .Append(OneLine(candidate.Question)).Append(" | Odpowiedź: ").Append(OneLine(candidate.Answer)).Append('\n');
        }

        return text.ToString();
    }

    private static string OneLine(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
