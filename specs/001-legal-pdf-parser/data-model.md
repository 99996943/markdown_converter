# Data Model: LegalAgent.PdfParser

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-10-07

Model dzieli się na dwie warstwy:

1. **Publiczny model dokumentu** (namespace `LegalAgent.PdfParser.Model`) — niezmienne rekordy
   zwracane wywołującemu; część kontraktu publicznego (zmiany łamiące = MAJOR).
2. **Wewnętrzny model układu** (namespace `LegalAgent.PdfParser.Layout`) — mutowalne struktury
   robocze potoku w `PipelineContext`; publiczne tylko w zakresie potrzebnym do pisania własnych
   etapów (oznaczone jako API zaawansowane).

Wszystkie współrzędne w punktach PDF (1/72 cala), oś Y rosnąca w dół po normalizacji
(0 = góra strony), numery stron od 1.

---

## 1. Publiczny model dokumentu

### PdfConversionResult

| Pole | Typ | Opis / reguły |
|------|-----|---------------|
| `Document` | `LegalDocument` | Ustrukturyzowany model (FR-002). |
| `Markdown` | `string` | Rendering `Document` wg bieżących `RenderingOptions`; LF. |
| `Report` | `ConversionReport` | Diagnostyka (FR-070). |
| `IsComplete` | `bool` | `false` ⇔ `Report.SkippedPages` niepuste (FR-009a, FR-071). |

### LegalDocument

| Pole | Typ | Opis / reguły |
|------|-----|---------------|
| `Source` | `SourceInfo` | Metadane źródła. |
| `Title` | `string?` | Tytuł wykryty (nagłówek poziomu 1) lub z metadanych PDF; `null`, gdy brak. |
| `Preamble` | `IReadOnlyList<ContentBlock>` | Treść przed pierwszą sekcją. |
| `PreambleFootnotes` | `IReadOnlyList<Footnote>` | Przypisy z odnośnikiem w tytule/preambule (FR-026). |
| `Sections` | `IReadOnlyList<Section>` | Sekcje najwyższego poziomu. |

### SourceInfo

| Pole | Typ | Opis |
|------|-----|------|
| `SourceId` | `string?` | Z żądania (nazwa pliku/URL); nie jest interpretowane. |
| `PageCount` | `int` | Liczba stron PDF. |
| `PdfTitle` | `string?` | Z metadanych `/Info`. |
| `ByteLength` | `long` | Rozmiar wczytanych danych. |
| `Sha256` | `string` | Skrót wejścia (hex, małe litery) — do śledzenia źródła i deduplikacji. |

### Section

| Pole | Typ | Opis / reguły |
|------|-----|---------------|
| `Level` | `int` | 1–6; poziom nagłówka Markdown (FR-042, FR-043). |
| `Kind` | `SectionKind` | `DocumentTitle`, `Book`, `Part`, `Division` (Dział), `Chapter` (Rozdział), `Subchapter` (Oddział), `Article` (Art.), `Paragraph` (§), `Typographic`. |
| `Designation` | `string?` | Oznaczenie dosłowne: „Rozdział 3”, „Art. 12a”, „§ 5¹”; `null` dla `Typographic`. |
| `Number` | `string?` | Sam numer: „3”, „12a”, „5¹”, „II”. |
| `Title` | `string?` | Tytuł jednostki („Ochrona konsumenta”); `null` gdy brak. |
| `HeadingText` | `string` | Pełny tekst nagłówka renderowany po `#…` (np. „Rozdział 3. Ochrona konsumenta”, „Art. 5.”). |
| `Path` | `IReadOnlyList<string>` | `HeadingText` przodków + własny — ścieżka kontekstu dla chunka. |
| `Pages` | `PageRange` | Strony źródłowe od nagłówka do końca treści (z podsekcjami). |
| `Blocks` | `IReadOnlyList<ContentBlock>` | Treść własna (przed podsekcjami). |
| `Footnotes` | `IReadOnlyList<Footnote>` | Definicje przypisów przypisane do tej sekcji (FR-026). |
| `Children` | `IReadOnlyList<Section>` | Podsekcje; `Level` dziecka > `Level` rodzica. |

