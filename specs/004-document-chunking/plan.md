# Implementation Plan: Podział dokumentów na fragmenty dla demonstracyjnej aplikacji RAG

**Branch**: `004-document-chunking` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-document-chunking/spec.md`

## Summary

Nowa biblioteka `LegalAgent.Chunking` dzieli model `LegalDocument` z wyniku parsera na fragmenty:
jedna jednostka (wstęp albo własna treść jednej sekcji) = jeden fragment, a jednostka dłuższa niż
2000 znaków — kilka części pakowanych zachłannie na granicach bloków, pozycji list i wierszy tabel
(nagłówek jednostki i nagłówek tabeli powtarzane). Treść każdej części renderuje istniejący
`MarkdownRenderer` z małego dokumentu w pamięci, bez znaczników stron (R1), więc Markdown parsera się
nie zmienia. Metadane: dokument od wywołującego, oznaczenie do cytatu i dosłowne etykiety list, ścieżka
sekcji, zakres stron (z jedynym addytywnym rozszerzeniem parsera: `TableRow.Page`), klucz jednostki
(oznaczenie wspólne dla wersji + najkrótsza unikalna ścieżka) i identyfikator fragmentu (dokument + skrót klucza +
część). Serializacja: wersjonowany kontrakt JSONL (rekord = metadane dokumentu + fragment). Nad
biblioteką: polecenie `legalagent-pdf chunk` i pliki `*.chunks.jsonl` w korpusie, zapisywane przez
`generate`/`refresh` i sprawdzane przez `verify`.

## Technical Context

**Language/Version**: C# 13 / .NET 9 (SDK z `global.json`)

**Primary Dependencies**: `LegalAgent.PdfParser` (projekt), `Microsoft.Extensions.DependencyInjection.Abstractions`
i `Microsoft.Extensions.Options` 9.0.20 (już przypięte w `Directory.Packages.props`), `System.Text.Json`
i `System.Security.Cryptography` (BCL). Brak nowych pakietów.

**Storage**: brak w bibliotece; pliki JSONL zapisuje tylko CLI i generator korpusu

**Testing**: xUnit v3 na Microsoft Testing Platform; nowy projekt `tests/LegalAgent.Chunking.Tests`;
test manifestu w `tests/LegalAgent.Corpus.Tests`; pliki wzorcowe z `UPDATE_GOLDEN=1`

**Target Platform**: Linux i Windows (CI na Linuksie), biblioteka używana w serwisie .NET

**Project Type**: biblioteka klas + rozszerzenie istniejących CLI (parser, korpus)

**Performance Goals**: podział dokumentu korpusu z gotowego wyniku < 1 s; `refresh` dłuższy o ≤ 20% (SC-045)

**Constraints**: determinizm bajtowy (kultura, wątki), zero dopisanych słów w treści, Markdown parsera
i pliki wzorcowe bez zmian, brak I/O w bibliotece

**Scale/Scope**: korpus 86 dokumentów (20–34 strony), rzędu kilku tysięcy fragmentów; pojedynczy
dokument w serwisie

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Zasada / ograniczenie | Ocena | Uwagi |
|-----------------------|-------|-------|
| I. TDD | ✅ | każda zmiana zachowania: czerwony commit z testem, potem zielony; testy offline na modelach w kodzie i zacommitowanych PDF korpusu |
| II. Wierność źródłu | ✅ | treść fragmentu tylko z renderera parsera; kontekst w metadanych; test pokrycia słów (FR-234) |
| III. Powtarzalność | ✅ | deterministyczny podział i zapis JSON; `refresh` dwukrotnie bez zmian; `verify` w CI |
| IV. Błędy zewnętrzne | ✅ | walidacja metadanych/opcji, wyjątki parsera przekazywane, kody wyjścia CLI, zapis atomowy |
| V. Bezpieczeństwo i konfiguracja | ✅ | brak sekretów; limit z opcji/konfiguracji (`CHUNKING__…`) |
| VI. Prostota, zależności | ✅ | brak nowych pakietów; jedno addytywne pole w modelu parsera |
| VII. Dokumentacja | ✅ | README (biblioteka, CLI), `corpus/README.md` (pliki fragmentów), kontrakt 003 `manifest.md` |
| Biblioteka + aplikacja | ✅ | logika w `LegalAgent.Chunking`; CLI to cienka warstwa (`legalagent-pdf chunk`) |
| Biblioteka ogólna, bez globalnego stanu | ✅ | brak zależności od korpusu/manifestu (FR-200) |
| Solucja `.slnx`, testy z solucji | ✅ | dwa nowe projekty dopisane do `LegalAgent.slnx` |
| Publiczne API udokumentowane, zmiany łamiące → MAJOR | ✅ | `TableRow.Page` addytywne (init-only, domyślnie `null`) |

Ponowna ocena po fazie 1: bez zmian — projekt (data-model, kontrakty) nie wprowadza odstępstw.

## Project Structure

### Documentation (this feature)

```text
specs/004-document-chunking/
├── spec.md
├── plan.md              # ten plik
├── research.md          # R1–R12
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── chunks-json.md   # format JSON/JSONL (schemaVersion 1)
│   ├── library-api.md   # publiczne API LegalAgent.Chunking
│   └── cli.md           # legalagent-pdf chunk + pliki korpusu
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── LegalAgent.Chunking/                      # NOWY
│   ├── LegalAgent.Chunking.csproj            # ref: LegalAgent.PdfParser; DI.Abstractions, Options
│   ├── IDocumentChunker.cs
│   ├── DocumentChunker.cs                    # walidacja, (konwersja PDF), orkiestracja
│   ├── ChunkingRequest.cs
│   ├── ChunkingServiceCollectionExtensions.cs
│   ├── Options/ChunkingOptions.cs, ChunkingOptionsValidator.cs
│   ├── Model/DocumentMetadata.cs, ChunkedDocument.cs, Chunk.cs, ChunkSource.cs, PageSpan.cs, ChunkUnitKind.cs
│   ├── Splitting/UnitCollector.cs            # R2: jednostki z drzewa sekcji
│   ├── Splitting/UnitSplitter.cs             # R3: atomy i pakowanie
│   ├── Splitting/FragmentRenderer.cs         # R1: mały LegalDocument → Markdown, wcięcie list
│   ├── Splitting/PageTracker.cs              # R4
│   ├── Splitting/FootnoteSelector.cs         # R5
│   ├── Identity/UnitKeyBuilder.cs            # R6
│   ├── Identity/ChunkIdBuilder.cs            # R7
│   └── Serialization/ChunkJson.cs            # R8
├── LegalAgent.PdfParser/
│   ├── Model/TableRow.cs                     # + int? Page { get; init; }
│   └── Stages/TableDetectionStage.cs         # ustawia TableRow.Page
├── LegalAgent.PdfParser.Cli/Program.cs       # + polecenie chunk; ref LegalAgent.Chunking
├── LegalAgent.Corpus/
│   ├── CorpusGenerator.cs                    # konwersja zwraca PdfConversionResult; pliki *.chunks.jsonl
│   └── Manifest/Manifest.cs, ManifestWriter.cs  # pole chunks
└── LegalAgent.Corpus.Cli/                    # bez zmian poleceń (generate/refresh/verify obejmują fragmenty)

