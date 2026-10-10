# Data Model: Regulaminy z etykietami w wysuniętej kolumnie i paragrafami „§ N”

Zmiany są addytywne względem modelu z 001/002. Nie powstają nowe typy publiczne poza dwiema wartościami wyliczenia.

## Model publiczny (`LegalAgent.PdfParser.Model`)

### `ListLabelKind` (enum) — nowe wartości

| Wartość | Przykłady | Ranga zagnieżdżenia | Uwagi |
|---|---|---|---|
| `ArabicSlash` | „1/”, „12/”, „1a/” | 2 (jak `ArabicParen`) | cały token: 1–3 cyfry, opcjonalna litera, „/”; po nim tekst |
| `LetterSlash` | „a/”, „b/”, „aa/” | 3 (jak `LetterParen`) | 1–2 małe litery + „/”; po nim tekst |

Nie są etykietami: „7/2017”, „13/36”, „4/49”, „Klient/Klienci”, „km/h”, samotne „i/”, token bez tekstu po nim.

### `ListItem`

Bez zmian w kształcie. Dla elementu słowniczka (US3):
- `Label` — etykieta dosłownie („1/” albo „1.”);
- `Spans` — pogrubiony termin (jak w źródle), spacja, definicja (wiersze scalone);
- `Children` — wyliczenia z definicji („a/”, „b/” …).

### `Section` (jednostka „§ N”)

Bez zmian w kształcie; nowe sytuacje wypełnienia:
- goły wiersz „§ 5” → `Designation` = „§ 5”, `HeadingText` = „§ 5”;
- wiersz z tytułem „§ 3. Porady ogólne” → `Designation` = „§ 3”, `HeadingText` = „§ 3. Porady ogólne” (bez
  przenoszenia tytułu do treści);
- `Level` = poziom otwartego numerowanego rozdziału („2. Rachunki…”) + 1, gdy taki rozdział jest rodzicem; w innych
  przypadkach dotychczasowe zasady `AssignLevels` (bez luk).

## Model wewnętrzny (etapy)

### Adnotacje wierszy (`Layout/LayoutAnnotations.cs`) — nowe

| Klucz | Wartości | Ustawia | Czyta | Znaczenie |
|---|---|---|---|---|
| `deflist.entry` | numer wpisu w obrębie dokumentu (od 1) | TableDetection | ListDetection, ReadingOrder | wiersz należy do wpisu słowniczka |
| `deflist.side` | `term` / `definition` | TableDetection | ListDetection | strona wpisu: etykieta + termin / definicja |

Wiersze z `deflist.*` nie dostają roli `Table`; ReadingOrder porządkuje je jako: wiersze `term` wpisu, potem wiersze
`definition` w kolejności y.

### Rozpoznanie słowniczka (warunki)

- region bez linii pionowych, z ≥ 2 liniami poziomymi podzielonymi na wspólnym x (granica kolumn);
- lewa strona każdego wpisu: etykieta (`ArabicSlash`, `ArabicDot`, `ArabicParen`) i pogrubiony termin;
- wpis = pas między kolejnymi liniami; pierwszy wpis od wiersza przecinającego granicę kolumn (zdanie wstępne) albo
  od poprzedzającego nagłówka; strona bez górnej linii kontynuuje poprzedni wpis.

### Wiersz jednostki w regionie bez siatki

Wiersz pasujący do oznaczenia jednostki (także gołe „§ N”) nie jest dołączany do tabeli bez siatki nad ziarnem, a
region jest przed nim cięty (C2).

## Miary (pomocnik testowy, R4)

| Miara | Definicja |
|---|---|
| `Tbl001` | liczba ostrzeżeń `TBL001_AmbiguousGrid` w raporcie |
| `PipeRows` | wiersze Markdown zawierające „ \| ” |
| `LooseLabelRows` | wiersze zaczynające się etykietą „N.”, „N/”, „x/” poza elementem listy |
| `ParagraphText` | samodzielne wiersze „§ N” (opcjonalnie pogrubione, z tytułem) poza nagłówkami |
| `TocHeadings` | nagłówki zakończone kropkami prowadzącymi i numerem |
| `PageFooters` | wystąpienia „N/M” poza tabelami |
