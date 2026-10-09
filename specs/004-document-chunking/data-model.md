# Data Model: Podział dokumentów na fragmenty (spec 004)

Modele publiczne biblioteki `LegalAgent.Chunking` (przestrzeń nazw `LegalAgent.Chunking`). Wszystkie są
niemutowalnymi rekordami; kolekcje to `IReadOnlyList<T>`. Postać JSON: `contracts/chunks-json.md`.

## DocumentMetadata (wejście od wywołującego, FR-210)

| Pole | Typ | Wymagane | Walidacja / uwagi |
|------|-----|----------|-------------------|
| `DocumentId` | string | tak | `^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$`; unikalny dla wersji (np. `REG-05-w1`) |
| `Designation` | string? | nie | oznaczenie wspólne dla wersji (np. `BP/REG/05`); brak → `DocumentId` |
| `Type` | string? | nie | po angielsku, np. `regulation`, `tariff`, `procedure`, `act` (korpus: tłumaczenie z manifestu) |
| `Title` | string? | nie | ma pierwszeństwo przed tytułem wykrytym przez parser |
| `Version` | int? | nie | > 0 |
| `ValidFrom` | DateOnly? | nie | |
| `ValidTo` | DateOnly? | nie | ≥ `ValidFrom`, gdy oba podane |
| `Status` | string? | nie | po angielsku, np. `in-force`, `outdated`; biblioteka nie interpretuje |
| `PreviousVersion` | string? | nie | `DocumentId` poprzedniej wersji |

Błąd walidacji → `ArgumentException` (FR-206) przed jakąkolwiek pracą.

## ChunkedDocument (wynik)

Implementacja: `ChunkedDocument(Header, Chunks)`, gdzie `Header` to `ChunkedDocumentHeader` z polami
`Metadata`, `Title`, `DetectedTitle`, `SeriesKey`, `Source` (tabela poniżej); modele w `LegalAgent.Chunking.Model`.

| Pole | Typ | Opis |
|------|-----|------|
| `Metadata` | DocumentMetadata | jak na wejściu |
| `Title` | string? | `Metadata.Title` ?? `LegalDocument.Title` |
| `DetectedTitle` | string? | tytuł wykryty przez parser (`LegalDocument.Title`) |
| `SeriesKey` | string | oznaczenie dokumentu wspólne dla wersji (spec FR-210): `Metadata.Designation` ?? `Metadata.DocumentId`; w JSON `document.designation` |
| `Source` | ChunkSource | dane źródła |
| `Chunks` | IReadOnlyList<Chunk> | w kolejności dokumentu (FR-225) |

`ChunkedDocumentHeader` = te same pola bez `Chunks` (część `document` rekordu JSON; wynik odczytu
`ChunkJson.ReadLines` w `ChunkRecord`).

### ChunkSource

| Pole | Typ | Opis |
|------|-----|------|
| `PageCount` | int | `SourceInfo.PageCount` |
| `Sha256` | string | `SourceInfo.Sha256` (hex) |
| `IsComplete` | bool | `PdfConversionResult.IsComplete` |
| `SkippedPages` | IReadOnlyList<int> | strony pominięte przez parser (z raportu) |

## Chunk (fragment, FR-240)

| Pole | Typ | Opis / reguła |
|------|-----|---------------|
| `ChunkId` | string | R7: `<DocumentId>_<hex16(SHA-256(UnitKey))>_<Part>` |
| `UnitKey` | string | R6: `<SeriesKey> \| <najkrótsza unikalna ścieżka>` |
| `Part` | int | 1..`PartCount` |
| `PartCount` | int | ≥ 1 |
| `UnitKind` | ChunkUnitKind | `Preamble` albo wartość `SectionKind` (`Article`, `Paragraph`, `Chapter`, `TableDocumentSection`, `Typographic`, …) |
| `Citation` | string? | `Section.Designation` albo `HeadingText` (sekcja bez oznaczenia); `null` dla wstępu (FR-241) |
| `ListLabels` | IReadOnlyList<string> | etykiety pozycji, od której zaczyna się część, i jej przodków, od zewnętrznej (np. `["3.", "2)"]`); puste, gdy część nie zaczyna się od pozycji listy, oraz dla pierwszej części jednostki (zaczyna się od początku jednostki, więc cytatem jest samo oznaczenie) |
| `SectionPath` | IReadOnlyList<string> | `Section.Path` (teksty nagłówków od korzenia do jednostki); pusta dla wstępu |
| `Pages` | PageSpan | `First`, `Last` (R4), w granicach stron jednostki |
| `Length` | int | `Content.Length` (znaki UTF-16) |
| `ExceedsLimit` | bool | `Length > MaxChunkLength` (tylko przy elemencie niepodzielnym, FR-224) |
| `Content` | string | Markdown części (R1): nagłówek jednostki, treść, przypisy; LF; bez znaczników stron; bez końcowego LF |

Niezmienniki (testowane):

1. `ChunkId` unikalne w dokumencie; `(UnitKey, Part)` unikalne w dokumencie.
2. Fragmenty jednej jednostki są kolejne i mają `Part = 1..PartCount`, ten sam `UnitKey`, `Citation`,
   `SectionPath`, `UnitKind`.
3. `Length ≤ MaxChunkLength` albo `ExceedsLimit = true` i część zawiera dokładnie jeden atom (R3).
4. `Pages.First ≤ Pages.Last`, oba w `Section.Pages` jednostki.
5. Pokrycie słów (FR-234).

## ChunkingOptions (FR-221)

| Pole | Typ | Domyślnie | Walidacja |
|------|-----|-----------|-----------|
| `MaxChunkLength` | int | 2000 | ≥ 200 |
| `Rendering` | RenderingOptions? | `null` → opcje renderowania parsera z `PageMarkers = false` | `PageMarkers` zawsze wymuszane na `false` |

## ChunkingRequest (per wywołanie)

| Pole | Typ | Opis |
|------|-----|------|
| `ConfigureOptions` | Action<ChunkingOptions>? | zmienia kopię skonfigurowanych opcji tylko dla tego wywołania |
| `ParserRequest` | PdfConversionRequest? | tylko dla wejścia PDF: przekazywane do `IPdfMarkdownConverter` |

## Rozszerzenie modelu parsera (R4)

`LegalAgent.PdfParser.Model.TableRow` — nowa właściwość `int? Page { get; init; }`: strona, na której
zaczyna się wiersz; `null`, gdy nieznana. Renderer jej nie czyta.

## Manifest korpusu (rozszerzenie kontraktu 003)

`ManifestDocument` — nowe pole `chunks` (string, ścieżka względna do `corpus/`, np.
`regulaminy/REG-05.chunks.jsonl`), zapisywane po `markdown`. `schemaVersion` manifestu bez zmian (1).