Reguły: `Article`/`Paragraph` są zawsze odrębnymi sekcjami (Clarifications Q1) i liśćmi względem
jednostek strukturalnych. Poziom `Article`/`Paragraph` = najgłębszy poziom jednostki strukturalnej w
dokumencie + 1, a gdy brak jednostek strukturalnych — 2.

### ContentBlock (abstrakcyjny; dyskryminator `Kind`)

Wspólne: `Pages: PageRange`.

| Typ | Pola | Uwagi |
|-----|------|-------|
| `ParagraphBlock` | `Inlines: IReadOnlyList<Inline>` | Akapit; może zawierać `PageBreak` i `FootnoteRef`. |
| `ListBlock` | `Items: IReadOnlyList<ListItem>` | Lista jednego poziomu. |
| `TableBlock` | `Header: TableRow?`, `Rows: IReadOnlyList<TableRow>`, `ColumnCount: int`, `IsFallback: bool` | `IsFallback = true` ⇒ rendering „ | ” (FR-064). |
| `SkippedPageBlock` | `PageNumber: int`, `Reason: SkipReason` | `NoTextLayer`, `PageReadError` (FR-071, FR-009a). |

### ListItem

| Pole | Typ | Opis |
|------|-----|------|
| `Label` | `string` | Oznaczenie dosłowne: „1)”, „a)”, „2.”, „–”, „•”. |
| `LabelKind` | `ListLabelKind` | `Bullet`, `Dash`, `ArabicParen`, `ArabicDot`, `LetterParen`, `Roman`, `Outline` (1.2.3). |
| `Inlines` | `IReadOnlyList<Inline>` | Treść pozycji. |
| `Children` | `IReadOnlyList<ContentBlock>` | Zagnieżdżone listy i akapity „części wspólnej” na niższym poziomie. |

### TableRow / TableCell

`TableRow { Cells: IReadOnlyList<TableCell> }`; `TableCell { Inlines: IReadOnlyList<Inline>, ColumnSpan: int }`.
Reguła: `Sum(ColumnSpan) == ColumnCount` w tabelach nie-fallback; scalenia poziome wg FR-066
(treść w pierwszej kolumnie, pozostałe puste — rendering bez colspan).

### Inline (dyskryminator `Kind`)

| Typ | Pola |
|-----|------|
| `TextRun` | `Text: string`, `Style: TextStyle` (`[Flags] None, Bold, Italic`) |
| `FootnoteRef` | `FootnoteNumber: int` |
| `PageBreak` | `PageNumber: int` — znacznik `<!-- page: N -->` w miejscu przejścia strony (FR-002a) |

### Footnote

| Pole | Typ | Opis |
|------|-----|------|
| `Number` | `int` | Globalny, unikalny w dokumencie, kolejność pierwszego odnośnika. |
| `OriginalLabel` | `string` | Oznaczenie z PDF („1)”, „*”). |
| `Inlines` | `IReadOnlyList<Inline>` | Treść. |
| `Page` | `int` | Strona źródłowa definicji. |
| `IsOrphan` | `bool` | Brak odnalezionego odnośnika (ostrzeżenie w raporcie). |

### PageRange

`record struct PageRange(int First, int Last)`; niezmiennik `1 ≤ First ≤ Last ≤ PageCount`.

### ConversionReport

| Pole | Typ | Opis |
|------|-----|------|
| `PageCount` | `int` | |
| `SkippedPages` | `IReadOnlyList<SkippedPage>` | `{ PageNumber, Reason, Message }` |
| `RemovedArtifacts` | `IReadOnlyList<ArtifactSummary>` | `{ Pattern, Kind (RunningHeader/RunningFooter/PageNumber), Occurrences, Pages }` — posortowane po pierwszej stronie, potem `Pattern` (FR-027). |
| `HeadingCounts` | `IReadOnlyDictionary<int,int>` | poziom → liczba (klucze rosnąco). |
| `ListCount`, `TableCount`, `FallbackTableCount`, `FootnoteCount` | `int` | |
| `DroppedTextCount` | `int` | Litery pominięte jako obrócone/niewidoczne (FR-013). |
| `Warnings` | `IReadOnlyList<ConversionWarning>` | `{ Code, PageNumber?, Message }`; kody stabilne (np. `PDF001_NoTextLayer`, `TBL001_AmbiguousGrid`, `FTN001_OrphanFootnote`, `TXT001_UnmappedGlyphs`, `IMG001_ImagesIgnored`). |
| `Elapsed` | `TimeSpan` | Czas konwersji — jedyne pole niedeterministyczne. |

