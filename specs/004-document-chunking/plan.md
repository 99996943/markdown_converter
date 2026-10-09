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

## Stan prac i przekazanie

**Stan (2026-10-09)**: specyfikacja z doprecyzowaniami (4 pytania), plan, research R1–R12, data-model,
kontrakty i quickstart gotowe. Następny krok: `/speckit-tasks`. Kod jeszcze nie powstał; nic nie jest
zacommitowane na gałęzi `004-document-chunking`.

**Zmiany specyfikacji w trakcie planowania**: FR-232 (przypis z odwołaniami w kilku częściach jest w
każdej; przypisy bez odwołania — w ostatniej części) i FR-234 (pokrycie słów bez wiersza tytułu
dokumentu i znaczników stron pominiętych) — uzgodnienie z R1/R5.

**Po `/speckit-analyze` (2026-10-09)**: poprawki I1 (strony pominięte w zakresie), I2 (limit ≥ 200 w
FR-206/FR-221), U1 (FR-243: najkrótsza unikalna ścieżka), F1 (jedna nazwa: „oznaczenie wspólne dla
wersji” / `SeriesKey` / `document.designation`), C1 (`--allow-partial` w T034) oraz drobne C2, C6 (T014,
T016) i O1 (wspólny `push` T037–T040).

**Pliki wzorcowe fragmentów (T044, FR-271)**: REG-06, REG-05, TAR-04, PRO-07, dz-u-2019-1781 — zaakceptowane
przez właściciela 2026-10-09 (identyczne z plikami fragmentów korpusu po T045a).