tests/
├── LegalAgent.Chunking.Tests/                # NOWY
│   ├── Unit/                                 # granice, listy, tabele, przypisy, strony, klucze, id, walidacja, DI
│   ├── Serialization/                        # kontrakt JSON, round-trip
│   ├── Determinism/                          # wielokrotnie i równolegle
│   ├── Golden/                               # REG-06, REG-05, TAR-04, PRO-07, dz-u-2019-1781 (*.chunks.jsonl)
│   └── Corpus/                               # pokrycie słów na pełnym korpusie (CorpusFull)
├── LegalAgent.Corpus.Tests/Corpus/ChunkVersionsTests.cs   # FR-273 na manifest.json + corpus/**/*.chunks.jsonl
└── LegalAgent.PdfParser.Tests/                           # test TableRow.Page

corpus/**/<id>.chunks.jsonl                   # NOWE pliki danych (regenerowane, commit „data:”)
```

**Structure Decision**: osobna biblioteka obok parsera (zgodnie z opisem featury i konstytucją:
biblioteka ogólna + cienkie CLI). Polecenie `chunk` trafia do istniejącego CLI parsera zamiast nowego
programu — jedno narzędzie dla pojedynczego PDF. Generator korpusu referuje bibliotekę, by `verify`
obejmowało pliki fragmentów bez osobnego kodu porównań.

## Kolejność prac (dla /speckit-tasks)

1. Szkielet: projekty, slnx, modele, DI, walidacja (test-first).
2. US1 rdzeń: jednostki → pojedynczy fragment na jednostkę → renderowanie → metadane (cytat, ścieżka,
   klucz, id) → podział długich jednostek (akapity, listy z zagnieżdżeniem, tabele z nagłówkiem) →
   przypisy → strony (+ `TableRow.Page` w parserze, czerwony test parsera) → wejście PDF → determinizm.
3. Kontrakt JSON (`ChunkJson`) z testami.
4. US3: polecenie `chunk` w CLI; generator korpusu (`chunks` w manifeście, pliki, `verify`), regeneracja
   korpusu (commit danych), README.
5. US2: test manifestu FR-273; pliki wzorcowe (5 dokumentów); `CorpusFull` pokrycia słów; pomiar SC-045.
6. Handoff (poniżej), README, `CLAUDE.md` (polecenie `chunk`, projekt Chunking).

## Ryzyka

- **Struktura parsera** (znane błędy rozpoznania nagłówków) daje „dziwne” jednostki — akceptowane w
  specyfikacji; ewentualne poprawki tylko w parserze, osobnymi zadaniami T-xxx.
- **Klucze sekcji typograficznych** zależą od tekstu nagłówka — zmiana tytułu sekcji między wersjami
  rozdziela klucze. Test FR-273 pokaże, czy korpus to trafia; wtedy decyzja właściciela.
- **`TableRow.Page`** w tabelach budowanych przez kilka stron (taryfy bez siatki, tabela-dokument) —
  jeśli strona wiersza nie jest dostępna w miejscu budowy, zostaje `null` i zakres całej tabeli.

## Complexity Tracking

Brak naruszeń konstytucji.

## Stan prac i przekazanie (T051, 2026-10-09)

**Zrobione** — wszystkie zadania T001–T051 oraz poprawki T033a, T043a, T045a (`tasks.md`), gałąź
`004-document-chunking` (niewypchnięta; PR do zrobienia):

- Biblioteka `LegalAgent.Chunking` (`AddLegalAgentChunking`, `IDocumentChunker.ChunkAsync` z wyniku parsera albo
  strumienia PDF, `ChunkingOptions`/`ChunkingRequest`, `ChunkJson` — kontrakt JSONL `schemaVersion` 1) i jej testy
  `tests/LegalAgent.Chunking.Tests` (jednostkowe, kontrakt, determinizm, 5 plików wzorcowych, `CorpusFull`,
  `Performance`).
- Parser: addytywne `TableRow.Page` (strona początku wiersza) z `TableDetectionStage`; Markdown i pliki wzorcowe
  parsera bez zmian (także prywatny korpus właściciela).
- CLI: `legalagent-pdf chunk` (metadane z opcji, `CHUNKING__*`, kody wyjścia jak `convert`).
- Korpus: `<id>.chunks.jsonl` obok każdego Markdown (86 plików), pole `chunks` w manifeście, `generate`/`refresh`
  zapisują, `verify` porównuje, sprzątanie obejmuje `*.jsonl`; `.gitattributes` trzyma `*.jsonl` w LF.
- README, `corpus/README.md`, `CLAUDE.md`, kontrakty spec 003 (`manifest.md`: `chunks`; `content-format.md`:
  `nazwa-en`).

**Walidacja (T050)**: `dotnet test --filter "Category!=Performance"` z `LEGALAGENT_PRIVATE_CORPUS` i
`LEGALAGENT_CORPUS_FULL=1` — 1483/1483; `Category=Performance` — 4/4; `verify` — kod 0; scenariusze quickstart
(CLI `chunk`, błędna data → 2, klucz `BP/REG/06 | § 30` w REG-06 i REG-06-w2) — zgodne. CI bez zmian (nowy projekt w
`.slnx`, `verify` obejmuje pliki fragmentów).

**Pomiary (SC-045, T046)**: konwersja całego korpusu 23,4 s, podział 0,79 s (3,4%); najwolniejszy dokument
(dz-u-2024-1646, 645 fragmentów) 103 ms. Pliki fragmentów korpusu ≈ 12 MB (metadane dokumentu w każdej linii).

**Pliki wzorcowe fragmentów (T044, FR-271)**: REG-06, REG-05, TAR-04, PRO-07, dz-u-2019-1781 — zaakceptowane przez
właściciela 2026-10-09; później zmienione na jego polecenie (angielskie `type`/`status`, `~preamble`).

**Decyzje i doprecyzowania w trakcie implementacji** (zapisane w spec.md → Clarifications):

- FR-222: część zaczynająca się od pozycji zagnieżdżonej listy jest bez wcięcia (wcięcie 4 spacji = blok kodu w
  CommonMark); etykiety pozycji (`listLabels`) mają tylko części od 2. wzwyż.
- FR-232 (T033a): przypisy bez odwołania są atomami na końcu jednostki — prawo bankowe, Art. 4 dawało część 3796 zn.
- FR-220/FR-234 (T045a): sekcja bez treści i bez podsekcji = fragment z samym nagłówkiem (tytuł ustawy obok
  rozdziałów ginął); nagłówki sekcji bez własnej treści są pokryte przez `sectionPath`.
- Spec 003 FR-120 (T043a, decyzja właściciela): wersje dokumentu mają te same bloki i numerację paragrafów co
  najnowsza (`DocumentPlan.SeriesId`, liczba bloków opcjonalnych z najnowszej) — wcześniej przenumerowanie rozdzielało
  klucze 10 z 64 zmian; regeneracja dotknęła tylko 13 wcześniejszych wersji.
- Decyzja właściciela: wartości metadanych dla modelu po angielsku — `type` (`regulation`/`tariff`/`procedure`/
  `act`, z `nazwa-en` w `typy.yaml`), `status` (`in-force`/`outdated`), segment `~preamble`; manifest bez zmian.

**Otwarte / do wiadomości**:

- Strona zmiany w manifeście (spec 003) to dla pozycji taryfy pierwsza strona tabeli, nie wiersza — test FR-273
  rozpoznaje pozycje i kroki po etykiecie. Poprawka wymagałaby stron wierszy w składzie generatora.
- Dokumenty zatrute mają oznaczenie dokumentu, który udają, więc ich klucze jednostek pokrywają się z oryginałem
  (odróżnia je `document.id`) — świadomie, opisane w `corpus/README.md`.
- Klucze sekcji bez oznaczenia (taryfy, procedury) pochodzą z tekstu nagłówka — zmiana tytułu sekcji między wersjami
  rozdzieli klucze (w korpusie nie występuje).
- Commity T037–T040 i dane korpusu wypchnąć razem (CI `verify`).