---

## 2. Opcje (`PdfParserOptions`)

Wartości domyślne = wartości ze specyfikacji. Walidacja `IValidateOptions`: wartości procentowe
w (0, 1], liczby dodatnie, `MaxTypographicDepth` 1–6.

| Grupa | Pole (domyślnie) | FR |
|-------|------------------|----|
| `Limits` | `MaxInputBytes` (104 857 600, `null` = brak), `MaxPages` (2000), `MaxDuration` (120 s) | FR-009b |
| (root) | `AllowPartialResult` (false) | FR-009a |
| `Normalization` | `HyphenationExceptions` (lista złożeń, np. „e-mail”, „biało-czerwony”), `DropRotatedText` (true), `DropInvisibleText` (true) | FR-010–013 |
| `Artifacts` | `Enabled` (true), `MarginZoneRatio` (0,08), `MinPageRatio` (0,5), `MinPages` (3), `PositionTolerance` (0,02), `Similarity` (0,85), `SplitOddEven` (true), `RemovePageNumbers` (true) | FR-020–025 |
| `Layout` | `LineOverlapRatio` (0,5), `BaselineToleranceRatio` (0,3), `ParagraphGapFactor` (1,5), `ShortLineRatio` (0,75), `DetectColumns` (true), `GutterMinWidthRatio` (0,02), `GutterMinHeightRatio` (0,6), `ColumnMinLineWidthRatio` (0,3), `DetectSideNotes` (true), `SideNoteMaxWidthRatio` (0,25), `SideNoteMaxSizeRatio` (0,9) | FR-030–034 |
| `Headings` | `Enabled` (true), `SizeRatio` (1,15), `SizeClusterTolerance` (0,5 pt), `MaxLength` (120), `MaxLines` (2), `MaxTypographicDepth` (3), `GapFactor` (1,3), `CenterTolerance` (0,05), `DetectLegalUnits` (true) | FR-040–047, FR-043a |
| `Lists` | `Enabled` (true), `IndentTolerance` (1,5 pt) | FR-050–054 |
| `Tables` | `Enabled` (true), `CellGapFactor` (2,0), `MinRows` (3), `ColumnTolerance` (0,03), `RowMergeGapFactor` (1,2), `UseRulingLines` (true), `MergeAcrossPages` (true) | FR-060–066 |
| `Rendering` | `PageMarkers` (true), `FootnotesPlacement` (`EndOfSection`), `EmphasisInline` (true) | FR-002a, FR-026, FR-046 |
| `Footnotes` | `Enabled` (true), `MaxSizeRatio` (0,9) | FR-026 |

---

## 3. Wewnętrzny model układu (robocze, `PipelineContext`)

| Typ | Pola kluczowe | Tworzony przez etap |
|-----|---------------|---------------------|
| `PipelineContext` | `Options`, `Source`, `Pages: List<LayoutPage>`, `Report: ReportBuilder`, `CancellationToken`, `BodyStyle?`, `Document?` | fasada |
| `LayoutPage` | `Number`, `Width`, `Height`, `Lines: List<LayoutLine>`, `Rulings: List<Segment>`, `HasImages`, `Skipped?` | Extraction |
| `LayoutGlyph` | `Text`, `Box`, `Baseline`, `PointSize`, `IsBold`, `IsItalic` | Extraction |
| `LayoutWord` | `Glyphs`, `Box`, `Text`, `Style` | LineAssembly |
| `LayoutLine` | `Words`, `Box`, `Baseline`, `Segments: List<LineSegment>` (podział po dużych odstępach), `Zone` (Header/Body/Footer), `Role` (Unknown/Artifact/Footnote/Table/ListItem/Heading/Body), `Annotations` | LineAssembly → kolejne etapy |
| `LayoutBlock` | `Kind`, `Lines`, `Pages`, dane specyficzne (pasy kolumn, oznaczenie listy, poziom nagłówka) | BlockAssembly / Table / List / Heading |

