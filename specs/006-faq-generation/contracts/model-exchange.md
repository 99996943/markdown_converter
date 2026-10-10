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
  nie twórz oznaczeń spoza listy (T067a: regulaminy mBanku nie mają § ani Art., a model podawał „§ 6”).

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
        "required": ["question", "answer", "unit"],
        "properties": {
          "question": { "type": "string" },
          "answer": { "type": "string" },
          "unit": { "type": "string" }
        }
      }
    }
  }
}
```

Kandydaci dostają identyfikatory `D2-K1`, `D2-K2`, … w kolejności z odpowiedzi.

## Krok 2 — wybór (jedno zapytanie)

**Komunikat systemowy** musi zawierać:
- zadanie: wybór dokładnie N (`ItemCount` = 10) najważniejszych pytań dla klienta z listy kandydatów; dozwolone
  połączenie lub przeredagowanie kandydatów, zakaz dodawania faktów, których nie ma w kandydatach;
- wymóg wskazania `basedOn` (identyfikatory kandydatów) i `sources` (identyfikator dokumentu i jednostka
  przepisana z kandydatów);
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
        "required": ["question", "answer", "basedOn", "sources"],
        "properties": {
          "question": { "type": "string" },
          "answer": { "type": "string" },
          "basedOn": { "type": "array", "items": { "type": "string" } },
          "sources": {
            "type": "array",
            "items": {
              "type": "object",
              "additionalProperties": false,
              "required": ["documentId", "unit"],
              "properties": {
                "documentId": { "type": "string" },
                "unit": { "type": "string" }
              }
            }
          }
        }
      }
    }
  }
}
```

Liczba elementów nie jest wymuszana schematem (`minItems`/`maxItems` nie są obsługiwane w trybie strict wszystkich
modeli). Wymusza ją walidacja (`data-model.md`, „Reguły sprawdzania”).

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
