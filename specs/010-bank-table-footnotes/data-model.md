# Data Model: spec 010

Zmiany publicznego modelu `LegalAgent.PdfParser.Model` (MINOR, 1.3.0) i struktur wewnętrznych. Uzupełnia
`specs/001-legal-pdf-parser/data-model.md` (sekcja tabel) i `specs/004-document-chunking/data-model.md`.

## Model publiczny

### TableCell (zmiana)

```text
TableCell(IReadOnlyList<Inline> Inlines, int ColumnSpan = 1, int RowSpan = 1, bool ContinuesAbove = false)
```

| Pole | Znaczenie | Reguły |
|---|---|---|
| `Inlines` | tekst komórki | w komórce `ContinuesAbove` — te same inline'y co w komórce początkowej (ta sama lista) |
| `ColumnSpan` | liczba objętych kolumn | ≥ 1; bez zmian |
| `RowSpan` | liczba wierszy objętych komórką scaloną pionowo | ≥ 1; > 1 tylko w pierwszym objętym wierszu; wiersze objęte leżą w tym samym `TableBlock.Rows` (scalenie nie przekracza strony — przy łączeniu przez strony każda część ma własne scalenia) |
| `ContinuesAbove` | komórka jest kontynuacją komórki scalonej z wiersza wyżej | `true` tylko w wierszach 2…`RowSpan` grupy; komórka ma `RowSpan` = 1 i ten sam `ColumnSpan` co początkowa |

Niezmienniki (rozszerzenie FR-061 spec 001):
- suma `ColumnSpan` w każdym wierszu = `ColumnCount` (bez zmian — kontynuacje zajmują swoje kolumny);
- dla komórki z `RowSpan` = k w wierszu r i kolumnie c: wiersze r+1…r+k−1 mają w kolumnie c komórkę
  `ContinuesAbove` = true z tymi samymi `Inlines`; wiersz r+k (jeśli istnieje) nie ma tam kontynuacji;
- komórka nagłówka (`TableBlock.Header`) nigdy nie ma `RowSpan` > 1 ani `ContinuesAbove`.

### TableBlock (zmiana)

```text
TableBlock(PageRange Pages, TableRow? Header, IReadOnlyList<TableRow> Rows, int ColumnCount, bool IsFallback,
           TableRow? HeaderGroup = null)
```

| Pole | Znaczenie | Reguły |
|---|---|---|
| `HeaderGroup` | wiersz grupujący nad nagłówkiem (np. „Gdzie można zrobić?” nad kolumnami 2–4) | tylko gdy `Header` ≠ null i `IsFallback` = false; suma `ColumnSpan` = `ColumnCount`; komórki z pustymi `Inlines` oznaczają kolumny bez grupy |

### TableRow (bez zmian)

`TableRow(IReadOnlyList<TableCell> Cells) { int? Page }` — dokumentacja niezmiennika rozszerzona o kontynuacje.

## Struktury wewnętrzne

### RuledTableGrid (nowy, `Stages/RuledTableGrid.cs`)

Siatka tabeli z liniami, budowana z odcinków linii poziomych i pionowych w obszarze tabeli.

| Element | Opis |
|---|---|
| `Columns` | x granic kolumn: linie pionowe występujące w ≥ 2 wierszach siatki, łączone z tolerancją 3 pt |
| `RowBoundaries` | y granic wierszy: suma y odcinków poziomych pokrywających ≥ 80% szerokości któregokolwiek pasma |
| `HasBoundary(column, boundary)` | czy w paśmie kolumny na danej granicy jest odcinek linii (podstawa scalenia pionowego) |
| `SpansColumns(row)` | komórki wiersza obejmujące kilka pasm (odcinek nad komórką przez kilka pasm, brak linii pionowej w wierszu) |
| `Locate(word)` | (wiersz, kolumna) słowa: linia bazowa między granicami wiersza, środek x w paśmie |

Wynik: lista komórek siatki (wiersz początkowy, liczba wierszy, kolumna, liczba kolumn, słowa w kolejności linii
bazowych i x) → `RowDraft` → `TableRow`/`TableCell` z `RowSpan`/`ContinuesAbove`.

### Token / RowDraft (`TableDetectionStage`, zmiana)

`Token` przenosi `FootnoteId` słowa; `RowDraft.Inlines` emituje `FootnoteRef(id)` przyklejony do sąsiedniego słowa
(przed albo po, jak w PDF).

### FootnoteDraft / obszar przypisów (`FootnoteDetectionStage`, zmiana)

| Element | Zmiana |
|---|---|
| etykieta definicji | także małe podniesione cyfry na początku linii obszaru (z odstępem lub bez) oraz — tylko pod kreską i dla numerów z odnośnikiem na stronie — goła cyfra `\d{1,3}` ze spacją |
| obszar pod kreską | linie większe niż `MaxSizeRatio` dozwolone, jeśli linie zaczynające definicje mają etykiety z odnośnikami na stronie |
| odnośnik | także prefiks słowa (podniesione małe cyfry przed literą/cyfrą pełnego rozmiaru); etykiety na początku linii obszaru nie są odnośnikami |
| szkic z pustą etykietą | tworzony tylko, gdy pierwsza linia obszaru nie ma żadnej etykiety (jak dotąd), ale nie zagarnia linii z etykietą |

### NumberCheck (testy, zmiana)

`Compare(string markdown, string|byte[] pdf, ConversionReport report, LegalDocument? document = null)` — gdy
`document` podany, liczby z komórek `ContinuesAbove` i z komórek `HeaderGroup` × (objęte kolumny − 1) są odejmowane
od wielozbioru Markdownu.

## Przejścia stanu

Brak (model niemutowalny; scalenia powstają w `TableDetectionStage` i nie zmieniają się w dalszych etapach).
`MergeAcrossPages` dokleja wiersze części z następnej strony bez zmiany ich scaleń; doklejenie kontynuacji komórki
(R6) zmienia `Inlines` ostatniego wiersza poprzedniej części — jeśli ta komórka jest początkiem lub kontynuacją
scalenia, aktualizowane są wszystkie komórki grupy (ta sama lista inline'ów).
