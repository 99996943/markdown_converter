# LegalAgent.Faq

Biblioteka, która z kilku regulaminów (PDF) przygotowuje FAQ dla klientów: 10 pytań i odpowiedzi, każda ze
źródłem (dokument i rozdział). Odpowiedzi pisze model językowy, ale biblioteka **nie przyjmuje ich na wiarę** —
każdą propozycję modelu sprawdza w kodzie z tekstem dokumentu, a to, czego nie da się potwierdzić, odrzuca.

Biblioteka zależy tylko od `Microsoft.SemanticKernel.Abstractions` (`IChatCompletionService`) i od
`LegalAgent.PdfParser`. Nie zna Azure, mBanku ani nazw plików — konektor, klucz i zapis pliku są po stronie
aplikacji (`mBank.FaqGenerator`). Specyfikacja: `specs/006-faq-generation/`.

## Użycie

```csharp
// 1. PDF → Markdown (obok każdego PDF-u powstaje <nazwa>.md)
var converter = new DocumentSetConverter(pdfMarkdownConverter);
ConversionRun run = await converter.ConvertAllAsync(sources, progress, ct);

// 2. Wejście modelu: nazwa, adres, Markdown i lista jednostek (nagłówków) każdego dokumentu
FaqDocumentInput[] documents =
[
    .. run.Documents.Select(d => new FaqDocumentInput(
        string.IsNullOrWhiteSpace(d.Title) ? d.MarkdownFileName : d.Title, d.Address, d.Markdown, d.Units)),
];
FaqGenerator.CheckInput(documents, options);   // rozmiar dokumentów — przed pytaniem o klucz

// 3. FAQ
var generator = new FaqGenerator(chat, options, (step, schema) => settingsFor(step, schema));
FaqResult result = await generator.GenerateAsync(documents, progress, ct);
string file = FaqMarkdownRenderer.Render(result, header);
```

`settingsFor` buduje ustawienia zapytania dla kroku i jego schematu JSON (`FaqSchemas.Candidates`,
`FaqSchemas.Selection`); aplikacja przekazuje schemat jako `response_format` w trybie strict.

Opcje (`FaqGeneratorOptions`):

| Opcja | Domyślnie | Znaczenie |
|---|---|---|
| `CandidatesPerDocument` | 10 | najwyżej tylu kandydatów z jednego dokumentu (1–30) |
| `ItemCount` | 10 | liczba pozycji FAQ |
| `MaxItemsPerDocument` | 3 | najwyżej tyle pozycji z jednego dokumentu (podnoszone, gdy dokumentów jest mało) |
| `MinItemsPerDocument` | 1 | co najmniej tyle pozycji z każdego dokumentu (pomijane, gdy się nie da) |
| `MaxDocumentTokens` | 100 000 | szacowany limit tokenów jednego dokumentu (`FaqInputTooLongException`) |
| `CharactersPerToken` | 3,0 | przelicznik do szacunku tokenów |

## Zasada działania

```
PDF ──parser──► Markdown + jednostki ──► krok 1: kandydaci (po 1 zapytaniu na dokument)
                                              │  sprawdzenie odpowiedzi, ugruntowanie w tekście
                                              ▼
                                         krok 2: wybór (1 zapytanie, ewentualnie 1 poprawka)
                                              │  sprawdzenie pozycji, wybór N w kodzie
                                              ▼
                                         FaqResult ──► FAQ_mBank.md (nagłówek YAML + pytania)
```

### 1. Konwersja

`DocumentSetConverter` zamienia każdy PDF parserem `LegalAgent.PdfParser` (domyślne opcje) i zapisuje
`<nazwa>.md` obok PDF-u (zapis atomowy przez plik tymczasowy). Po konwersji wszystkich dokumentów usuwa
nieaktualne `*.md` spoza bieżącej listy. Z modelu dokumentu (`LegalDocument`) zbiera **jednostki** — oznaczenia i
teksty nagłówków wszystkich sekcji („§ 12”, „Rozdział 3”, „6. Jakie informacje musisz podać…”). To słownik, z
którego model może później wskazywać źródła.

### 2. Krok kandydatów

Dla każdego dokumentu D1…Dn osobno (kolejno, nie równolegle) model dostaje cały Markdown dokumentu i na końcu
listę „Jednostki dokumentu Dn”. Ma zaproponować do `CandidatesPerDocument` pytań, które klienci zadają
najczęściej, a dla każdego podać:

- `question`, `answer` — tylko na podstawie dokumentu; gdy dokument nie rozstrzyga sprawy, odpowiedź mówi to wprost
  („Dokument nie rozstrzyga …”); w odpowiedzi ma być, kogo dotyczy zasada (konsument, firma, klient Private
  Banking…), oraz jej warunki i wyjątki;
- `unit` — jednostkę przepisaną z listy albo pusty tekst;
- `quote` — dosłowny, ciągły fragment dokumentu (co najmniej 3 słowa) z tej jednostki, który potwierdza odpowiedź.

Kandydaci dostają identyfikatory `D2-K1`, `D2-K2`, … w kolejności odpowiedzi.

### 3. Krok wyboru

Model dostaje listę dokumentów i wszystkich kandydatów (bez tekstów dokumentów) i zwraca **uszeregowaną pulę** od
N do N+5 pytań — od najważniejszego dla klienta. Może łączyć kandydatów o tę samą sprawę i przeredagowywać, ale
nie dodawać faktów; dla każdej pozycji podaje `basedOn` — identyfikatory kandydatów, z których korzysta.
**Źródeł nie podaje** — liczy je kod.

Ostateczne N pozycji wybiera kod (model nie umie wiarygodnie trafić w dokładne liczby — patrz niżej).

### 4. Plik FAQ

`FaqMarkdownRenderer` składa plik: nagłówek YAML (typ, tytuł, opis, adresy dokumentów, czas, model, wdrożenie) i
pozycje `## pytanie`, odpowiedź, „Źródło: [nazwa dokumentu](adres), jednostka”. Kontrakt:
`specs/006-faq-generation/contracts/faq-file.md`.

## Weryfikacja propozycji modelu

Wszystko poniżej dzieje się w kodzie, bez dodatkowych zapytań do modelu.

### Odpowiedź kroku kandydatów (`FaqResponseParser`, `FaqResponseValidator`)

Cała odpowiedź jest **odrzucana** (`FaqResponseException`, w aplikacji kod 7), gdy:

- nie jest dokładnie obiektem JSON zgodnym ze schematem (bez tekstu wokół, bez dodatkowych pól);
- liczba kandydatów jest spoza 1…`CandidatesPerDocument`;
- pytanie lub odpowiedź jest puste albo pytanie się powtarza.

Nieznana jednostka nie odrzuca całej odpowiedzi — odpada tylko ten kandydat (patrz ugruntowanie niżej).

**Dopasowanie jednostki** (`UnitMatcher`): po normalizacji (spacje, wielkość liter, kropka na końcu) wskazana
jednostka musi być równa jednostce dokumentu albo zaczynać się od niej i dalej mieć spację lub przecinek — „§ 12
ust. 3” pasuje do „§ 12.”, „Art. 5a” nie pasuje do „Art. 5”. Numer nagłówka numerowanego też jest jednostką:
„6” i „6.” pasują do „6. Jakie informacje…”, „§ 6” i „16” nie.

### Ugruntowanie kandydata w tekście (`FaqGrounding`)

Każdy kandydat jest sprawdzany osobno. Najpierw `unit` musi pasować do jednostki dokumentu (`UnitMatcher`) — inaczej
kandydat odpada („kandydat D1-K7: jednostka „Załącznik nr 2” nie występuje w dokumencie D1”). Potem cytat i liczby
są porównywane z **tekstem swojej jednostki**: sekcją Markdown od nagłówka pasującego do
`unit` do następnego nagłówka tego samego lub wyższego poziomu (sekcja z numerem obejmuje też następujące po niej
nagłówki bez numeru, np. „Dodatkowe wyjaśnienia”). Bez jednostki — cały dokument.

| Sprawdzenie | Reguła |
|---|---|
| cytat | co najmniej 3 słowa; ≥ 80% trójek kolejnych słów cytatu występuje w tekście jednostki (fragmenty po wielokropku liczone osobno) |
| liczby | każda liczba (ciąg cyfr) z odpowiedzi występuje w tekście jednostki — kwoty, terminy, godziny, daty |

