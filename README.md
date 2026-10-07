# LegalAgent.PdfParser

Biblioteka .NET 9 (z cienką aplikacją CLI) konwertująca pliki PDF z polskimi aktami prawnymi
(ustawy, rozporządzenia, obwieszczenia z ISAP / Dziennika Ustaw) oraz regulaminami bankowymi na
**ustrukturyzowany model dokumentu** i **Markdown** nadający się do dalszego przetwarzania (np. przez agenta AI).
Wynik jest deterministyczny: te same bajty i opcje dają ten sam model, Markdown i raport (poza `Elapsed`).

## Co robi

Potok etapów (`IPipelineStage`) stosuje heurystyki:

- **artefakty** — usuwa powtarzalne nagłówki i stopki (np. „Dziennik Ustaw – N – Poz. …”) oraz numery stron;
- **normalizacja tekstu** — ligatury, spacje, NFC, indeksy górne, łączenie wyrazów dzielonych myślnikiem na końcu linii, pomijanie tekstu obróconego i niewidocznego;
- **nagłówki i jednostki prawne** — Dział, Rozdział, Art., § oraz nagłówki typograficzne; drzewo sekcji ze ścieżkami (np. `["Rozdział 1. …", "Art. 1."]`);
- **listy** — wyliczenia `1)`, `a)`, punktory, tiret, zagnieżdżenie;
- **tabele** — z linii siatki lub z wyrównania kolumn, łączenie tabel ciągłych na kolejnych stronach;
- **przypisy** — odnośniki `[^n]` i definicje umieszczane po treści artykułu/sekcji;
- **adnotacje boczne** — wąska kolumna przy krawędzi strony jest wydzielana z tekstu głównego;
- **kolejność czytania** — wykrywanie układu wielokolumnowego.

## Wymagania

