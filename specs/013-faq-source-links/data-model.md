# Data Model: linki źródeł FAQ

Wszystkie zmiany addytywne (nowe typy, nowe opcjonalne właściwości `init`); istniejące konstruktory bez zmian.

## FaqUnitLocation (nowy, `src/LegalAgent.Faq/Model/FaqUnitLocation.cs`)

| Pole | Typ | Znaczenie |
|---|---|---|
| `Unit` | string | pozycja listy jednostek (oznaczenie albo tekst nagłówka, pierwsze wystąpienie) |
| `Page` | int? | strona PDF-u, na której zaczyna się sekcja jednostki (`Section.Pages.First`); `null` gdy nieznana |
| `Anchor` | string? | kotwica nagłówka sekcji w Markdownie dokumentu (bez `#`); `null` gdy nie da się jej wyliczyć |

## UnitExtractor (rozszerzony, `Conversion/UnitExtractor.cs`)

- `FromDocument(LegalDocument)` bez zmian (lista jednostek).
- Nowe `Locate(LegalDocument document, string markdown)` → `IReadOnlyList<FaqUnitLocation>`: jedna pozycja na
  jednostkę listy, w tej samej kolejności co `FromDocument`, ze stroną i kotwicą sekcji (R1). Kotwice z `MarkdownAnchors` dla nagłówków `markdown`; brak zgodności liczby nagłówków z sekcjami → wszystkie
  `Anchor` = `null`.

## ConvertedDocument (rozszerzony, `Conversion/Model/ConversionModels.cs`)

| Pole | Typ | Status |
|---|---|---|
| `UnitLocations` | `IReadOnlyList<FaqUnitLocation>`, `init`, domyślnie puste | nowe; wypełnia `DocumentSetConverter` |

## FaqDocumentInput (rozszerzony)

| Pole | Typ | Status | Znaczenie |
|---|---|---|---|
| `UnitLocations` | jak wyżej, `init`, domyślnie puste | nowe | położenie jednostek |
| `MarkdownLink` | string?, `init` | nowe | gotowy adres Markdownu dokumentu względem pliku FAQ (od aplikacji, R5); `null` → bez linków do nagłówków |

## FaqSourceDocument (rozszerzony)

`UnitLocations` i `MarkdownLink` przepisane z `FaqDocumentInput` przez `FaqGenerator` (kolejność D1…Dn).

## MarkdownAnchors (nowy, `src/LegalAgent.Faq/MarkdownAnchors.cs`)

- `Slug(string headingText)` — algorytm R3 (bez licznika powtórzeń).
- `ForDocument(string markdown)` → kotwice wszystkich nagłówków ATX w kolejności, z dopiskami `-1`, `-2` dla powtórzeń.

## UnitMatcher (rozszerzony)

- Nowe `Find(string cited, IEnumerable<string> units)` → pierwsza w kolejności pasująca pozycja listy albo `null`
  (R2); renderer woła je z `UnitLocations.Select(l => l.Unit)`.

## Wiersz źródła (FaqMarkdownRenderer)

Dla źródła z jednostką `u`, dokumentu `d`, pozycji `p = UnitMatcher.Find(u, d.UnitLocations.Select(l => l.Unit))` i położenia `L` = pozycja `d.UnitLocations` z `Unit == p`:

| Warunek | Nazwa dokumentu | Jednostka |
|---|---|---|
| `L.Page` znane, adres bez `#` | `[Name](Resource#page=N)` | — |
| inaczej | `[Name](Resource)` | — |
| `L.Anchor` i `d.MarkdownLink` znane | — | `[u](MarkdownLink#Anchor)` |
| inaczej | — | `u` (jak dziś) |

Źródło bez jednostki: wiersz jak dziś.
