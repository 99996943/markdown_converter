---

description: "Task list for spec 004 — podział dokumentów na fragmenty"
---

# Tasks: Podział dokumentów na fragmenty dla demonstracyjnej aplikacji RAG

**Input**: Design documents from `specs/004-document-chunking/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: OBOWIĄZKOWE — konstytucja, zasada I (TDD, NON-NEGOTIABLE). Zadanie testowe poprzedza
implementację i MUSI najpierw padać **na asercji** (nie na kompilacji — w razie potrzeby szkielet typów
z `NotImplementedException`). Osobne commity: `test: … (red)`, potem `feat:`/`fix: …` (green). Testy
offline i deterministyczne. Test, który przechodzi od razu (charakteryzacja zachowania już
zapewnionego wcześniejszym zadaniem), jest commitowany jako `test:` z adnotacją w opisie commita.

**Organization**: zadania pogrupowane wg historyjek ze spec.md: US1 (P1) fragmenty z biblioteki, US2
(P1) porównanie wersji przez klucz jednostki, US3 (P2) pliki fragmentów w korpusie i CLI. **Kolejność
faz: US1 → US3 → US2** — test FR-273 (US2) czyta zacommitowane pliki fragmentów korpusu z US3 (R11);
reguły klucza jednostki, od których zależy US2, powstają już w fazie 2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: można wykonać równolegle (inne pliki, brak zależności od niezakończonych zadań)
- **[Story]**: historyjka (US1…US3)

## Path Conventions

- Biblioteka: `src/LegalAgent.Chunking/` (dalej `chunk-lib/`), testy: `tests/LegalAgent.Chunking.Tests/`
  (dalej `chtests/`); parser: `src/LegalAgent.PdfParser/`, testy parsera:
  `tests/LegalAgent.PdfParser.Tests/` (dalej `ptests/`); generator: `src/LegalAgent.Corpus/`
  (dalej `corpus-lib/`), testy generatora: `tests/LegalAgent.Corpus.Tests/` (dalej `ctests/`).
- Konwencje jak w 001–003: `CultureInfo.InvariantCulture`, `StringComparison.Ordinal*`, jawne
  sortowania (ordinal), LF, UTF-8 bez BOM, XML-doc publicznych typów, `TreatWarningsAsErrors`.
- Biblioteka: żadnego I/O plików, konsoli, zmiennych środowiskowych, `DateTime.Now`, `Guid.NewGuid`,
  `System.Random`, mutowalnego stanu statycznego (FR-200, FR-205).
- Treść fragmentu wyłącznie z renderera parsera — nigdy dopisany tekst (FR-230, pamięć „no added text”).
- Odniesienia „R*n*” = decyzje w research.md; formaty = contracts/*.md; pola = data-model.md.
- Pliki wzorcowe parsera (`ptests/Corpus/**/*.expected.md`) NIE MOGĄ się zmienić (SC-046, FR-163).

---

## Phase 1: Setup

- [X] T001 Utwórz `src/LegalAgent.Chunking/LegalAgent.Chunking.csproj` (class library, `GenerateDocumentationFile`, `RootNamespace` `LegalAgent.Chunking`, `ProjectReference` do `src/LegalAgent.PdfParser`, `PackageReference` `Microsoft.Extensions.DependencyInjection.Abstractions` i `Microsoft.Extensions.Options`, `InternalsVisibleTo` `LegalAgent.Chunking.Tests`) i `tests/LegalAgent.Chunking.Tests/LegalAgent.Chunking.Tests.csproj` (jak `ctests` csproj: Exe, xunit.v3, runner, Test.Sdk, `NoWarn CA1707`, `Using Xunit`, referencje do `LegalAgent.Chunking`, `LegalAgent.PdfParser`, `Microsoft.Extensions.DependencyInjection`); dodaj oba do `LegalAgent.slnx`; `dotnet build LegalAgent.slnx -c Release` zielony
- [X] T002 [P] Szkielet modeli publicznych z data-model.md w `chunk-lib/Model/` (`DocumentMetadata`, `ChunkedDocument`, `ChunkedDocumentHeader`, `ChunkSource`, `Chunk`, `PageSpan`, `ChunkUnitKind` = `Preamble` + wartości `SectionKind`), `chunk-lib/Options/ChunkingOptions.cs` (`MaxChunkLength = 2000`, `Rendering`), `chunk-lib/ChunkingRequest.cs`, `chunk-lib/IDocumentChunker.cs` z sygnaturami z contracts/library-api.md; `DocumentChunker` z `NotImplementedException`; XML-doc
- [X] T003 [P] Narzędzia testowe w `chtests/Fixtures/`: `DocBuilder.cs` (płynne budowanie `LegalDocument`/`Section`/`ParagraphBlock`/`ListBlock`/`TableBlock`/`Footnote` z `PageBreak` i stronami, tak by testy jednostkowe nie potrzebowały PDF), `GoldenFile.cs` (kopia `ptests/Fixtures/GoldenFile.cs` dla `*.chunks.jsonl`, `*.actual.jsonl`, `UPDATE_GOLDEN=1`), `RepoPaths.cs` (katalog główny repozytorium, `corpus/`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: rejestracja i walidacja, renderowanie fragmentu, klucz jednostki i identyfikator —
wspólne dla wszystkich historyjek.

**⚠️ CRITICAL**: żadna historyjka nie startuje przed końcem tej fazy.

- [X] T004 Test (red) w `chtests/Unit/RegistrationAndValidationTests.cs`: `AddLegalAgentChunking` rejestruje `IDocumentChunker` (singleton) i `IPdfMarkdownConverter`, jest idempotentne, `configure` się kumuluje; walidacja opcji: `MaxChunkLength` „≥ 200” (199 → `OptionsValidationException`); walidacja metadanych przed pracą (`ArgumentException`): `DocumentId` wymagany i zgodny z „`^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$`”, `Version` „> 0”, `ValidTo` „≥ `ValidFrom`, gdy oba podane”; `ConfigureOptions` z `ChunkingRequest` zmienia tylko kopię opcji dla jednego wywołania
- [X] T005 Implementacja (green): `chunk-lib/ChunkingServiceCollectionExtensions.cs` (namespace `Microsoft.Extensions.DependencyInjection`, wywołuje `AddLegalAgentPdfParser()`, `TryAdd*`), `chunk-lib/Options/ChunkingOptionsValidator.cs`, walidacja metadanych i kopia opcji w `chunk-lib/DocumentChunker.cs`
- [X] T006 [P] Test (red) w `chtests/Unit/FragmentRendererTests.cs` (R1): sekcja z akapitem, listą, tabelą GFM, tabelą zastępczą i przypisem renderuje się identycznie jak odpowiadający jej fragment `MarkdownRenderer.Render` całego dokumentu po usunięciu znaczników stron; brak `<!-- page: N -->` i `# tytułu`; wstęp bez nagłówka; `SkippedPageBlock` pominięty; `ListBlock` z pozycjami zagnieżdżonymi (część zaczynająca się w środku listy) renderuje się od kolumny 0 — bez bloku kodu — z dosłowną etykietą i zagnieżdżeniem ich dzieci; `RenderingOptions.PageMarkers` wymuszone na `false` nawet przy `Rendering.PageMarkers = true`
- [X] T007 Implementacja (green) `chunk-lib/Splitting/FragmentRenderer.cs`: mały `LegalDocument` (`Title = null`, jedna `Section` o tym samym `Level`/`Kind`/`HeadingText`, `Children = []`) renderowany publicznym `IMarkdownRenderer`; listy od kolumny 0 (R1); wynik bez końcowego LF
- [X] T008 [P] Test (red) w `chtests/Unit/UnitKeyBuilderTests.cs` (R6): segment = `Designation` bez kropki końcowej ze znormalizowanymi spacjami, bez oznaczenia — `HeadingText`, wstęp — `~wstep`; klucz `<SeriesKey> | <ścieżka>`, `SeriesKey` = `Designation` ?? `DocumentId`; najkrótszy unikalny sufiks ścieżki łączony `" > "` (np. `BP/REG/05 | Oprocentowanie > § 2`, gdy „§ 2” występuje w dwóch sekcjach); pełna ścieżka powtórzona → `" #2"`, `" #3"` w kolejności dokumentu; wstawienie rozdziału przed paragrafem nie zmienia klucza „§ 11”
- [X] T009 Implementacja (green) `chunk-lib/Identity/UnitKeyBuilder.cs`
- [X] T010 [P] Test (red) w `chtests/Unit/ChunkIdBuilderTests.cs` (R7): `<DocumentId>_<hex16(SHA-256(UTF-8(UnitKey)))>_<Part>`, małe litery hex, zgodność z „`^[A-Za-z0-9][A-Za-z0-9._-]*_[0-9a-f]{16}_[0-9]+$`”, długość ≤ 120, stabilność (wartość oczekiwana zapisana w teście), różne klucze → różne id
- [X] T011 Implementacja (green) `chunk-lib/Identity/ChunkIdBuilder.cs`

