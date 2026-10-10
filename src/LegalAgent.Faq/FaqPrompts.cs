using System.Globalization;
using System.Runtime.CompilerServices;
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
        - W polu „unit” podaj jednostkę redakcyjną (nagłówek), z której pochodzi odpowiedź, przepisaną dokładnie z listy „Jednostki dokumentu” podanej pod dokumentem, albo pusty tekst, gdy odpowiedź nie pochodzi z jednej jednostki lub lista jest pusta. Nie twórz oznaczeń, których nie ma na liście (np. „§ 6”, gdy dokument nie ma paragrafów).
        - W polu „quote” przepisz dosłownie z dokumentu jeden ciągły fragment (co najmniej 3 słowa, najlepiej całe zdanie, bez wielokropków, skrótów i zmian słów), który potwierdza odpowiedź; fragment musi pochodzić z jednostki podanej w „unit”. Kandydat bez takiego fragmentu zostanie odrzucony.
        - Podawaj w odpowiedzi, kogo dotyczy zasada (np. konsumenta, osobę fizyczną nieprowadzącą działalności gospodarczej, firmę, klienta Private Banking), oraz jej warunki i wyjątki, jeśli dokument je określa.
        - Liczby (kwoty, terminy, godziny, daty) zapisuj w odpowiedzi tak jak w dokumencie; liczba, której nie ma w tekście jednostki, odrzuca kandydata.
        - Odpowiedz wyłącznie obiektem JSON zgodnym ze schematem.
        """);

    /// <summary>User message of the candidate step: identifier, name, source, the full Markdown and the units it may cite.</summary>
    public static string CandidatesUser(FaqSourceDocument document, string markdown, IReadOnlyList<string> units)
    {
        var text = new StringBuilder()
            .Append("Dokument ").Append(document.Id).Append(": ").Append(document.Name).Append('\n')
            .Append("Źródło: ").Append(document.Resource.OriginalString).Append("\n\n")
            .Append(markdown);
        if (!markdown.EndsWith('\n'))
        {
            text.Append('\n');
        }

        if (units.Count == 0)
        {
            return text.Append("\nJednostki dokumentu ").Append(document.Id).Append(": brak — w polu „unit” podaj pusty tekst.\n").ToString();
        }

        text.Append("\nJednostki dokumentu ").Append(document.Id).Append(" (pole „unit” przepisz dokładnie z tej listy albo podaj pusty tekst):\n");
        foreach (string unit in units)
        {
            text.Append("- ").Append(OneLine(unit)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>System message of the selection step.</summary>
    public static string SelectionSystem(int itemCount, int minPerDocument, int maxPerDocument) => string.Create(
        CultureInfo.InvariantCulture,
        $"""
        Układasz końcowe FAQ dla klientów banku z kandydatów przygotowanych wcześniej na podstawie kilku dokumentów.

        Zasady:
        - Wybierz od {itemCount} do {itemCount + PoolExtra} najważniejszych dla klienta pytań z listy kandydatów, bez powtórzeń, i uszereguj je od najważniejszego. Program wybierze z nich {itemCount} pozycji FAQ, tak żeby każdy dokument był źródłem od {minPerDocument} do {maxPerDocument} pozycji (pozycja liczy się dla każdego dokumentu swoich kandydatów z „basedOn”).
        - Każde pytanie dotyczy jednej sprawy. Łącz kandydatów tylko wtedy, gdy dotyczą tej samej sprawy (np. z różnych dokumentów); nie łącz różnych tematów w jedno pytanie.
        - Możesz połączyć kilku kandydatów o tę samą sprawę w jedno pytanie albo przeredagować pytanie i odpowiedź, ale nie dodawaj faktów, których nie ma w kandydatach, i nie zmieniaj warunków ani wyjątków.
        - {PoolPerDocument(minPerDocument)}
        - Zachowaj w odpowiedzi, kogo dotyczy zasada (np. konsument, osoba fizyczna nieprowadząca działalności gospodarczej, firma, klient Private Banking), oraz jej warunki i wyjątki, jeśli podają je kandydaci.
        - Liczby przepisuj z kandydatów; liczba, której nie ma w kandydatach z „basedOn”, odrzuca odpowiedź.
        - Jeśli kandydat mówi „Dokument nie rozstrzyga …”, zachowaj to stwierdzenie.
        - W polu „basedOn” podaj identyfikatory wykorzystanych kandydatów (np. D2-K3).
        - Źródła pozycji (dokument i jednostka) zostaną wzięte z kandydatów wskazanych w „basedOn”, więc wskaż wszystkich kandydatów, z których pochodzi odpowiedź.
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

    /// <summary>User message asking once for a corrected selection (T067i): the problems and the required item count.</summary>
    public static string SelectionCorrection(IReadOnlyList<string> problems, int itemCount)
    {
        var text = new StringBuilder("Twoja odpowiedź została odrzucona:\n");
        foreach (string problem in problems)
        {
            text.Append("- ").Append(problem).Append('\n');
        }

        return text.Append(CultureInfo.InvariantCulture, $"Popraw ją i odpowiedz ponownie pełnym obiektem JSON zgodnym ze schematem, z co najmniej {itemCount} poprawnymi pozycjami uszeregowanymi od najważniejszej.\n")
            .ToString();
    }

    /// <summary>Extra ranked items asked for beyond the item count, so the program can keep the documents balanced (T067l).</summary>
    public const int PoolExtra = 5;

    /// <summary>The pool rule per document: one more than the minimum, so the program has a choice (T067l).</summary>
    private static string PoolPerDocument(int min) => min > 0
        ? FormattableStringFactory.Create("Podaj z każdego dokumentu co najmniej {0} pozycje.", min + 1).ToString(CultureInfo.InvariantCulture)
        : "Uwzględnij różne dokumenty.";

    private static string OneLine(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