Porównanie odbywa się po normalizacji: tylko litery i cyfry, małe litery; znaczniki Markdown, cudzysłowy,
komentarze stron `<!-- page: N -->` i etykiety list („a)”, „1)”) nie przeszkadzają. Drobna zmiana słowa przechodzi,
parafraza i tekst z innego rozdziału — nie.

Kandydat, który nie przejdzie, **odpada z ostrzeżeniem** (zdarzenie `CandidateDropped`), np.:

```
[D2] pominięto kandydata D2-K3: liczby „13”, „18” nie występują w jednostce „5. Rachunki dla osób małoletnich”
```

Odpowiedź jest odrzucana dopiero wtedy, gdy z dokumentu nie zostanie żaden kandydat.

### Pozycje wyboru i wybór N w kodzie

Pozycja puli jest **pomijana z ostrzeżeniem** (`SelectionItemSkipped`), gdy ma puste pytanie lub odpowiedź,
powtarza pytanie wcześniejszej pozycji, ma puste `basedOn`, wskazuje nieistniejącego kandydata albo zawiera
liczbę, której nie ma w odpowiedziach ani cytatach jej kandydatów `basedOn`.

Z poprawnych pozycji kod wybiera N:

1. dla każdego dokumentu (D1…Dn) jego najwyżej oceniona pozycja — aż do `MinItemsPerDocument`;
2. potem kolejne pozycje w kolejności modelu, o ile żaden ich dokument nie przekroczy `MaxItemsPerDocument`
   (pozycja liczy się dla każdego dokumentu swoich kandydatów);
3. wynik zostaje w kolejności modelu i jest numerowany 1…N.

**Źródła** pozycji to dokument i jednostka każdego kandydata `basedOn` (już sprawdzone w kroku kandydatów), bez
powtórzeń — model nie może więc wskazać nieistniejącego paragrafu.

Gdy poprawnych pozycji jest mniej niż N, dokument nie dostaje minimum albo limit nie pozwala zebrać N, biblioteka
**raz** prosi model o poprawkę: wysyła tę samą rozmowę, odrzuconą odpowiedź i listę problemów („liczba poprawnych
pozycji 9, potrzeba co najmniej 10”). Druga zła odpowiedź kończy generowanie (`FaqResponseException`).

### Co nie jest sprawdzane

Kod potwierdza źródła, cytaty i liczby, ale **nie rozumie znaczenia** odpowiedzi. Model może poprawnie zacytować
rozdział, a w odpowiedzi przeinaczyć warunek bez liczb (np. dopisać „rażące niedbalstwo” tam, gdzie regulamin
mówi tylko „umyślnie”). Na to pomaga tylko przegląd człowieka albo — planowane — sprawdzenie odpowiedzi drugim
zapytaniem.

## Błędy

| Wyjątek | Kiedy |
|---|---|
| `FaqInputTooLongException` | szacunek tokenów dokumentu > `MaxDocumentTokens` (`CheckInput`, przed zapytaniem) |
| `FaqServiceException` | błąd usługi modelu (klucz, wdrożenie, limit 429, czas, sieć) — bez ponowień |
| `FaqResponseException` | odpowiedź łamie reguły; `Step`, `DocumentId`, `Problems` |

Komunikaty są po polsku i nie zawierają treści zapytań.

## Postęp

`IProgress<FaqEvent>` dostaje zdarzenia: `CandidatesStarted` (rozmiar i szacunek tokenów), `CandidateDropped`,
`CandidatesFinished` (liczba kandydatów, zużycie tokenów), `SelectionStarted`, `SelectionCorrection`,
`SelectionItemSkipped`, `SelectionFinished`. Aplikacja wypisuje je w konsoli.

## Testy

`tests/LegalAgent.Faq.Tests` nie łączą się z Azure. `Fakes/FakeChatCompletionService` odpowiada zaplanowanymi
tekstami (lub `Fallback`, który odpowiada poprawnie na każde zapytanie) i zapisuje każde zapytanie; znacznik
`@cytat@` w zaplanowanej odpowiedzi zamienia na zdanie z dokumentu z zapytania. `Fakes/TestPdfs` buduje syntetyczne
regulaminy. Plik FAQ ma golden `Golden/faq.expected.md` (`UPDATE_GOLDEN=1` go przepisuje).

```bash
dotnet test tests/LegalAgent.Faq.Tests
```