**Checkpoint**: fundament gotowy — można zaczynać historyjki.

---

## Phase 3: User Story 1 — Fragmenty dokumentu z biblioteki w serwisie (Priority: P1) 🎯 MVP

**Goal**: serwis dostaje w pamięci dokument z metadanymi i listę fragmentów: jednostki, podział
długich jednostek, treść, metadane, strony, wejście PDF, determinizm, serializacja JSON.

**Independent Test**: testy jednostkowe na modelach z `DocBuilder` i wywołanie na PDF korpusu; granice,
treść i metadane zgodne z FR-220 – FR-244; pokrycie słów FR-234.

- [X] T012 [US1] Test (red) w `chtests/Unit/UnitCollectorTests.cs` (R2): jednostki w kolejności dokumentu: wstęp (gdy niepusty) + każda sekcja z własnymi blokami (poza `SkippedPageBlock`) lub przypisami; rozdział tylko z paragrafami nie tworzy jednostki, ale jest w `SectionPath` paragrafów; rozdział z tekstem przed pierwszym paragrafem tworzy własną jednostkę; tytuł dokumentu nie jest jednostką; `UnitKind` = `Preamble` lub `SectionKind`
- [X] T013 [US1] Implementacja (green) `chunk-lib/Splitting/UnitCollector.cs`
- [X] T014 [US1] Test (red) w `chtests/Unit/WholeUnitChunkTests.cs`: jednostka ≤ 2000 znaków → dokładnie jeden fragment (`Part = 1`, `PartCount = 1`) zaczynający się od nagłówka jednostki; `Citation` = `Designation` (np. „§ 13”) albo `HeadingText`, `null` dla wstępu; `ListLabels` puste; `SectionPath` = `Section.Path`; `Length` = `Content.Length`; `ExceedsLimit = false`; `ChunkedDocument.Title` = `Metadata.Title` ?? tytuł parsera, `DetectedTitle`, `SeriesKey`, `Source` (`PageCount`, `Sha256`, `IsComplete`, `SkippedPages`); dokument pusty → pusta lista fragmentów; bardzo krótka jednostka („Art. 5. (uchylony)”) nie jest łączona z sąsiednią; metadane bez `Version` i dat — fragmenty i identyfikatory jak zwykle (C6); fragment nigdy nie zawiera treści dwóch sekcji
- [X] T015 [US1] Implementacja (green) ścieżki `ChunkAsync(PdfConversionResult, …)` w `chunk-lib/DocumentChunker.cs` dla jednostek mieszczących się w limicie (UnitCollector → FragmentRenderer → UnitKeyBuilder/ChunkIdBuilder), sprawdzanie tokenu przed pracą i między jednostkami
- [X] T016 [US1] Test (red) w `chtests/Unit/SplitParagraphsTests.cs` (R3, FR-222, FR-224, FR-231): jednostka z akapitami > limit → części pakowane zachłannie na granicach akapitów, każda zaczyna się od nagłówka jednostki (powtórzonego dosłownie), żaden akapit nie jest przecięty; akapit dłuższy niż limit → osobna część z `ExceedsLimit = true` zawierająca dokładnie ten akapit; części tej jednostki mają `Part = 1..PartCount`, ten sam `UnitKey`/`Citation`/`SectionPath`, różne `ChunkId`; kolejność wszystkich fragmentów dokumentu = kolejność treści (FR-225); wstęp dzielony bez nagłówka
- [X] T017 [US1] Implementacja (green) `chunk-lib/Splitting/UnitSplitter.cs` — atomy i pakowanie dla akapitów (pomiar długości przez FragmentRenderer)
- [X] T018 [US1] Test (red) w `chtests/Unit/SplitListsTests.cs` (FR-222, FR-241): lista > limit dzielona między pozycjami najwyższego poziomu; pozycja > limit dzielona rekurencyjnie między pozycjami zagnieżdżonymi (własny tekst pozycji z akapitami-dziećmi to atom, nigdy przecięty); część zaczynająca się od pozycji ma `ListLabels` = etykiety tej pozycji i jej przodków od zewnętrznej (np. `["3.", "2)"]`), dosłownie, bez „ust.”/„pkt”; pozostałe pozycje każdego poziomu jako osobne listy od kolumny 0 (R1, bez bloku kodu); pojedyncza pozycja > limit → `ExceedsLimit = true`
- [X] T019 [US1] Implementacja (green) dzielenia list w `chunk-lib/Splitting/UnitSplitter.cs`
- [X] T020 [US1] Test (red) w `chtests/Unit/SplitTablesTests.cs` (FR-223): tabela > limit dzielona po całych wierszach; każda część zaczyna się od wiersza `Header`, gdy istnieje; tabela bez nagłówka — bez powtórzeń; tabela zastępcza (`IsFallback`) dzielona po wierszach; wiersz > limit → `ExceedsLimit = true`; akapit przed tabelą i tabela w jednej jednostce pakowane razem, gdy się mieszczą
- [X] T021 [US1] Implementacja (green) dzielenia tabel w `chunk-lib/Splitting/UnitSplitter.cs`
- [X] T022 [US1] Test (red) w `chtests/Unit/FootnoteSelectionTests.cs` (R5, FR-232): część zawiera definicje przypisów, do których prowadzi `FootnoteRef` w jej treści (akapity, pozycje, komórki), w kolejności numerów; przypis z odwołaniami w dwóch częściach jest w obu; przypisy bez odwołania w jednostce (w tym `IsOrphan`) — tylko w ostatniej części; przypisy wstępu z `PreambleFootnotes`; długość przypisów wliczana do limitu
- [X] T023 [US1] Implementacja (green) `chunk-lib/Splitting/FootnoteSelector.cs` i jej użycie w `UnitSplitter`
- [X] T024 [US1] Test (red) w `ptests/Unit/Stages/TableDetectionStageTests.cs` (R4; strony układu jak w teście FR-065 zamiast PDF): tabela przechodząca przez dwie strony — `TableRow.Page` każdego wiersza = strona, na której wiersz się zaczyna; Markdown dokumentu bez zmian
- [X] T025 [US1] Implementacja (green): `public int? Page { get; init; }` w `src/LegalAgent.PdfParser/Model/TableRow.cs` (XML-doc: „strona, na której zaczyna się wiersz; null, gdy nieznana”), ustawiane w `src/LegalAgent.PdfParser/Stages/TableDetectionStage.cs`; pełny zestaw testów parsera z `LEGALAGENT_PRIVATE_CORPUS` — pliki wzorcowe bez zmian (SC-046)
- [X] T026 [US1] Test (red) w `chtests/Unit/PageRangeTests.cs` (R4, FR-242): pierwsza część jednostki zaczyna się na `Section.Pages.First`; część zaczynająca się od pozycji listy po `PageBreak(7)` ma `Pages.First = 7`; część tabeli zaczyna się na `TableRow.Page` pierwszego wiersza danych (nie nagłówka), a przy `Page == null` — na `TableBlock.Pages.First`; `Pages.Last` = maksimum stron treści; zakres przycięty do `Section.Pages`; `First ≤ Last`; jednostka z `SkippedPageBlock` strony 5 między treścią ze stron 4 i 6 ma zakres 4–6, a część zawierająca tylko treść ze strony 6 zaczyna się na 6 (nie na 5)
- [X] T027 [US1] Implementacja (green) `chunk-lib/Splitting/PageTracker.cs` i jej użycie w `UnitSplitter`
- [ ] T028 [US1] Test (red) w `chtests/Integration/PdfInputTests.cs`: `ChunkAsync(Stream, …)` na `corpus/regulaminy/REG-06.pdf` daje wynik identyczny (serializacja) z `ChunkAsync(await converter.ConvertAsync(...), …)`; `ParserRequest` przekazywany do parsera; wyjątek parsera (uszkodzony PDF) przechodzi bez opakowania; anulowany token → `OperationCanceledException` i brak wyniku; wynik niekompletny (`IsComplete = false`) → `Source.IsComplete = false`
- [ ] T029 [US1] Implementacja (green) ścieżki `ChunkAsync(Stream, …)` w `chunk-lib/DocumentChunker.cs`
- [ ] T030 [US1] Test (red) w `chtests/Serialization/ChunkJsonTests.cs` (contracts/chunks-json.md): `ChunkJson.ToJsonLines` — jedna linia na fragment, LF także po ostatniej, brak wcięć, kolejność pól z kontraktu, pola `null` pominięte, tablice puste zapisane, polskie znaki i `§` dosłownie, `\n` w `content` escapowane, daty `yyyy-MM-dd`, `schemaVersion: 1`, `unitKind` w camelCase (`tableDocumentSection`); dokument bez fragmentów → pusty tekst; `ReadLines` — round-trip, nieznane pola ignorowane, brak `schemaVersion`/pola wymaganego/`schemaVersion` > 1 → `FormatException` z numerem linii
- [ ] T031 [US1] Implementacja (green) `chunk-lib/Serialization/ChunkJson.cs` (`Utf8JsonWriter`, `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, `JsonDocument` przy odczycie) i `ChunkRecord`
- [ ] T032 [P] [US1] Test w `chtests/Determinism/DeterminismTests.cs` (FR-205): 5 kolejnych i 16 równoległych podziałów tego samego wyniku konwersji (`Parallel.ForEachAsync`) na jednej instancji z DI oraz pod kulturami `pl-PL` i `tr-TR` dają identyczne bajty `ToJsonLines`; poprawki (green), jeśli test padnie
- [ ] T033 [P] [US1] Test w `chtests/Unit/WordCoverageTests.cs` (FR-234, SC-041) z pomocnikiem `chtests/Fixtures/WordCoverage.cs`: wielozbiór słów połączonej treści fragmentów minus słowa powtórzone wg FR-231 (nagłówek jednostki w częściach 2+) i FR-232 (przypisy powtórzone) = wielozbiór słów Markdown całego dokumentu bez znaczników stron, znaczników stron pominiętych i wiersza `# tytułu`; na modelach z T016–T022 i na `corpus/taryfy/TAR-04.pdf`; poprawki (green), jeśli test padnie

**Checkpoint**: US1 działa samodzielnie — serwis może dzielić dokumenty i serializować wynik (MVP).

---

## Phase 4: User Story 3 — Fragmenty korpusu i dowolnego PDF w plikach (Priority: P2)

**Goal**: `legalagent-pdf chunk` zapisuje JSONL; korpus ma `<id>.chunks.jsonl` obok Markdown,
zapisywane przez `generate`/`refresh` i sprawdzane przez `verify`.

**Independent Test**: CLI na PDF → plik zgodny z kontraktem i kody wyjścia; `refresh` → pliki
fragmentów z metadanymi z manifestu; drugi `refresh` bez zmian; zmieniony plik → `verify` kod 1.

- [ ] T034 [US3] Test (red) w `ptests/Integration/CliTests.cs` (contracts/cli.md): `chunk <pdf> -o <plik>` → kod 0 i JSONL czytelny przez `ChunkJson.ReadLines`; `--id`, `--designation`, `--type`, `--title`, `--doc-version`, `--valid-from`, `--valid-to`, `--status`, `--previous-version`, `--max-length` trafiają do rekordów; `--id` domyślnie z nazwy pliku (znaki spoza `[A-Za-z0-9._-]` → `-`); `CHUNKING__MaxChunkLength` działa, argument ma pierwszeństwo; brak `-o`, brak pliku, `--doc-version 0`, `--valid-from 2025-13-01`, `--max-length 10` → kod 2 z komunikatem po polsku; PDF uszkodzony → kod 3; PDF z nieczytelną stroną (syntetyczny, jak w istniejących testach `convert`) bez `--allow-partial` → kod 4 i brak pliku, z `--allow-partial` → kod 6, plik zapisany, `document.source.isComplete = false` i strona w `skippedPages`; zapis atomowy (brak pliku po błędzie); stdout: liczba fragmentów i przekraczających limit; pomoc wymienia `chunk`
- [ ] T035 [US3] Implementacja (green) polecenia `chunk` w `src/LegalAgent.PdfParser.Cli/Program.cs` (parsowanie argumentów, `AddLegalAgentChunking`, konfiguracja `CHUNKING__*`, mapowanie wyjątków na kody jak `convert`) i `ProjectReference` do `LegalAgent.Chunking` w `src/LegalAgent.PdfParser.Cli/LegalAgent.PdfParser.Cli.csproj`
- [ ] T036 [P] [US3] Test (red) w `ctests/Unit/ManifestChunksTests.cs`: `ManifestDocument` ma pole `Chunks` (ścieżka względna, np. `regulaminy/REG-05.chunks.jsonl`); `ManifestWriter` zapisuje `chunks` bezpośrednio po `markdown`, `Read` je odczytuje; `schemaVersion` manifestu pozostaje 1
- [ ] T037 [US3] Implementacja (green) w `corpus-lib/Manifest/Manifest.cs` i `corpus-lib/Manifest/ManifestWriter.cs`; opis pola w `specs/003-synthetic-bank-corpus/contracts/manifest.md`
- [ ] T038 [US3] Test (red) w `ctests/Unit/CorpusChunksTests.cs`: generacja małego przebiegu do katalogu tymczasowego (jak istniejące testy generatora) zapisuje `<stem>.chunks.jsonl` obok każdego `<stem>.md` (także zatrute i akty z testowego `akty.yaml`), metadane rekordów = wpis manifestu (`id`, `designation`, `type`, `title`, `version`, `validFrom`, `validTo`, `status`, `previousVersion`), wpis ma `chunks`; `RefreshAsync` przepisuje pliki fragmentów; `VerifyAsync` zgłasza różnicę po zmianie jednej linii pliku fragmentów i po jego usunięciu; sprzątanie usuwa plik fragmentów dokumentu, którego już nie ma
- [ ] T039 [US3] Implementacja (green) w `corpus-lib/CorpusGenerator.cs`: wewnętrzna konwersja zwraca `PdfConversionResult` (Markdown i liczba stron z wyniku), po konwersji `IDocumentChunker.ChunkAsync` z metadanymi z wpisu manifestu, `CorpusFile` z `ChunkJson.ToJsonLines` w `generate`, `refresh`, `ActsAsync` i `BuildDocumentAsync`; `ProjectReference` do `LegalAgent.Chunking` w `corpus-lib/LegalAgent.Corpus.csproj`
- [ ] T040 [US3] Regeneracja danych: `dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh`, potem `verify` (kod 0) i drugi `refresh` bez zmian (`git status` czysty poza nowymi plikami); `*.md` korpusu bez zmian (SC-046); commit `data:` z `corpus/**/*.chunks.jsonl` i `corpus/manifest.json` (jawne ścieżki, bez `git add -A`)
- [ ] T041 [P] [US3] `corpus/README.md`: pliki `*.chunks.jsonl` (format → contracts/chunks-json.md, metadane z manifestu, odświeżanie przez `refresh`, sprawdzanie przez `verify`)

**Checkpoint**: US3 działa — gotowe pliki fragmentów korpusu i CLI dla dowolnego PDF.

---

## Phase 5: User Story 2 — Porównanie wersji przez klucz jednostki (Priority: P1)

**Goal**: ta sama jednostka w różnych wersjach dokumentu ma ten sam klucz; potwierdzenie na wszystkich
zmianach z manifestu i plikach wzorcowych.

**Independent Test**: test FR-273 na `corpus/manifest.json` i `corpus/**/*.chunks.jsonl`; pliki wzorcowe
fragmentów 5 dokumentów.

- [ ] T042 [P] [US2] Test w `chtests/Unit/CrossVersionKeyTests.cs` (US2 scenariusze 1–4): dwie wersje modelu z tym samym `Designation`, różnym `DocumentId` i wstawionym rozdziałem → fragmenty „§ 11” mają ten sam `UnitKey` i różne `ChunkId`; ta sama pozycja taryfy w sekcji typograficznej → klucz sekcji wspólny; dwa różne dokumenty z „§ 11” → różne klucze; części jednej jednostki → ten sam klucz, kolejne `Part`; poprawki (green), jeśli test padnie
- [ ] T043 [US2] Test w `ctests/Corpus/ChunkVersionsTests.cs` (FR-273, SC-043, R11): dla każdego wpisu manifestu z `previousVersion` i `changes` i dla każdej zmiany — w nowszej wersji co najmniej jeden fragment, którego `pages` obejmuje `change.page`, a `content` zawiera `change.after` (dla jednostek „§ N …”/„Art. N …” dodatkowo `citation` = „§ N”/„Art. N”); w poprzedniej wersji istnieje fragment o tym samym `unitKey`, którego `content` zawiera `change.before`; komunikat błędu wskazuje dokument, zmianę i klucz; jeśli test padnie na korpusie — analiza przyczyny i zadania poprawek T043a, T043b, … (poprawka parsera z czerwonym testem na syntetycznym PDF albo decyzja właściciela, plan.md „Ryzyka”)
- [ ] T044 [US2] Pliki wzorcowe w `chtests/Corpus/GoldenChunksTests.cs` (FR-271): podział PDF z `corpus/` z metadanymi z manifestu i porównanie `ToJsonLines` z `chtests/Golden/{REG-06,REG-05,TAR-04,PRO-07,dz-u-2019-1781-ochrona-danych}.chunks.jsonl` (`UPDATE_GOLDEN=1` tworzy pliki); przegląd plików wzorcowych z właścicielem przed commitem (granice, cytaty, strony) i zapis wyniku przeglądu w handoffie plan.md
- [ ] T045 [P] [US2] Test kategorii `CorpusFull` w `ctests/Corpus/CorpusChunksFullTests.cs` (SC-040 – SC-042, SC-044; `LEGALAGENT_CORPUS_FULL`): każdy wpis manifestu ma plik fragmentów zgodny z kontraktem; pokrycie słów (FR-234) względem `<stem>.md`; każdy fragment w limicie albo `exceedsLimit` z jednym atomem; `chunk.id` unikalne w całym korpusie; poprawki (green), jeśli test padnie
- [ ] T046 [P] [US2] Test wydajności (kategoria `Performance`) w `chtests/PerformanceTests.cs` (SC-045): podział największego dokumentu korpusu z gotowego wyniku konwersji < 1 s; ręczny pomiar `refresh` przed i po (≤ +20%) zapisany w handoffie

**Checkpoint**: wszystkie historyjki gotowe i sprawdzone na korpusie.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T047 [P] `README.md`: biblioteka `LegalAgent.Chunking` (rejestracja, `ChunkAsync`, opcje, kontrakt JSON), polecenie `legalagent-pdf chunk` z przykładem, `CHUNKING__*`, pliki `*.chunks.jsonl` korpusu (zasada VII)
- [ ] T048 [P] `CLAUDE.md`: projekt `LegalAgent.Chunking` w „What this is”, polecenie `chunk` w „Commands”, krótka sekcja architektury podziału (jednostki, klucz, renderowanie przez parser) i zasada „corpus chunks regenerowane przez refresh”
- [ ] T049 Sprawdź `.github/workflows/ci.yml`: nowy projekt testowy uruchamia się z solucji, `verify` obejmuje pliki fragmentów; ewentualne poprawki
- [ ] T050 Walidacja końcowa wg quickstart.md: `dotnet test LegalAgent.slnx --filter "Category!=Performance"` z `LEGALAGENT_PRIVATE_CORPUS` i `LEGALAGENT_CORPUS_FULL=1`, `verify`, scenariusze CLI; pliki wzorcowe parsera bez zmian
- [ ] T051 Handoff „Stan prac i przekazanie” w `specs/004-document-chunking/plan.md` (zrobione, pomiary SC-045, wynik przeglądu plików wzorcowych, otwarte ryzyka) i oznaczenie zadań w `tasks.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** → **US1 (3)** → **US3 (4)** → **US2 (5)** → **Polish (6)**.
- US3 potrzebuje US1 (chunker i `ChunkJson`). US2: T042 zależy tylko od fazy 2 i T015–T019 (można
  zrobić równolegle z US3); T043 i T045 wymagają zacommitowanych plików korpusu (T040); T044 — US1.

### Within Each Phase

- Para test (red) → implementacja (green) zawsze w tej kolejności, osobnymi commitami.
- T017 → T019 → T021 → T023 → T027 modyfikują ten sam `UnitSplitter.cs` — sekwencyjnie.
- T024–T025 (parser) niezależne od T016–T023; muszą być przed T026.
- T034–T035 (CLI) i T036–T039 (korpus) niezależne od siebie.
- T037–T040: między zmianą manifestu a regeneracją korpusu `verify` w CI jest czerwone — commity T037–T040
  wypychać razem (bez `push` pomiędzy).

### Parallel Opportunities

- Faza 1: T002, T003.
- Faza 2: testy T006, T008, T010 równolegle; implementacje T007, T009, T011 w różnych plikach.
- US1: T024–T025 (parser) równolegle z T016–T023; T030–T031 (JSON) równolegle z T026–T029; T032 i T033
  równolegle po T031.
- US3: T034–T035 równolegle z T036–T039; T041 w dowolnym momencie fazy.
- US2: T042 równolegle z fazą 4; T045 i T046 równolegle po T040.
- Polish: T047, T048 równolegle.

## Parallel Example: User Story 1

```text
# Po T023 (przypisy) — dwa niezależne tory:
Tor A: T024 test TableRow.Page (ptests) → T025 TableRow.Page w parserze → T026 → T027 strony
Tor B: T030 test ChunkJson → T031 ChunkJson
# Po obu: T032 determinizm, T033 pokrycie słów (równolegle)
```

## Implementation Strategy

### MVP (US1)

1. Fazy 1–2, potem faza 3 (T012–T033).
2. Stop i walidacja: serwis dzieli PDF i wynik parsera, JSON zgodny z kontraktem, determinizm, pokrycie
   słów. To już wystarcza aplikacji RAG do indeksowania pojedynczych dokumentów.

### Incremental Delivery

1. MVP (US1) → 2. US3: CLI i pliki korpusu (aplikacja demo indeksuje gotowe pliki) → 3. US2:
   potwierdzenie kluczy wersji na całym korpusie, pliki wzorcowe → 4. dokumentacja i handoff.
- Poprawki parsera wykryte na korpusie (T043, T045) dopisywane jako T043a, T043b, … z parą red/green, z testem na
  syntetycznym PDF i pełnym zestawem z `LEGALAGENT_PRIVATE_CORPUS`.
