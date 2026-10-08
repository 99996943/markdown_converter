# Data Model: tabela-dokument (spec 002)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-10-08

Zmiany względem `specs/001-legal-pdf-parser/data-model.md`. Wszystkie zmiany modelu publicznego są
**addytywne** (kontrakt publiczny 1.0.0 → 1.1.0). Konwencje bez zmian: punkty PDF, oś Y w dół,
strony od 1.

---

## 1. Publiczny model dokumentu

### SectionKind (rozszerzenie)

| Wartość | Znaczenie |
|---------|-----------|
| `TableDocumentSection` (nowa, na końcu enum) | Sekcja tabeli-dokumentu: nagłówek = nazwa z lewej komórki (FR-083). `Designation = null`, `Number = null`, `Title = HeadingText` = nazwa, `Level` = 2 (tytuł dokumentu ma zawsze poziom 1). |

Reguły sekcji `TableDocumentSection`:
- `Blocks` = treść prawej komórki wszystkich wierszy sekcji (także z kolejnych stron) w kolejności
  czytania: `ParagraphBlock` (w tym pogrubione śródtytuły — `TextRun` ze stylem `Bold`), `ListBlock`.
  Nigdy `TableBlock`.
- Tekst po ostatnim wierszu tabeli-dokumentu (FR-087) należy do `Blocks` ostatniej sekcji.
- `Pages` = od strony nagłówka do ostatniej strony treści.
- Treść przed tytułem i między tytułem a pierwszą sekcją (okładka, linia „Obowiązuje od …”, podpis
  grafiki) trafia do `LegalDocument.Preamble`.

### ConversionReport (rozszerzenie)

| Pole | Typ | Opis |
|------|-----|------|
| `TableDocuments` (nowe) | `IReadOnlyList<TableDocumentSummary>` | Jedna pozycja na rozpoznaną tabelę-dokument, w kolejności stron; pusta lista, gdy brak. |

### TableDocumentSummary (nowy rekord)

| Pole | Typ | Opis / reguły |
|------|-----|---------------|
| `FirstPage` | `int` | Pierwsza strona regionu. |
| `LastPage` | `int` | Ostatnia strona regionu; `LastPage ≥ FirstPage + 1` (min. 2 strony). |
| `SectionCount` | `int` | Liczba sekcji (wierszy z niepustą lewą komórką; nazwa przerwana granicą strony liczy się raz). |
| `HeaderRowText` | `string?` | Tekst pominiętego wiersza nazw kolumn, komórki rozdzielone „ \| ” (np. „Definicje \| Wyjaśnienie”); `null`, gdy brak wiersza nazw. |
| `DroppedHeaderRows` | `int` | Liczba pominiętych wystąpień wiersza nazw kolumn (0, gdy brak). |

`HeadingCounts` obejmuje nagłówki sekcji tabeli-dokumentu jak każde inne. `TableCount` i
`FallbackTableCount` NIE liczą tabeli-dokumentu.

---

## 2. Opcje (`PdfParserOptions`) — nowe pola

| Grupa | Pole (domyślnie) | Walidacja | FR |
|-------|------------------|-----------|----|
| `Tables` | `DetectTableDocuments` (true) | — | FR-080, FR-005 |
| `Tables` | `TableDocumentMaxLeftColumnRatio` (0,35) | (0, 1] | FR-080 b |
| `Tables` | `TableDocumentMinPages` (2) | ≥ 2 | FR-080 c |
| `Tables` | `TableDocumentMinPageRatio` (0,5) | (0, 1] | FR-080 c |
| `Tables` | `TableDocumentMinMedianWords` (40) | ≥ 1 | FR-080 e |
| `Headings` | `DetectImageCaptions` (true) | — | FR-088 |
| `Headings` | `ValidityLineAsParagraph` (true) | — | FR-093 |

`DetectTableDocuments = false` ⇒ wynik identyczny z feature 001 (z wyjątkiem FR-088, FR-093, FR-094 i
punktora „o”, które mają własne przełączniki lub są ogólną poprawką wierności tekstu).

