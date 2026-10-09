# Kontrakt: format JSON fragmentów (`schemaVersion` 1)

Wspólny dla serwisu i plików (FR-250, FR-251). Rekordem jest **jeden fragment z metadanymi dokumentu**.

- Plik `*.chunks.jsonl`: jeden rekord na linię, UTF-8 bez BOM, LF, także po ostatniej linii; brak
  wcięć i spacji między tokenami; kolejność linii = kolejność fragmentów w dokumencie. Dokument bez
  fragmentów → pusty plik.
- Odpowiedź serwisu (jeśli zwraca JSON): tablica takich rekordów, w tej samej postaci.
- Kolejność pól jak poniżej. Pole o wartości `null` jest **pomijane** (nie ma `"x": null`); tablice
  puste są zapisywane (`[]`).
- Znaki: polskie litery i `§` dosłownie; escapowane tylko `"`, `\` i znaki sterujące (`\n` w `content`).
  Daty `yyyy-MM-dd`, liczby całkowite bez wykładnika, wartości logiczne `true`/`false`.
- Zmiana łamiąca zgodność (usunięcie/zmiana znaczenia pola) podnosi `schemaVersion` (FR-252); dodanie
  pola opcjonalnego nie podnosi.

## Rekord

```json
{
  "schemaVersion": 1,
  "document": {
    "id": "REG-05-w1",
    "designation": "BP/REG/05",
    "type": "regulation",
    "title": "Regulamin promocji „Konto z premią” Bank Przykładowy S.A.",
    "detectedTitle": "…",
    "version": 1,
    "validFrom": "2024-09-01",
    "validTo": "2025-08-31",
    "status": "outdated",
    "previousVersion": null,
    "source": { "pageCount": 22, "sha256": "…", "isComplete": true, "skippedPages": [] }
  },
  "chunk": {
    "id": "REG-05-w1_3f9a2c1d4e5b6a70_2",
    "unitKey": "BP/REG/05 | § 11",
    "part": 2,
    "partCount": 2,
    "unitKind": "paragraph",
    "citation": "§ 11",
    "listLabels": ["3."],
    "sectionPath": ["Warunki promocji", "§ 11."],
    "pages": { "first": 5, "last": 6 },
    "length": 1180,
    "exceedsLimit": false,
    "content": "### § 11.\n\n- 3\\. Premia jest wypłacana…"
  }
}
```

(Przykład sformatowany dla czytelności; w pliku jedna linia. `previousVersion: null` pokazany tylko
poglądowo — w pliku pole jest pominięte.)

## Pola `document`

| Pole | Typ | Wymagane | Źródło |
|------|-----|----------|--------|
| `id` | string | tak | `DocumentMetadata.DocumentId` |
| `designation` | string | tak | `SeriesKey` (`Designation` ?? `id`) |
| `type`, `title`, `detectedTitle`, `status`, `previousVersion` | string | nie | metadane / parser |
| `version` | int | nie | |
| `validFrom`, `validTo` | data | nie | |
| `source.pageCount` | int | tak | |
| `source.sha256` | string (hex, małe litery) | tak | |
| `source.isComplete` | bool | tak | |
| `source.skippedPages` | int[] | tak | |

## Pola `chunk`

| Pole | Typ | Wymagane | Opis |
|------|-----|----------|------|
| `id` | string | tak | `^[A-Za-z0-9][A-Za-z0-9._-]*_[0-9a-f]{16}_[0-9]+$` |
| `unitKey` | string | tak | data-model.md, R6 |
| `part`, `partCount` | int | tak | 1-based |
| `unitKind` | string | tak | `preamble`, `book`, `part`, `division`, `chapter`, `subchapter`, `article`, `paragraph`, `typographic`, `tableDocumentSection`, `documentTitle` |
| `citation` | string | nie | brak dla wstępu |
| `listLabels` | string[] | tak | dosłowne etykiety, od zewnętrznej |
| `sectionPath` | string[] | tak | |
| `pages.first`, `pages.last` | int | tak | 1-based, `first ≤ last` |
| `length` | int | tak | liczba znaków UTF-16 `content` |
| `exceedsLimit` | bool | tak | |
| `content` | string | tak | Markdown wg kontraktu wyjścia parsera, bez znaczników stron |

## Odczyt

`ChunkJson.ReadLines` odrzuca (`FormatException` z numerem linii) rekord bez `schemaVersion`, z
`schemaVersion` wyższym niż obsługiwany, bez pól wymaganych lub z błędnym typem. Nieznane pola są
ignorowane (zgodność w przód dla pól opcjonalnych).