### Kontrakty etapów US2 (nagłówki i przypisy, ustalone przy T055–T058)

| Element | Kto ustawia | Kto czyta | Znaczenie |
|---------|-------------|-----------|-----------|
| `LegalUnitPatterns.TryMatch(line, out LegalUnitMatch)` | — | HeadingDetection | `LegalUnitMatch(Kind, Designation, Number, Rest)`; `Designation` bez kropki końcowej („Art. 12a”, „§ 5¹”), `Rest` = treść po oznaczeniu |
| `HeadingInfo(Level, Kind, Designation, Number, Title, Text)` | HeadingDetection | BlockAssembly, DocumentBuild | `Text` = pełny `HeadingText` („Rozdział 3. Ochrona konsumenta”, „Art. 5.”) |
| `LayoutLine.Heading` + `Role = Heading` | HeadingDetection | BlockAssembly | pierwsza linia nagłówka ma `Heading`; dołączone linie tytułu mają `Role = Heading`, `Heading = null` |
| podział „Art. 5. Treść…” | HeadingDetection | BlockAssembly | linia zastąpiona w `page.Lines` dwiema: nagłówkową („Art. 5.”) i treściową (`Role = Unknown`, ta sama linia bazowa) |
| `LayoutBlock(Kind = Heading).Heading` | BlockAssembly | DocumentBuild | blok nagłówka w kolejności treści |
| `PipelineContext.Footnotes` (`FootnoteDraft`: `Id`, `Label`, `Page`, `Inlines`, `IsOrphan`) | FootnoteDetection | DocumentBuild | definicje w kolejności wykrycia; kontynuacje z kolejnych stron dołączone do poprzedniej definicji |
| `LayoutWord.FootnoteId` | FootnoteDetection | BlockAssembly | wyraz-odnośnik (indeks górny odcięty od wyrazu bazowego) |
| `FootnoteRef(n)` przed DocumentBuild | BlockAssembly | DocumentBuild | `n` = `FootnoteDraft.Id`; DocumentBuild zamienia na numer globalny wg pierwszego odnośnika |

### Przejścia stanu linii (`LayoutLine.Role`)

```text
Unknown ──(ArtifactRemoval)──► Artifact (usuwana, liczona w raporcie)
Unknown ──(FootnoteDetection)─► Footnote
Unknown ──(TableDetection)────► Table
Unknown ──(ListDetection)─────► ListItem | ListContinuation
Unknown ──(HeadingDetection)──► Heading
Unknown ──(koniec potoku)─────► Body
```

Reguła: rola raz nadana inna niż `Unknown` nie jest zmieniana przez późniejsze etapy (wyjątek:
`HeadingDetection` może zdegradować `Heading` → `Body` przy walidacji spójności hierarchii).

## 4. Kolejność etapów (Order)

| Order | Etap | FR |
|-------|------|----|
| 100 | `PageExtractionStage` (litery, linie siatki, obrazy; pomijanie obróconych/niewidocznych; błędy stron) | FR-009a, FR-013, FR-071 |
| 200 | `TextNormalizationStage` (ligatury, spacje, NFC, indeksy górne) | FR-010 |
| 300 | `LineAssemblyStage` (słowa, linie, segmenty, strefy) | FR-011, FR-030 |
| 400 | `ArtifactRemovalStage` | FR-020–025, FR-027 |
| 500 | `FootnoteDetectionStage` | FR-026 |
| 600 | `TableDetectionStage` | FR-060–066 |
| 700 | `ReadingOrderStage` (kolumny) | FR-031 |
| 800 | `ListDetectionStage` | FR-050–054 |
| 900 | `HeadingDetectionStage` | FR-040–047 |
| 1000 | `BlockAssemblyStage` (akapity, dzielenie wyrazów, łączenie przez strony) | FR-012, FR-032, FR-033 |
| 1100 | `DocumentBuildStage` (drzewo sekcji, przypisy → sekcje, `Path`, `PageRange`) | FR-002, FR-026, FR-043 |

Rendering Markdown nie jest etapem — to osobna usługa `IMarkdownRenderer` działająca na
`LegalDocument` (można renderować ponownie z innymi opcjami bez ponownej konwersji).