- .NET SDK wg `global.json` (10.0.x, `rollForward: latestFeature`) oraz **runtime .NET 9.0.x GA** (`dotnet --list-runtimes` musi pokazać `Microsoft.NETCore.App 9.0.*` bez sufiksu `preview`).
- Linux: biblioteka ICU (Alpine: `apk add icu-libs`). **Nie** ustawiaj `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.

```bash
dotnet build LegalAgent.slnx -c Release
```

## Użycie biblioteki

```csharp
var services = new ServiceCollection().AddLegalAgentPdfParser();
var converter = services.BuildServiceProvider().GetRequiredService<IPdfMarkdownConverter>();
var result = await converter.ConvertAsync(File.OpenRead("dokument.pdf"));
```

Bez kontenera DI: `PdfMarkdownConverter.CreateDefault(opts => { ... })`.

Opcje per wywołanie (stosowane na kopii opcji, nie wpływają na kolejne wywołania):

```csharp
var result = await converter.ConvertAsync(stream, new PdfConversionRequest
{
    SourceId = "ustawa-2024",
    ConfigureOptions = o => { o.Tables.Enabled = false; o.Rendering.PageMarkers = false; },
    Progress = new Progress<ConversionProgress>(p => Console.WriteLine($"{p.PageNumber}/{p.PageCount} {p.Stage}")),
});
```

Opcje globalne: `AddLegalAgentPdfParser(o => ...)` lub `services.Configure<PdfParserOptions>(configuration.GetSection("PdfParser"))`.
Strumień nie jest zamykany przez bibliotekę; konwerter jest bezpieczny do wywołań równoległych.

`PdfConversionResult`:

| Właściwość | Znaczenie |
|------------|-----------|
| `Markdown` | wynik w Markdown (LF, UTF-8) |
| `Document` | model `LegalDocument` (sekcje, bloki, przypisy) |
| `Report` | `ConversionReport`: liczba stron, pominięte strony, usunięte artefakty, liczniki nagłówków/list/tabel/przypisów, ostrzeżenia, `Elapsed` |
| `IsComplete` | `false` wtedy i tylko wtedy, gdy raport zawiera pominięte strony |

## Opcje (`PdfParserOptions`)

| Grupa (właściwość) | Pole | Domyślnie | Znaczenie |
|--------------------|------|-----------|-----------|
| (root) | `AllowPartialResult` | `false` | zwróć wynik częściowy, gdy część stron nie daje się odczytać |
| `Limits` | `MaxInputBytes` | `104857600` | maks. rozmiar wejścia w bajtach; `null` = bez limitu |
| `Limits` | `MaxPages` | `2000` | maks. liczba stron; `null` = bez limitu |
| `Limits` | `MaxDuration` | 120 s | maks. czas konwersji; `null` = bez limitu |
| `Normalization` | `HyphenationExceptions` | `["e-mail", "biało-czerwony"]` | złożenia, których myślnik zostaje przy łączeniu dzielonych wyrazów |
| `Normalization` | `DropRotatedText` | `true` | pomijaj tekst obrócony |
| `Normalization` | `DropInvisibleText` | `true` | pomijaj tekst o niewidocznym trybie renderowania |
| `Artifacts` | `Enabled` | `true` | usuwanie nagłówków/stopek/numerów stron |
| `Artifacts` | `MarginZoneRatio` | `0.08` | ułamek wysokości strony u góry i u dołu traktowany jako strefa marginesu |
| `Artifacts` | `MinPageRatio` | `0.5` | minimalny ułamek stron, na których linia musi się powtarzać |
| `Artifacts` | `MinPages` | `3` | minimalna liczba stron dla usuwania na podstawie powtórzeń |
| `Artifacts` | `PositionTolerance` | `0.02` | dopuszczalne odchylenie pionowe (ułamek wysokości strony) |
| `Artifacts` | `Similarity` | `0.85` | minimalne podobieństwo odcisku (0–1), by uznać linie za ten sam artefakt |
| `Artifacts` | `SplitOddEven` | `true` | osobna analiza stron nieparzystych i parzystych |
| `Artifacts` | `RemovePageNumbers` | `true` | usuwanie numerów stron |
| `Layout` | `LineOverlapRatio` | `0.5` | minimalne pionowe pokrycie glifów należących do jednej linii |
| `Layout` | `BaselineToleranceRatio` | `0.3` | maks. różnica linii bazowych (ułamek mniejszego rozmiaru czcionki) |
| `Layout` | `ParagraphGapFactor` | `1.5` | odstęp (w interliniach), od którego zaczyna się nowy akapit |
| `Layout` | `ShortLineRatio` | `0.75` | linia kończąca zdanie, krótsza niż ten ułamek szerokości kolumny, kończy akapit |
| `Layout` | `DetectColumns` | `true` | wykrywanie układu wielokolumnowego |
| `Layout` | `GutterMinWidthRatio` | `0.02` | minimalna szerokość rynny (ułamek szerokości strony) |
| `Layout` | `GutterMinHeightRatio` | `0.6` | minimalna wysokość rynny (ułamek wysokości strony) |
| `Layout` | `ColumnMinLineWidthRatio` | `0.25` | minimalna szerokość linii liczonej do kolumn (ułamek szerokości strony) |
| `Layout` | `DetectSideNotes` | `true` | wydzielanie wąskiej kolumny adnotacji bocznych |
| `Layout` | `SideNoteMaxWidthRatio` | `0.25` | maks. szerokość kolumny adnotacji (ułamek szerokości strony) |
| `Layout` | `SideNoteMaxSizeRatio` | `0.9` | maks. rozmiar czcionki adnotacji względem tekstu głównego |
| `Headings` | `Enabled` | `true` | wykrywanie nagłówków |
| `Headings` | `SizeRatio` | `1.15` | minimalny stosunek rozmiaru czcionki do tekstu głównego dla nagłówka typograficznego |
| `Headings` | `SizeClusterTolerance` | `0.5` | tolerancja grupowania rozmiarów czcionek (pkt) |
| `Headings` | `MaxLength` | `120` | maks. długość nagłówka (znaki) |
| `Headings` | `MaxLines` | `2` | maks. liczba linii nagłówka |
| `Headings` | `MaxTypographicDepth` | `3` | maks. liczba poziomów nagłówków typograficznych (1–6) |
| `Headings` | `GapFactor` | `1.3` | wymagany odstęp wokół nagłówka (w interliniach) |
| `Headings` | `CenterTolerance` | `0.05` | tolerancja wyśrodkowania (ułamek szerokości strony) |
| `Headings` | `DetectLegalUnits` | `true` | wykrywanie Dział/Rozdział/Art./§ |
| `Lists` | `Enabled` | `true` | wykrywanie list |
| `Lists` | `IndentTolerance` | `1.5` | różnica wcięcia (pkt) traktowana jako ten sam poziom |
| `Tables` | `Enabled` | `true` | wykrywanie tabel |
| `Tables` | `CellGapFactor` | `2.0` | odstęp (w średnich szerokościach spacji) rozdzielający komórki |
| `Tables` | `MinRows` | `3` | minimalna liczba wierszy tabeli |
| `Tables` | `ColumnTolerance` | `0.03` | tolerancja wyrównania kolumn (ułamek szerokości strony) |
| `Tables` | `RowMergeGapFactor` | `1.2` | odstęp (w interliniach), do którego linie łączą się w jeden wielolinijkowy wiersz |
| `Tables` | `UseRulingLines` | `true` | użycie linii siatki do wykrycia tabeli |
| `Tables` | `MergeAcrossPages` | `true` | łączenie tabel ciągłych między stronami |
| `Rendering` | `PageMarkers` | `true` | znaczniki stron `<!-- page: N -->` |
| `Rendering` | `FootnotesPlacement` | `EndOfSection` | miejsce definicji przypisów (jedyna wartość: po treści sekcji) |
| `Rendering` | `EmphasisInline` | `true` | pogrubienie/kursywa jako `**` / `*` |
| `Footnotes` | `Enabled` | `true` | wykrywanie przypisów |
| `Footnotes` | `MaxSizeRatio` | `0.9` | maks. rozmiar czcionki linii przypisu względem tekstu głównego |

Opcje są walidowane (`OptionsValidationException` przy niepoprawnych wartościach).

## Własne etapy potoku

Etap implementuje `IPipelineStage` (`int Order`, `void Execute(PipelineContext context)`); etapy są wykonywane
rosnąco wg `Order`. Wbudowane wartości są w stałych `StageOrder` (przestrzeń nazw `LegalAgent.PdfParser.Pipeline`):
`PageExtraction` 100, `TextNormalization` 200, `LineAssembly` 300, `ArtifactRemoval` 400, `FootnoteDetection` 500,
`TableDetection` 600, `ReadingOrder` 700, `ListDetection` 800, `HeadingDetection` 900, `BlockAssembly` 1000,
`DocumentBuild` 1100. Etap musi być bezstanowy (stan wyłącznie w `PipelineContext`).

```csharp
services.AddLegalAgentPdfParser()
        .AddPdfParserStage<MojEtap>()                              // własny etap (Singleton)
        .ReplacePdfParserStage<TableDetectionStage, MojeTabele>()  // zastąpienie wbudowanego
        .RemovePdfParserStage<ListDetectionStage>();               // usunięcie etapu