---

## 3. Wewnętrzny model układu

| Typ / element | Zmiana | Ustawia | Czyta |
|---------------|--------|---------|-------|
| `LayoutGlyph.FontName` | nowy parametr opcjonalny `string? FontName = null` (nazwa z PDF, z prefiksem podzbioru) | PageExtraction | ListDetection (R10) |
| `LayoutPage.ImageAreas` | nowa lista `IList<Rect>` — prostokąty obrazów (Y w dół) | PageExtraction | HeadingDetection (FR-088) |
| `PipelineContext.TableDocuments` | nowa lista `IList<TableDocumentRegion>` | TableDocument (zapisuje też `Report.TableDocuments` przez `ReportBuilder.AddTableDocument`) | HeadingDetection |
| `TableDocumentRegion` (internal) | `Index`, `FirstPage`, `LastPage`, `Top` (górna krawędź ramki na pierwszej stronie), `Divider`, `ContentLeft`, `ContentRight`, `SectionCount`, `HeaderRowText?`, `DroppedHeaderRows` | TableDocument | j.w. |
| `LayoutAnnotations.TableDocumentIndex` = `"tabledoc.index"` | nowa stała; na wszystkich liniach w ramce regionu (nazwy, treść, wiersz nazw kolumn) | TableDocument | TableDetection, ReadingOrder (pomijają), BlockAssembly (R9) |
| `LayoutAnnotations.ColumnLeft/Right` | ustawiane także na liniach prawej kolumny tabeli-dokumentu (`ContentLeft`/`ContentRight`) | TableDocument | BlockAssembly, HeadingDetection |
| `HeadingInfo` | bez zmian; `Kind = TableDocumentSection`, `Level = 2` | TableDocument | BlockAssembly, DocumentBuild |

### Robocze struktury etapu `TableDocumentStage` (prywatne)

| Struktura | Pola | Uwagi |
|-----------|------|-------|
| `PageFrame` | `Page`, `Left`, `Divider`, `Right`, `Top`, `Bottom`, `RowEdges: double[]` (rosnąco, z `Top` i `Bottom`) | R2; brak ramki ⇒ strona poza regionem. |
| `FrameRow` | `Frame`, `Top`, `Bottom`, `LeftLines`, `RightLines`, `IsHeaderRow`, `ContinuesName` | R4–R6. |

### Przejścia stanu linii (uzupełnienie)

```text
Unknown ──(TableDocument)──► Heading (linie nazwy sekcji; pierwsza z HeadingInfo)
                          ├─► Artifact (wiersz nazw kolumn)
                          └─► Unknown z `tabledoc.index` + `column.*` (treść prawej kolumny → listy/akapity)
```

Linie dzielone na granicy kolumn są zastępowane w `page.Lines` przez wycinki (`LineSlicer`); oryginał
znika z listy (jak w FR-067).

---

## 4. Kolejność etapów (uzupełnienie)

| Order | Etap | FR |
|-------|------|----|
| 550 | `StepSequenceStage` | FR-067 |
| **560** | **`TableDocumentStage`** (nowy; `StageOrder.TableDocument`) | FR-080 – FR-084, FR-089, FR-090 |
| 600 | `TableDetectionStage` — pomija linie z `tabledoc.index` | FR-081, FR-089 |
| 700 | `ReadingOrderStage` — pomija linie z `tabledoc.index` | FR-081 |
| 800 | `ListDetectionStage` — punktor „o” w innej czcionce | FR-085 |
| 900 | `HeadingDetectionStage` — FR-086/087 (brak typograficznych od początku tabeli-dokumentu), FR-088, FR-093 | |
| 1000 | `BlockAssemblyStage` — R9 (koniec akapitu „zmieściłoby się”, granica pogrubienia) | FR-085, FR-086 |

Pozostałe zmiany poza etapami: `PageExtractionStage` (100: `FontName`, `ImageAreas`),
`Hyphenation` (FR-094).
