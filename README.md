# Konwerter repozytorium regulaminów

Aplikacja konsolowa **`mBank.FaqGenerator`** jednym poleceniem:

1. **pobiera 5 wskazanych regulaminów PDF** ze strony mBanku do `./downloads` (z `manifest.json` wiążącym pliki z
   adresami),
2. **konwertuje je do Markdown** (`<nazwa>.md` obok każdego PDF-u) własnym parserem dokumentów prawnych,
3. **generuje plik FAQ** `faq/FAQ_mBank.md` (format OKF): 10 najważniejszych pytań i odpowiedzi, każda ze źródłem
   (dokument i paragraf), przygotowanych przez model Azure OpenAI wyłącznie na podstawie treści regulaminów.

Rozwiązanie: .NET 9, biblioteki z całą logiką i cienka aplikacja; testy działają offline (atrapy HTTP, modelu i
klawiatury). Specyfikacje i decyzje projektowe: `specs/`, zasady projektu: `.specify/memory/constitution.md`.

## Szybki start

Wymagane: .NET SDK z `global.json` i runtime .NET 9 (szczegóły: [Wymagania](#wymagania)), subskrypcja Azure; skrypt
zasobu wymaga Basha (na Windows: Git Bash).

```bash
dotnet build LegalAgent.slnx -c Release

# 1. Zasób Azure OpenAI (jednorazowo): skrypt albo portal — patrz „Generowanie FAQ” niżej
scripts/azure/create-openai.sh

# 2. Endpoint (nie jest sekretem) w src/mBank.FaqGenerator/appsettings.Local.json:
#    { "AzureOpenAI": { "Endpoint": "https://<zasób>.openai.azure.com/", "Deployment": "<wdrożenie>", "Model": "<model>" } }

# 3. Uruchomienie: 5 adresów PDF z mbank.pl; o klucz API aplikacja zapyta po konwersji (wpisywany jako gwiazdki)
dotnet run --project src/mBank.FaqGenerator -c Release -- \
  --url <adres1> --url <adres2> --url <adres3> --url <adres4> --url <adres5>
```

Wynik: `downloads/*.pdf`, `downloads/*.md`, `downloads/manifest.json` i `faq/FAQ_mBank.md`; kod wyjścia 0. Adresy
można też wpisać do `Download:Urls` w `appsettings.json` (wtedy wystarczy samo `dotnet run`) albo podać w odpowiedzi
na pytania aplikacji. Szczegóły: [Pobieranie regulaminów](#pobieranie-regulaminów-mbankfaqgenerator) i
[Generowanie FAQ](#generowanie-faq-mbankfaqgenerator-legalagentfaq).

## Struktura rozwiązania

Jedna solucja `LegalAgent.slnx`:

| Projekt | Rola |
|---------|------|
| `src/mBank.FaqGenerator` | **aplikacja zadania**: argumenty, konfiguracja, klucz API, konsola, kody wyjścia ([przebieg i konfiguracja](src/mBank.FaqGenerator/README.md)) |
| `src/LegalAgent.Downloads` | biblioteka pobierania listy PDF-ów (hosty, przekierowania, limity, zapis atomowy, manifest) |
| `src/LegalAgent.PdfParser` | biblioteka konwertująca PDF na model dokumentu i Markdown ([zasada działania](src/LegalAgent.PdfParser/README.md)) |
| `src/LegalAgent.Faq` | biblioteka konwersji zestawu PDF-ów i generowania FAQ (dwa kroki, weryfikacja, renderer OKF) ([zasada działania i weryfikacji](src/LegalAgent.Faq/README.md)) |
| `src/LegalAgent.PdfParser.Cli` | CLI parsera (`legalagent-pdf`: `convert`, `chunk`) |
| `src/LegalAgent.Chunking` | biblioteka podziału dokumentów na fragmenty dla RAG ([zasada działania](src/LegalAgent.Chunking/README.md)) |
| `src/LegalAgent.Corpus`, `src/LegalAgent.Corpus.Cli` | generator syntetycznego korpusu dokumentów bankowych ([zasada działania](src/LegalAgent.Corpus/README.md)) |
| `scripts/azure/create-openai.sh` | utworzenie zasobu i wdrożenia Azure OpenAI |
| `tests/*` | osobny projekt testów dla każdej biblioteki i aplikacji |

Aplikacja i każda biblioteka mają własne `README.md` z opisem zasady działania.

Biblioteki są ogólne: nie znają mBanku ani Azure (adresy, hosty, tytuł FAQ i konektor modelu należą do aplikacji).

## Parser PDF (`LegalAgent.PdfParser`)

Biblioteka konwertuje pliki PDF z polskimi aktami prawnymi (ustawy, rozporządzenia, obwieszczenia z ISAP / Dziennika
Ustaw) oraz regulaminami bankowymi na **ustrukturyzowany model dokumentu** i **Markdown** nadający się do dalszego
przetwarzania (np. przez agenta AI). Wynik jest deterministyczny: te same bajty i opcje dają ten sam model, Markdown i
raport (poza `Elapsed`). Biblioteka **`LegalAgent.Chunking`** dzieli wynik parsera na **fragmenty dla aplikacji RAG**
— patrz [Podział na fragmenty](#podział-na-fragmenty-legalagentchunking).

## Co robi

Potok etapów (`IPipelineStage`) stosuje heurystyki:

- **artefakty** — usuwa powtarzalne nagłówki i stopki (np. „Dziennik Ustaw – N – Poz. …”) oraz numery stron;
- **normalizacja tekstu** — ligatury, spacje, NFC, indeksy górne, łączenie wyrazów dzielonych myślnikiem na końcu linii, pomijanie tekstu obróconego i niewidocznego;
- **nagłówki i jednostki prawne** — Dział, Rozdział, Art., § oraz nagłówki typograficzne; drzewo sekcji ze ścieżkami (np. `["Rozdział 1. …", "Art. 1."]`);
- **listy** — wyliczenia `1)`, `a)`, punktory, tiret, zagnieżdżenie;
- **tabele** — z linii siatki lub z wyrównania kolumn, łączenie tabel ciągłych na kolejnych stronach;
- **przypisy** — odnośniki `[^n]` i definicje umieszczane po treści artykułu/sekcji;
- **adnotacje boczne** — wąska kolumna przy krawędzi strony jest wydzielana z tekstu głównego;
- **kolejność czytania** — wykrywanie układu wielokolumnowego;
- **tabele-dokumenty** — dokument będący jedną wielostronicową tabelą dwukolumnową jest odtwarzany jako sekwencja sekcji (patrz niżej).

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
| `Report` | `ConversionReport`: liczba stron, pominięte strony, usunięte artefakty, liczniki nagłówków/list/tabel/przypisów, `TableDocuments` (rozpoznane tabele-dokumenty: strony, liczba sekcji, pominięty wiersz nazw kolumn), ostrzeżenia, `Elapsed` |
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
| `Headings` | `DetectImageCaptions` | `true` | krótka linia na obrazie lub tuż pod nim (np. adres pod logo) jest podpisem, nie nagłówkiem (FR-088) |
| `Headings` | `ValidityLineAsParagraph` | `true` | linia „Obowiązuje od …” na pierwszej stronie jest akapitem, nie nagłówkiem (FR-093) |
| `Lists` | `Enabled` | `true` | wykrywanie list |
| `Lists` | `IndentTolerance` | `1.5` | różnica wcięcia (pkt) traktowana jako ten sam poziom |
| `Tables` | `Enabled` | `true` | wykrywanie tabel |
| `Tables` | `CellGapFactor` | `2.0` | odstęp (w średnich szerokościach spacji) rozdzielający komórki |
| `Tables` | `MinRows` | `3` | minimalna liczba wierszy tabeli |
| `Tables` | `ColumnTolerance` | `0.03` | tolerancja wyrównania kolumn (ułamek szerokości strony) |
| `Tables` | `RowMergeGapFactor` | `1.2` | odstęp (w interliniach), do którego linie łączą się w jeden wielolinijkowy wiersz |
| `Tables` | `UseRulingLines` | `true` | użycie linii siatki do wykrycia tabeli |
| `Tables` | `MergeAcrossPages` | `true` | łączenie tabel ciągłych między stronami |
| `Tables` | `DetectStepSequences` | `true` | schematy kroków (szare pola z nazwami kroków, wyjaśnienie obok) jako pogrubiona nazwa kroku + treść (FR-067) |
| `Tables` | `DetectTableDocuments` | `true` | dokument będący wielostronicową tabelą dwukolumnową → sekcje (FR-080); `false` = zachowanie jak bez tej funkcji |
| `Tables` | `TableDocumentMaxLeftColumnRatio` | `0.35` | maks. szerokość lewej kolumny (ułamek szerokości tabeli), zakres (0, 1] |
| `Tables` | `TableDocumentMinPages` | `2` | minimalna liczba stron tabeli-dokumentu (≥ 2) |
| `Tables` | `TableDocumentMinPageRatio` | `0.5` | minimalny ułamek stron z tekstem, który zajmuje tabela, zakres (0, 1] |
| `Tables` | `TableDocumentMinMedianWords` | `40` | minimalna mediana liczby słów w prawych komórkach wierszy z nazwą (≥ 1) |
| `Rendering` | `PageMarkers` | `true` | znaczniki stron `<!-- page: N -->` |
| `Rendering` | `FootnotesPlacement` | `EndOfSection` | miejsce definicji przypisów (jedyna wartość: po treści sekcji) |
| `Rendering` | `EmphasisInline` | `true` | pogrubienie/kursywa jako `**` / `*` |
| `Footnotes` | `Enabled` | `true` | wykrywanie przypisów |
| `Footnotes` | `MaxSizeRatio` | `0.9` | maks. rozmiar czcionki linii przypisu względem tekstu głównego |

Opcje są walidowane (`OptionsValidationException` przy niepoprawnych wartościach).

## Tabele-dokumenty

Część regulaminów to jedna wielostronicowa tabela z ramką i dwiema kolumnami: w lewej są nazwy sekcji (np. „Warunki
promocji”), w prawej ich treść (akapity, listy, podpunkty). Biblioteka rozpoznaje taki dokument (etap
`TableDocumentStage`, `StageOrder.TableDocument` = 560; progi: opcje `Tables.TableDocument*`) i zamiast tabeli GFM
zapisuje go jako zwykły dokument:

- nazwa z lewej komórki to nagłówek `##` (`SectionKind.TableDocumentSection`), także gdy nazwa jest przerwana granicą strony;
- treść z prawych komórek płynie ciągle, ponad granicami wierszy i stron (akapity, listy, zagnieżdżone podpunkty);
- wiersz nazw kolumn („Definicje | Wyjaśnienie”), powtarzany na kolejnych stronach, jest pomijany i odnotowany w `Report.TableDocuments`;
- tabela-dokument nie tworzy `TableBlock` i nie jest wliczana do `Report.TableCount` ani `FallbackTableCount`;
- zwykła, krótka tabela (np. definicji) w regulaminie nadal jest tabelą GFM.

Wyłączenie: `o.Tables.DetectTableDocuments = false` lub `PDFPARSER__Tables__DetectTableDocuments=false`.
Pozostałe nowe opcje ustawia się tak samo, np. `PDFPARSER__Tables__TableDocumentMinPages=3`,
`PDFPARSER__Headings__DetectImageCaptions=false`.

Nowa wartość enuma `SectionKind.TableDocumentSection` — przy `switch` bez gałęzi `default` obsłuż ją jak `Typographic`
(nagłówek bez oznaczenia jednostki prawnej). Kontrakt zmian: `specs/002-table-document-sections/contracts/public-api.md` (1.1.0).

**Wskazówka dla chunkera opartego na nagłówkach:** dziel po `##`. Sekcje tabeli-dokumentu są na poziomie 2 pod tytułem
`#`, a ich treść jest ciągła (bez znaczników wierszy tabeli), więc każdy fragment jest samodzielną sekcją
o czytelnej nazwie; `Section.Path` zawiera tytuł i nazwę sekcji.

## Własne etapy potoku

Etap implementuje `IPipelineStage` (`int Order`, `void Execute(PipelineContext context)`); etapy są wykonywane
rosnąco wg `Order`. Wbudowane wartości są w stałych `StageOrder` (przestrzeń nazw `LegalAgent.PdfParser.Pipeline`):
`PageExtraction` 100, `TextNormalization` 200, `LineAssembly` 300, `ArtifactRemoval` 400, `FootnoteDetection` 500,
`StepSequence` 550, `TableDocument` 560, `TableDetection` 600, `ReadingOrder` 700, `ListDetection` 800, `HeadingDetection` 900, `BlockAssembly` 1000,
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
legalagent-pdf chunk <wejście.pdf> -o|--output <wyjście.jsonl> [--id <id>] [--designation <oznaczenie>]
                     [--type <typ>] [--title <tytuł>] [--doc-version <n>] [--valid-from <rrrr-mm-dd>]
                     [--valid-to <rrrr-mm-dd>] [--status <status>] [--previous-version <id>]
                     [--max-length <n>] [--allow-partial]
legalagent-pdf --help | --version
```

```bash
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- convert ustawa.pdf -o ustawa.md --report ustawa.report.json
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- convert ustawa.pdf --no-page-markers > ustawa.md
PDFPARSER__Limits__MaxPages=5000 dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- convert duza.pdf --allow-partial -o duza.md
```

Bez `-o` Markdown trafia na stdout. `--report` zapisuje `ConversionReport` jako JSON. Opcje heurystyk można ustawić zmiennymi
środowiskowymi `PDFPARSER__<Grupa>__<Pole>`. Komunikaty błędów (po polsku) trafiają na stderr.

`chunk` zapisuje fragmenty dokumentu jako JSON Lines (opis niżej); `--id` domyślnie to nazwa pliku (znaki spoza
`[A-Za-z0-9._-]` → `-`), limit długości: `--max-length` albo `CHUNKING__MaxChunkLength` (argument ma pierwszeństwo).
Błędne metadane lub opcje → kod 2; pozostałe kody jak dla `convert`.

```bash
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- chunk regulamin.pdf -o regulamin.chunks.jsonl \
  --id REG-06 --designation BP/REG/06 --type regulation --doc-version 3 --valid-from 2026-06-01 --status in-force
```

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

Pełny kontrakt: `specs/001-legal-pdf-parser/contracts/markdown-output.md` (uzupełnienia: `specs/002-table-document-sections/contracts/markdown-output.md`). W skrócie: CommonMark + GFM, UTF-8, LF.

- tytuł jako `#`, sekcje jako `##`…; jednostki prawne (Dział, Rozdział, Art., §) jako nagłówki o rosnącym poziomie;
- listy: `- 1\) treść`, zagnieżdżenie wcięciem 2 spacji; punktory i tiret jako `- treść` / `- – treść`;
- tabele GFM (`| a | b |` + separator `| --- |`); gdy tabeli nie da się wiarygodnie odtworzyć, wiersze są zapisywane jako akapity z komórkami rozdzielonymi ` \| `;
- przypisy: odnośnik `[^n]`, definicja `[^n]: treść` po treści artykułu/sekcji;
- znaczniki stron `<!-- page: N -->` (wyłączane opcją `Rendering.PageMarkers` / `--no-page-markers`); strony pominięte: `<!-- page N skipped: … -->`;
- pogrubienie `**t**`, kursywa `*t*`; znaki specjalne Markdown są escapowane.

## Podział na fragmenty (`LegalAgent.Chunking`)

Biblioteka zwraca wyłącznie modele w pamięci (nie czyta ani nie zapisuje plików) i działa na modelu `LegalDocument`
z wyniku parsera, nie na Markdown. Specyfikacja: `specs/004-document-chunking/`.

```csharp
var services = new ServiceCollection().AddLegalAgentChunking(o => o.MaxChunkLength = 2000);   // rejestruje też parser
var chunker = services.BuildServiceProvider().GetRequiredService<IDocumentChunker>();
var metadata = new DocumentMetadata("REG-06")
{
    Designation = "BP/REG/06", Type = "regulation", Version = 3,
    ValidFrom = new DateOnly(2026, 6, 1), Status = "in-force", PreviousVersion = "REG-06-w2",
};
ChunkedDocument doc = await chunker.ChunkAsync(File.OpenRead("REG-06.pdf"), metadata);   // albo ChunkAsync(wynikParsera, metadata)
string jsonl = ChunkJson.ToJsonLines(doc);                                                 // LegalAgent.Chunking.Serialization
```

- **Jednostka** = wstęp albo własna treść jednej sekcji (paragraf, artykuł, sekcja taryfy, procedury, tabeli-dokumentu).
  Jednostka dłuższa niż limit (domyślnie 2000 znaków, co najmniej 200) jest dzielona między akapitami, pozycjami list
  (dowolnego poziomu) i wierszami tabel — nigdy w środku; każda część zaczyna się od nagłówka jednostki, a części
  tabeli od jej wiersza nagłówka. Fragment nigdy nie łączy dwóch sekcji.
- **Treść** to oryginalny tekst w Markdown (renderer parsera, bez znaczników stron), bez dopisanych słów; kontekst jest
  w metadanych: `citation` (np. „§ 13”), `listLabels` (np. `["3.", "2)"]` dla kontynuacji), `sectionPath`, `pages`.
- **`unitKey`** (np. `BP/REG/06 | § 30`) jest wspólny dla tej samej jednostki we wszystkich wersjach dokumentu — po nim
  porównuje się wersje; **`chunk.id`** jest stabilny i unikalny (identyfikator dokumentu + skrót klucza + część).
- Wartości metadanych przeznaczone dla modelu (`type`, `status`) podaje się po angielsku (`regulation`, `in-force`, …).
- Opcje per wywołanie: `ChunkingRequest { ConfigureOptions = …, ParserRequest = … }`; błędne metadane →
  `ArgumentException`, błędne opcje → `OptionsValidationException`, wyjątki parsera przechodzą bez zmian.
- **Kontrakt JSON** (wspólny dla serwisu i plików, `schemaVersion` 1):
  `specs/004-document-chunking/contracts/chunks-json.md` — jedna samodzielna linia na fragment
  (`schemaVersion`, `document`, `chunk`).

## Pobieranie regulaminów (`mBank.FaqGenerator`)

Aplikacja pobiera **5 regulaminów PDF** równolegle do katalogu pobrań (domyślnie `./downloads` względem bieżącego
katalogu) i zapisuje `manifest.json`, który wiąże każdy plik z adresem źródła. Logika (sprawdzanie adresów, nazwy
plików, pobieranie, zapis atomowy, manifest, sprzątanie) jest w bibliotece **`LegalAgent.Downloads`**; aplikacja to
cienka warstwa (argumenty, konfiguracja, pytania, komunikaty, kody wyjścia). Specyfikacja:
`specs/005-regulation-download/`.

```bash
# tryb pytań: aplikacja prosi kolejno o 5 adresów
dotnet run --project src/mBank.FaqGenerator -c Release

# jedno polecenie, bez pytań: 5 adresów w argumentach
dotnet run --project src/mBank.FaqGenerator -c Release -- \
  --url https://www.mbank.pl/pdf/…/a.pdf --url … --url … --url … --url … [--output <katalog>]
```

**Skąd adresy:** `--url` podane dokładnie 5 razy → w przeciwnym razie niepusta lista `Download:Urls` z
konfiguracji (musi mieć 5 pozycji) → w przeciwnym razie pytania w konsoli. Każdy adres musi być `https://`, z hosta
z listy `Download:AllowedHosts` (domyślnie `mbank.pl` i jego subdomeny) i nie może się powtarzać; przekierowania są
wykonywane tylko do dozwolonych hostów.

**Konfiguracja** (rosnący priorytet): `appsettings.json` obok pliku wykonywalnego → `appsettings.Local.json`
(opcjonalny, ignorowany przez git) → zmienne środowiskowe `FAQGEN__<Sekcja>__<Pole>` → argumenty.

| Klucz (`Download:…`) | Domyślnie | Znaczenie |
|----------------------|-----------|-----------|
| `Urls` | `[]` | 5 adresów (pusta lista = pytania), np. `FAQGEN__Download__Urls__0=https://…` … `__4` |
| `AllowedHosts` | `["mbank.pl"]` | dozwolone hosty (z subdomenami) |
| `AllowHttp` | `false` | czy dopuszczać `http://` |
| `OutputDirectory` | `downloads` | katalog pobrań (`--output` ma pierwszeństwo) |
| `TimeoutSeconds` | `60` | limit czasu na plik (liczba, może być ułamkowa) |
| `MaxFileSizeMegabytes` | `50` | limit rozmiaru pliku |
| `MaxRedirects` | `5` | limit przekierowań (0–20) |
| `UserAgent` | `mBank.FaqGenerator/1.0` | nagłówek `User-Agent` |

**Wynik:** pliki PDF o nazwach z ostatniego segmentu adresu (oczyszczonych; kolizje dostają przyrostek `-N`) i
`manifest.json` (kontrakt: `specs/005-regulation-download/contracts/download-manifest.md`). Plik trafia pod docelową
nazwę dopiero po pełnym pobraniu i sprawdzeniu sygnatury `%PDF-`, więc błąd nie zostawia niekompletnego pliku ani
nie psuje poprzedniej wersji. Ponowne uruchomienie nadpisuje pliki; po pobraniu 5 z 5 usuwane są pliki PDF spoza
bieżącej listy. Niedostępny link (kod błędu, przekroczony czas, brak połączenia, strona HTML zamiast PDF) nie
przerywa pozostałych pobrań — przyczyna trafia do podsumowania i manifestu.

**Kody wyjścia etapu pobierania:** 2 błędne argumenty, konfiguracja, lista adresów lub zamknięte wejście przy
pytaniach; 3 co najmniej jedno pobranie nieudane; 4 błąd katalogu pobrań (utworzenie, manifest, sprzątanie);
130 przerwano (Ctrl+C); 1 błąd nieoczekiwany. Po pobraniu 5 z 5 aplikacja przechodzi do konwersji i FAQ, a kod 0
oznacza zapisany plik FAQ (pełna lista kodów — niżej).

Pobrane regulaminy nie są częścią repozytorium (`downloads/` w `.gitignore`).

## Generowanie FAQ (`mBank.FaqGenerator`, `LegalAgent.Faq`)

Po pobraniu 5 z 5 aplikacja:
1. konwertuje każdy PDF do Markdown parserem z domyślnymi opcjami (`<nazwa>.md` obok PDF-u, zapis atomowy;
   po udanej konwersji usuwa `*.md` spoza bieżącej listy);
2. sprawdza rozmiar dokumentów (szacunek: 3 znaki na token, limit `Faq:MaxDocumentTokens`);
3. pyta o klucz API;
4. generuje FAQ modelem Azure OpenAI w dwóch krokach: osobno dla każdego dokumentu do 10 kandydatów (kolejno,
   bez równoległości), potem jedno zapytanie wybierające 10 najważniejszych pytań;
5. sprawdza każdą odpowiedź (JSON zgodny ze schematem, liczba pozycji, powtórzenia, istniejące dokumenty, kandydaci
   i jednostki redakcyjne, np. „§ 12”) i zapisuje `faq/FAQ_mBank.md`.

Logika konwersji, generowania, walidacji i renderowania jest w bibliotece **`LegalAgent.Faq`** (zależy tylko od
`Microsoft.SemanticKernel.Abstractions`). Aplikacja dodaje konektor Azure OpenAI, konfigurację, klucz i komunikaty.
Specyfikacja: `specs/006-faq-generation/`.

### Zasób Azure OpenAI

```bash
az login
scripts/azure/create-openai.sh            # grupa rg-faqgen, zasób faqgen-<skrót subskrypcji>, wdrożenie gpt-4o-mini
scripts/azure/create-openai.sh --help     # parametry: --resource-group, --location, --name, --deployment, --model, …
```

Skrypt jest idempotentny (drugie uruchomienie niczego nie tworzy), nie odczytuje klucza i na końcu wypisuje fragment
`appsettings.Local.json` oraz zmienne `FAQGEN__AzureOpenAI__…`. Na Windows uruchom go w Git Bash.

**Przez portal Azure** (zamiast skryptu):
1. Utwórz zasób **Azure OpenAI** (warstwa `Standard S0`, dostęp ze wszystkich sieci).
2. W portalu Foundry wybierz **Wdrożenia → Wdróż model bazowy**, np. `gpt-4.1-mini` (`Global Standard`), i ustaw
   jak najwyższy limit tokenów na minutę. Pierwsze zapytanie wysyła cały regulamin (~50 tys. tokenów), a aplikacja
   nie ponawia zapytań po błędzie 429.
3. Z **Klucze i punkt końcowy** skopiuj punkt końcowy do `appsettings.Local.json`, a nazwę wdrożenia wpisz w
   `Deployment`. Klucz wkleisz dopiero na pytanie aplikacji.

**Model:** domyślnie `gpt-4o-mini` w wersji `2024-07-18`. Ma on w Azure status *Deprecated* (wycofanie 2027-04-14):
subskrypcja, która nigdy go nie wdrażała, nie utworzy nowego wdrożenia. Skrypt sprawdza to przed utworzeniem zasobu
i kończy się kodem 4 z podpowiedzią, np.:

```bash
scripts/azure/create-openai.sh --model gpt-5.4-mini --model-version 2026-03-17 --deployment gpt-5.4-mini
```

Modele GPT-5 nie przyjmują temperatury, więc wtedy ustaw `"Temperature": null` (albo `FAQGEN__AzureOpenAI__Temperature=`).

**Usunięcie zasobów** (koniec kosztów): `az group delete --name rg-faqgen --yes`.

### Konfiguracja

Endpoint nie jest sekretem; wpisz go do `src/mBank.FaqGenerator/appsettings.Local.json` (ignorowany przez git,
kopiowany do katalogu wyjściowego) albo ustaw zmienną `FAQGEN__AzureOpenAI__Endpoint`.

| Klucz | Domyślnie | Znaczenie |
|-------|-----------|-----------|
| `AzureOpenAI:Endpoint` | `""` | adres zasobu, wymagany, `https://` |
| `AzureOpenAI:Deployment` | `gpt-4o-mini` | nazwa wdrożenia, wymagana |
| `AzureOpenAI:Model` | `gpt-4o-mini` | nazwa modelu do nagłówka FAQ |
| `AzureOpenAI:TimeoutSeconds` | `300` | limit czasu jednego zapytania (bez ponowień) |
| `AzureOpenAI:Temperature` | `0` | 0–2; `null` / pusta wartość = nie wysyłaj |
| `AzureOpenAI:Seed` | `42` | `null` / pusta wartość = nie wysyłaj |
| `AzureOpenAI:MaxOutputTokens` | `4096` | limit tokenów odpowiedzi |
| `Faq:OutputDirectory` | `faq` | katalog OKF z `FAQ_mBank.md` (`--faq-output` ma pierwszeństwo) |
| `Faq:CandidatesPerDocument` | `10` | kandydaci na dokument, 1–30 |
| `Faq:MaxDocumentTokens` | `100000` | limit szacowanych tokenów jednego dokumentu |

Konfiguracja jest sprawdzana przy starcie, zanim aplikacja zapyta o adresy (brak endpointu → kod 2).

### Klucz API

Klucz **nigdy** nie jest opcją, zmienną środowiskową ani wpisem konfiguracji (`AzureOpenAI:ApiKey` jest odrzucany
z kodem 2). Aplikacja pyta o niego dopiero po udanym pobraniu i konwersji:
- **w konsoli:** „Klucz API Azure OpenAI: ” — każdy wpisany lub wklejony znak to jedna `*`, Backspace cofa, Enter
  kończy, Ctrl+C przerywa (kod 130);
- **potokiem:** przy przekierowanym wejściu klucz to kolejny wiersz (po adresach, jeśli też były czytane z wejścia):

```bash
az cognitiveservices account keys list -g rg-faqgen -n <zasób> --query key1 -o tsv \
  | dotnet run --project src/mBank.FaqGenerator -c Release -- --url <a> --url <b> --url <c> --url <d> --url <e>
```

Klucz zostaje tylko w pamięci procesu; komunikaty błędów są z niego czyszczone (`***`).

### Wynik

`faq/FAQ_mBank.md` (katalog nie jest ignorowany przez git) to dokument OKF: nagłówek YAML (`type: faq`, `title`,
`description`, `resource` — 5 adresów, `timestamp`, `model`, `deployment`) i 10 sekcji `## <pytanie>` z odpowiedzią
i wierszem `Źródło:` / `Źródła:` (link do dokumentu i jednostka). Kontrakt: `specs/006-faq-generation/contracts/faq-file.md`.
Treść odpowiedzi pochodzi z modelu i może się różnić między uruchomieniami (konstytucja 1.4.0, zasada III);
poprzedni plik zostaje nienaruszony przy każdym błędzie.

**Kody wyjścia:** 0 pobrano 5 z 5, przekonwertowano 5 z 5 i zapisano FAQ; 1 błąd nieoczekiwany; 2 argumenty,
konfiguracja, adresy lub brak wejścia (adresów albo klucza); 3 nie wszystkie pliki pobrane; 4 błąd zapisu
(katalog pobrań, Markdown, FAQ); 5 błąd konwersji; 6 błąd usługi modelu (klucz, wdrożenie, limit 429, filtr treści,
czas, sieć) albo dokument za długi; 7 odpowiedź modelu odrzucona przy sprawdzaniu; 130 przerwano.

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
- **Fragmenty:** `tests/LegalAgent.Chunking.Tests` — testy jednostkowe podziału i metadanych, kontrakt JSON, determinizm,
  pliki wzorcowe `Golden/*.chunks.jsonl` (`UPDATE_GOLDEN=1`, różnice jako `*.actual.jsonl`), pokrycie słów całego korpusu
  (`CorpusFull`) i wydajność (`Performance`).
- **Pobieranie:** `tests/LegalAgent.Downloads.Tests` (biblioteka) i `tests/mBank.FaqGenerator.Tests` (aplikacja przez
  `Program.RunAsync`) działają bez sieci — odpowiedzi serwera zastępuje atrapa `FakeHttpHandler`, a każdy test aplikacji
  ma własny `appsettings.json` w katalogu tymczasowym.
- **FAQ:** `tests/LegalAgent.Faq.Tests` (biblioteka: dopasowanie jednostek, parser i walidacja odpowiedzi, renderer z
  plikiem wzorcowym `Golden/faq.expected.md`, generator, konwersja syntetycznych PDF-ów) i testy aplikacji z atrapami
  modelu (`FakeChatCompletionService`), klawiatury (`FakeKeyInput`), czasu i HTTP konektora Azure; żaden test nie łączy
  się z Azure. `AzureScriptTests` uruchamiają skrypt z atrapą `az` i są pomijane, gdy nie ma `bash`.
- CI (`.github/workflows/ci.yml`, ubuntu-latest) buduje i uruchamia testy z filtrem `Category!=Performance`, a następnie osobno `Category=Performance`.

## Korpus syntetyczny

Obok parsera repozytorium zawiera **syntetyczny korpus polskich dokumentów bankowych** („Bank Przykładowy S.A.”):
regulaminy, taryfy i procedury wewnętrzne w wielu wersjach, dokumenty nieaktualne i sprzeczne, dokumenty zatrute
(wstrzyknięcia w treści) oraz 10 aktów prawnych. Do każdego PDF dołączony jest Markdown i plik fragmentów
`*.chunks.jsonl` (gotowy do zaindeksowania), a **manifest prawdy referencyjnej**
(`corpus/manifest.json`) opisuje zmiany między wersjami, pary sprzeczności i rodzaje zatruć. Korpus służy do testowania
konwersji PDF → Markdown (`LegalAgent.PdfParser`) oraz aplikacji RAG. Pełna instrukcja: [`corpus/README.md`](corpus/README.md).

Projekty korpusu (pełna lista: [Struktura rozwiązania](#struktura-rozwiązania)):

| Projekt | Rola |
|---------|------|
| `src/LegalAgent.Corpus` | generator korpusu syntetycznego (PDF, manifest, czcionki) |
| `src/LegalAgent.Corpus.Cli` | CLI generatora: `generate`, `refresh`, `verify`, `check` |
| `tests/LegalAgent.Corpus.Tests` | testy generatora i jakości korpusu |

```bash
dotnet run --project src/LegalAgent.Corpus.Cli -- generate   # wygenerowanie korpusu
dotnet run --project src/LegalAgent.Corpus.Cli -- refresh    # odświeżenie Markdown i fragmentów (np. po zmianie parsera)
dotnet run --project src/LegalAgent.Corpus.Cli -- verify     # sprawdzenie zgodności z zapisanym korpusem
dotnet run --project src/LegalAgent.Corpus.Cli -- check      # kontrole jakości
LEGALAGENT_CORPUS_FULL=1 dotnet test LegalAgent.slnx -c Release --filter "Category=CorpusFull"   # testy całego korpusu
```

Domyślnie testy sprawdzają próbkę korpusu; testy `CorpusFull` są pomijane bez `LEGALAGENT_CORPUS_FULL=1`
(PowerShell: `$env:LEGALAGENT_CORPUS_FULL = "1"`). Szczegóły i opcje poleceń: `corpus/README.md`.

**Licencja czcionek:** PDF-y korpusu osadzają czcionki Noto Sans i Noto Sans Mono (`src/LegalAgent.Corpus/Fonts/`)
na licencji SIL Open Font License 1.1; jej tekst: `src/LegalAgent.Corpus/Fonts/OFL.txt`.

## Ograniczenia

- Strony skanowane (bez warstwy tekstowej) **nie są** przetwarzane OCR-em — są pomijane (znacznik `no-text-layer`, `IsComplete == false`).
- Złożone, wielopoziomowe nagłówki tabel nie są odtwarzane — tabela spada do trybu zapasowego (wiersze jako akapity z komórkami rozdzielonymi ` | `).
- Podwójne brzmienie całych artykułów w tekstach ISAP (`[Art. …]` / `<Art. …>`) nie jest strukturyzowane.