```

## Obsługa błędów

Wszystkie wyjątki biblioteki dziedziczą po `PdfParserException` i mają komunikat po polsku:

| Wyjątek | Kiedy |
|---------|-------|
| `InvalidPdfException` (`Reason`: `Empty`, `NotPdf`, `Corrupted`) | pusty strumień, brak nagłówka `%PDF`, uszkodzona struktura |
| `PdfEncryptedException` | wymagane hasło otwarcia |
| `PdfPageReadException` (`PageNumber`) | błąd odczytu strony przy `AllowPartialResult = false` lub pominięcie wszystkich stron |
| `PdfLimitExceededException` (`Limit`, `Configured`, `Measured`) | przekroczony limit rozmiaru, liczby stron lub czasu |
| `PdfNoTextException` | brak tekstu w całym dokumencie |

Dodatkowo: `ArgumentNullException` (`pdf == null`), `ArgumentException` (strumień nieczytelny),
`OptionsValidationException`, `OperationCanceledException` (anulowanie przez `CancellationToken`).

**Tryb częściowy:** przy `AllowPartialResult = true` nieczytelne strony są pomijane — wynik wraca z `IsComplete == false`,
a w Markdown pojawia się znacznik `<!-- page N skipped: read-error -->`. Strony bez warstwy tekstowej są pomijane ze
znacznikiem `<!-- page N skipped: no-text-layer -->`.

## CLI

```text
legalagent-pdf convert <wejście.pdf> [-o|--output <wyjście.md>] [--report <raport.json>]
                       [--no-page-markers] [--allow-partial]
