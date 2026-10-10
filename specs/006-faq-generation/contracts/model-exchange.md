# Kontrakt: wymiana z modelem (kroki kandydatów i wyboru)

Biblioteka `LegalAgent.Faq` buduje `ChatHistory` (komunikat systemowy + komunikat użytkownika) i oczekuje odpowiedzi
JSON zgodnej ze schematem. Schematy są publicznymi stałymi `FaqSchemas.Candidates` i `FaqSchemas.Selection`.
Aplikacja przekazuje je jako `response_format` typu `json_schema` z `strict: true` (research R4). Treść poleceń
jest po polsku. Dokładne brzmienie poleceń należy do implementacji. Wymagania poniżej są obowiązkowe i każde ma
test sprawdzający, że polecenie je zawiera.

## Krok 1 — kandydaci (jedno zapytanie na dokument, kolejno D1…D5)

**Komunikat systemowy** musi zawierać:
- rolę: przygotowanie FAQ dla klientów banku na podstawie jednego dokumentu;
- zakaz informacji spoza dokumentu oraz nakaz napisania wprost „Dokument nie rozstrzyga …”, gdy odpowiedź nie
  wynika z tekstu (konstytucja, zasada II);
- język polski;
- liczbę: „co najwyżej N pytań” (`CandidatesPerDocument`);
- jednostkę: przepisz jednostkę dokładnie z listy „Jednostki dokumentu” pod dokumentem albo podaj pusty tekst;
  nie twórz oznaczeń spoza listy (T067a: regulaminy mBanku nie mają § ani Art., a model podawał „§ 6”);
- cytat (T067d): w polu „quote” dosłowny fragment dokumentu (co najmniej 3 słowa) z jednostki „unit”, który
  potwierdza odpowiedź; liczby w odpowiedzi zapisane tak jak w dokumencie;
- zastrzeżenia (T067k): kogo dotyczy zasada (konsument, firma, klient Private Banking…), jej warunki i wyjątki.

**Komunikat użytkownika**:

```text
Dokument D2: <Name>
Źródło: <Resource>

<pełny Markdown dokumentu>

Jednostki dokumentu D2 (pole „unit” przepisz dokładnie z tej listy albo podaj pusty tekst):
- <Units[0]>
- <Units[1]>
```

Pozycje to `FaqDocumentInput.Units` (każda w jednym wierszu). Gdy lista jest pusta, zamiast niej jest wiersz
„Jednostki dokumentu D2: brak — w polu „unit” podaj pusty tekst.”.

**Schemat odpowiedzi** (`FaqSchemas.Candidates`):

```json
{
  "type": "object",
  "additionalProperties": false,
  "required": ["candidates"],
  "properties": {
    "candidates": {
      "type": "array",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": ["question", "answer", "unit", "quote"],
        "properties": {
          "question": { "type": "string" },
          "answer": { "type": "string" },
          "unit": { "type": "string" },
          "quote": { "type": "string" }
        }
      }
    }
  }
}
```

Kandydaci dostają identyfikatory `D2-K1`, `D2-K2`, … w kolejności z odpowiedzi.

## Krok 2 — wybór (jedno zapytanie)

**Komunikat systemowy** musi zawierać:
- zadanie (T067l): wybór od N do N+5 (`ItemCount` = 10, `FaqPrompts.PoolExtra` = 5) najważniejszych pytań dla
  klienta z listy kandydatów, uszeregowanych od najważniejszego; program wybierze z nich N pozycji z od min do max
  pozycjami na dokument; z każdego dokumentu co najmniej min+1 pozycji; dozwolone połączenie lub przeredagowanie
  kandydatów, zakaz dodawania faktów, których nie ma w kandydatach;
- jedno pytanie = jedna sprawa: łączyć tylko kandydatów o tę samą sprawę, nie łączyć różnych tematów (T067c);
  liczby przepisywane z kandydatów `basedOn`;
- zastrzeżenia z kandydatów zachowane: kogo dotyczy zasada, warunki, wyjątki (T067k);
- wymóg wskazania `basedOn` (identyfikatory wszystkich wykorzystanych kandydatów); model nie podaje źródeł —
  źródła pozycji liczy kod z kandydatów `basedOn` (T067b: model przeredagowywał jednostki źródeł);
- język polski.

**Komunikat użytkownika**:

```text
Dokumenty:
D1: <Name> — <Resource>
…
D5: <Name> — <Resource>

Kandydaci:
[D1-K1] (D1, § 3) Pytanie: … | Odpowiedź: …
[D1-K2] (D1) Pytanie: … | Odpowiedź: …
…
```

**Schemat odpowiedzi** (`FaqSchemas.Selection`):

```json
{
  "type": "object",
  "additionalProperties": false,
  "required": ["items"],
  "properties": {
    "items": {
      "type": "array",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": ["question", "answer", "basedOn"],
        "properties": {
          "question": { "type": "string" },
          "answer": { "type": "string" },
          "basedOn": { "type": "array", "items": { "type": "string" } }
        }
      }
    }
  }
}
```

**Poprawka (T067i, jedna)**: gdy odpowiedź wyboru nie jest JSON-em zgodnym ze schematem albo łamie reguły, to samo
zapytanie jest wysyłane raz jeszcze z dopisanymi wiadomościami: odrzucona odpowiedź (`assistant`) i komunikat
użytkownika:

```text
Twoja odpowiedź została odrzucona:
- liczba poprawnych pozycji 9, potrzeba co najmniej 10
Popraw ją i odpowiedz ponownie pełnym obiektem JSON zgodnym ze schematem, z co najmniej 10 poprawnymi pozycjami uszeregowanymi od najważniejszej.
```

Druga odrzucona odpowiedź kończy generowanie (`FaqResponseException`, kod 7) z jej problemami. Krok kandydatów nie
ma poprawki; błędy usługi nie są ponawiane (FR-424).

Liczba elementów nie jest wymuszana schematem (`minItems`/`maxItems` nie są obsługiwane w trybie strict wszystkich
modeli). Model nie liczy dokładnie (przebiegi 5 i 7: 9 zamiast 10, 4 pozycje z jednego dokumentu), więc ostateczne N
pozycji wybiera kod z uszeregowanej puli (`data-model.md`, „Reguły sprawdzania”, T067l).

## Ustawienia zapytania

Każde zapytanie wysyła ustawienia z sekcji `AzureOpenAI`:
- `max_tokens` / `max_completion_tokens` = `MaxOutputTokens`;
- `temperature`, jeśli ustawiona;
- `seed`, jeśli ustawiony.

Aplikacja tworzy `AzureOpenAIPromptExecutionSettings` przez fabrykę
`Func<FaqStep, string schemaJson, PromptExecutionSettings>` przekazaną do `FaqGenerator`. Biblioteka nie zna typów
Azure.

## Zużycie tokenów

Biblioteka czyta `ChatMessageContent.Metadata["Usage"]`. Gdy metadane mają znany kształt (właściwości
`InputTokenCount`/`OutputTokenCount` albo `PromptTokens`/`CompletionTokens`), są sumowane. W przeciwnym razie
zużycie to `null` i jest wypisywane jako „nieznane”. Brak metadanych nie jest błędem.
