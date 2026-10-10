using System.Globalization;
using System.Text;
using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>Messages of both steps (contracts/model-exchange.md); Polish, because the FAQ is Polish.</summary>
internal static class FaqPrompts
{
    /// <summary>System message of the candidate step.</summary>
    public static string CandidatesSystem(int maxCandidates) => string.Create(
        CultureInfo.InvariantCulture,
        $"""
        Przygotowujesz FAQ dla klientów banku na podstawie jednego dokumentu (regulaminu lub taryfy), podanego w Markdown.

        Zasady:
        - Zaproponuj co najwyżej {maxCandidates} pytań, które klient najczęściej zadaje w sprawach opisanych w dokumencie, wraz z odpowiedziami.
        - Odpowiadaj wyłącznie na podstawie treści dokumentu. Nie dodawaj informacji spoza dokumentu, nie zgaduj i nie uogólniaj.
        - Jeśli dokument nie daje odpowiedzi na pytanie, napisz wprost: „Dokument nie rozstrzyga …” i dokończ, czego nie rozstrzyga.
        - Pisz po polsku, jasno i zwięźle, językiem zrozumiałym dla klienta; kwoty, terminy i warunki przepisuj dokładnie.
        - W polu „unit” podaj oznaczenie jednostki redakcyjnej, z której pochodzi odpowiedź (np. § 12, Art. 5, Rozdział 3), dokładnie tak, jak w nagłówku dokumentu, albo pusty tekst, gdy odpowiedź nie pochodzi z jednej jednostki.
        - Odpowiedz wyłącznie obiektem JSON zgodnym ze schematem.
        """);

    /// <summary>User message of the candidate step: identifier, name, source and the full Markdown.</summary>
    public static string CandidatesUser(FaqSourceDocument document, string markdown) =>
        $"Dokument {document.Id}: {document.Name}\nŹródło: {document.Resource.OriginalString}\n\n{markdown}";

    /// <summary>System message of the selection step.</summary>
    public static string SelectionSystem(int itemCount) => string.Create(
        CultureInfo.InvariantCulture,
        $"""
        Układasz końcowe FAQ dla klientów banku z kandydatów przygotowanych wcześniej na podstawie kilku dokumentów.

        Zasady:
        - Wybierz dokładnie {itemCount} najważniejszych dla klienta pytań z listy kandydatów, bez powtórzeń.
        - Możesz połączyć kilku kandydatów w jedno pytanie albo przeredagować pytanie i odpowiedź, ale nie dodawaj faktów, których nie ma w kandydatach.
        - Jeśli kandydat mówi „Dokument nie rozstrzyga …”, zachowaj to stwierdzenie.
        - W polu „basedOn” podaj identyfikatory wykorzystanych kandydatów (np. D2-K3).
        - W polu „sources” podaj źródła: identyfikator dokumentu (np. D2) i jednostkę przepisaną z kandydatów albo pusty tekst; każdy dokument źródła musi być dokumentem któregoś z kandydatów z „basedOn”.
        - Pisz po polsku, jasno i zwięźle.
        - Odpowiedz wyłącznie obiektem JSON zgodnym ze schematem.
        """);

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