legalagent-pdf --help | --version
```

```bash
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- convert ustawa.pdf -o ustawa.md --report ustawa.report.json
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- convert ustawa.pdf --no-page-markers > ustawa.md
PDFPARSER__Limits__MaxPages=5000 dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- convert duza.pdf --allow-partial -o duza.md
```

Bez `-o` Markdown trafia na stdout. `--report` zapisuje `ConversionReport` jako JSON. Opcje heurystyk można ustawić zmiennymi
środowiskowymi `PDFPARSER__<Grupa>__<Pole>`. Komunikaty błędów (po polsku) trafiają na stderr.

| Kod | Znaczenie |
|-----|-----------|
| 0 | sukces, wynik kompletny |
| 1 | nieoczekiwany błąd |
| 2 | błędne argumenty / plik wejściowy nie istnieje |
| 3 | `InvalidPdfException` / `PdfEncryptedException` / `PdfNoTextException` |
| 4 | `PdfPageReadException` |
| 5 | `PdfLimitExceededException` |
| 6 | sukces, ale wynik niepełny (`IsComplete == false`); plik zapisany |
| 130 | przerwane (Ctrl+C) |

## Format wyjściowy Markdown

Pełny kontrakt: `specs/001-legal-pdf-parser/contracts/markdown-output.md`. W skrócie: CommonMark + GFM, UTF-8, LF.

- tytuł jako `#`, sekcje jako `##`…; jednostki prawne (Dział, Rozdział, Art., §) jako nagłówki o rosnącym poziomie;
- listy: `- 1\) treść`, zagnieżdżenie wcięciem 2 spacji; punktory i tiret jako `- treść` / `- – treść`;
- tabele GFM (`| a | b |` + separator `| --- |`); gdy tabeli nie da się wiarygodnie odtworzyć, wiersze są zapisywane jako akapity z komórkami rozdzielonymi ` \| `;
- przypisy: odnośnik `[^n]`, definicja `[^n]: treść` po treści artykułu/sekcji;
- znaczniki stron `<!-- page: N -->` (wyłączane opcją `Rendering.PageMarkers` / `--no-page-markers`); strony pominięte: `<!-- page N skipped: … -->`;
- pogrubienie `**t**`, kursywa `*t*`; znaki specjalne Markdown są escapowane.

## Testy

```bash
dotnet test LegalAgent.slnx -c Release                                  # wszystkie
dotnet test LegalAgent.slnx -c Release --filter "Category!=Performance" # bez testów wydajności (jak w CI)
dotnet test LegalAgent.slnx -c Release --filter "Category=Performance"  # tylko testy wydajności (SC-007)
```

- **Golden files** (`*.expected.md`): wynik jest porównywany ze wzorcem; różnice zapisywane są jako `*.actual.md`.
  Ustawienie `UPDATE_GOLDEN=1` nadpisuje wzorce (`UPDATE_GOLDEN=1 dotnet test LegalAgent.slnx -c Release`; w PowerShell: `$env:UPDATE_GOLDEN = "1"`). Zmiany wzorców przejrzyj w diffie.
- **Korpus** publiczny: `tests/LegalAgent.PdfParser.Tests/Corpus/acts` (źródła: `Corpus/acts/SOURCES.md`) oraz pliki błędów w `Corpus/errors`.
- **Korpus prywatny:** zmienna `LEGALAGENT_PRIVATE_CORPUS` wskazuje katalog z własnymi PDF (np. regulaminami, których nie można publikować); odpowiadające jej testy są pomijane, gdy zmienna nie jest ustawiona.
- CI (`.github/workflows/ci.yml`, ubuntu-latest) buduje i uruchamia testy z filtrem `Category!=Performance`, a następnie osobno `Category=Performance`.

## Ograniczenia

- Strony skanowane (bez warstwy tekstowej) **nie są** przetwarzane OCR-em — są pomijane (znacznik `no-text-layer`, `IsComplete == false`).
- Złożone, wielopoziomowe nagłówki tabel nie są odtwarzane — tabela spada do trybu zapasowego (wiersze jako akapity z komórkami rozdzielonymi ` | `).
- Podwójne brzmienie całych artykułów w tekstach ISAP (`[Art. …]` / `<Art. …>`) nie jest strukturyzowane.
