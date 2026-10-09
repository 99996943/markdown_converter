# Implementation Plan: Syntetyczny korpus dokumentów fikcyjnego banku dla aplikacji RAG

**Branch**: `003-synthetic-bank-corpus` (utworzona z `002-table-document-sections`) | **Date**: 2026-10-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/003-synthetic-bank-corpus/spec.md`

## Summary

Repozytorium dostaje generator korpusu fikcyjnego „Bank Przykładowy S.A.”: 10 regulaminów, 10 taryf i
10 procedur (20–30 stron, po polsku) z wersjami, dokumentami nieaktualnymi i parami sprzecznymi, 30
dokumentów zatrutych w `corpus/zatrute/<typ>/<rodzaj>/`, 10 aktów prawnych, Markdown z biblioteki i
manifest z prawdą referencyjną dla aplikacji RAG.

Podejście (research R1–R14): nowa biblioteka **`LegalAgent.Corpus`** (logika) + cienkie CLI
**`LegalAgent.Corpus.Cli`** (`generate` / `refresh` / `verify`) + projekt testów. Treść w plikach YAML w
`corpus/zrodla/` (szablony typów, bloki z wariantami `{a|b}` i odwołaniami do **faktów banku**, wzorce
zatruć). Jedno źródło wartości (fakty) daje spójność dokumentów, a wersje, sprzeczności i fałszywe
stawki to nadpisania faktów — manifest wylicza je automatycznie. Planowanie z własnym PRNG (SplitMix64)
i z góry podzielonymi pulami bloków (dokumenty niezależne). Skład stron: uogólniony `Flow` z
`BankingCorpusGenerator` na przeniesionym do biblioteki `SyntheticPdfBuilder`, z deterministycznym
`/ID` PDF. Każdy PDF konwertowany biblioteką parsera; metryki jakości liczone względem prawdy
referencyjnej z generatora — próbka w każdym `dotnet test`, pełny korpus w CI. Układy źle obsłużone
przez bibliotekę poprawiane test-first (kandydaci w R11).

## Technical Context

**Language/Version**: C# 13 / .NET 9 (`net9.0`), SDK z `global.json` — bez zmian

**Primary Dependencies**: `PdfPig` 0.1.16 (skład PDF, jak dotąd w testach), biblioteka
`LegalAgent.PdfParser` (konwersja), **nowa: `YamlDotNet` 16.3.0** (pliki treści, R2);
`Microsoft.Extensions.*` 9.0.20 bez zmian. Czcionki Noto Sans (OFL) przeniesione jako zasoby osadzone.

**Storage**: pliki w repozytorium: `corpus/` (PDF, Markdown, `manifest.json`, `przebieg.json`,
`zrodla/*.yaml`)

**Testing**: xUnit v3; nowy projekt `tests/LegalAgent.Corpus.Tests` (jednostkowe generatora, próbka
korpusu, `Category=CorpusFull` przy `LEGALAGENT_CORPUS_FULL=1`); testy parsera z nowymi przypadkami R11;
istniejące goldeny jako test regresji (SC-029)

**Target Platform**: Linux (CI ubuntu-latest) i Windows; bajtowo identyczny wynik na obu

**Project Type**: biblioteka klas + cienka aplikacja konsolowa (drugi taki zestaw w solucji) + dane
(korpus)

**Performance Goals**: pełne odtworzenie korpusu (≈ 75 dokumentów + wersje, ≈ 2 000 stron) < 10 min
(SC-021), praktyczny cel < 3 min (składanie równoległe po dokumentach, konwersja ≈ 0,1 s/stronę)

**Constraints**: FR-101 determinizm bajtowy (brak zegara, kultura niezmienna, własny PRNG, `/ID`);
brak sieci w generatorze i testach; brak nazw prawdziwych banków; Markdown korpusu = bieżący wynik
biblioteki; zero słów dopisanych przez bibliotekę; goldeny parsera bez niezatwierdzonych zmian

**Scale/Scope**: 2 nowe projekty src + 1 testowy; ≈ 4–5 tys. linii kodu generatora; treść źródłowa
rzędu 60–90 tys. słów z wariantami (największy nakład pracy — partiami per typ); 30 dokumentów
bazowych + ≈ 6 wersji wcześniejszych + 30 zatrutych + 10 aktów; FR-100 – FR-165, SC-020 – SC-031

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Zasada / ograniczenie | Ocena | Jak spełnione |
|-----------------------|-------|---------------|
| I. Test-First / TDD | ✅ | Każdy komponent generatora (PRNG, warianty, fakty, planowanie, skład, manifest) i każda poprawka parsera (R11) zaczyna od czerwonego testu; osobne commity red/green. Testy offline i deterministyczne (akty commitowane, korpus odtwarzany w pamięci). |
| II. Wierność źródłu | ✅ | Biblioteka przenosi tekst dosłownie, łącznie z zatruciami (FR-162, SC-023); prawda referencyjna i manifest wyliczane z tego samego planu, co PDF — żadnych ręcznych twierdzeń o treści. Akty z oficjalnych źródeł ze wskazaniem URL. |
| III. Powtarzalność | ✅ | Jedno polecenie (`generate`); identyczne bajty przy tym samym ziarnie (R3, R4); ponowne uruchomienie nadpisuje i sprząta bez duplikatów (R14); `verify` + test pełny w CI wykrywa rozjazd. |
| IV. Odporność na błędy zewnętrzne | ✅ | Jedyne wywołanie sieci — ręczne, jednorazowe `curl --max-time` (README); zapis atomowy, kody wyjścia 1–6 z czytelnym komunikatem, brak częściowego korpusu ([cli.md](./contracts/cli.md)). |
| V. Bezpieczeństwo i konfiguracja | ✅ | Brak sekretów; parametry przebiegu w `corpus/przebieg.json` (konfiguracja, nie kod); źródła aktów w `zrodla/akty.yaml`. Dane w zatruciach fikcyjne (FR-105, domeny przykładowe). |
| VI. Prostota i minimalne zależności | ⚠️ uzasadnione | Nowa zależność `YamlDotNet` (przypięta) i dwa nowe projekty — patrz Complexity Tracking. Skład stron reużywa `Flow`/`SyntheticPdfBuilder`, bez zewnętrznego silnika. |
| VII. Dokumentacja | ✅ | `corpus/README.md` (regeneracja, dodawanie dokumentów, liczba stron, nowy typ/szablon/rodzaj zatrucia, odświeżenie Markdown i manifestu, pobranie aktów); README główne — sekcja o korpusie i nowym CLI. |
| C# / .NET 9, Linux, bez API Windows | ✅ | Czcionki jako zasoby osadzone (bez ścieżek systemowych), `Path.Combine`, porządek ordinal, `\n`. |
| Biblioteka + cienka aplikacja | ✅ | Logika w `LegalAgent.Corpus`; CLI tylko argumenty, parametry, kody wyjścia. |
| Biblioteka parsera ogólna, bez zaszytych źródeł | ✅ | Parser nie zależy od generatora; poprawki R11 ogólne (geometria/typografia), bez reguł „Bank Przykładowy”. |
| API jawne, SemVer | ✅ | Parser: poprawki wewnętrzne, bez zmian API (ew. zmiany addytywne → MINOR). `LegalAgent.Corpus`: nowe API 1.0.0, XML-doc. |
| `.slnx`, osobny projekt testowy | ✅ | Projekty dodane do `LegalAgent.slnx`; `LegalAgent.Corpus.Tests` dla nowej biblioteki. |
| Format OKF | N/A | Nie dotyczy (manifest nie jest FAQ). |

**Wynik bramki (przed Phase 0)**: PASS z uzasadnionymi odstępstwami (Complexity Tracking).

**Re-check po Phase 1**: PASS — model danych i kontrakty nie wprowadzają kolejnych zależności ani
projektów; zmiana w testach parsera ogranicza się do odwołania do przeniesionego `SyntheticPdfBuilder`
(goldeny bez zmian, bo PDF różni się wyłącznie `/ID`, którego parser nie czyta).

## Project Structure

### Documentation (this feature)

```text
specs/003-synthetic-bank-corpus/
├── plan.md              # Ten plik
├── research.md          # Phase 0: pomiary PdfPig/czcionek, decyzje R1–R14, źródła aktów
├── data-model.md        # Phase 1: treść źródłowa, przebieg i plan, wynik składu, manifest
├── quickstart.md        # Phase 1: walidacja end-to-end
├── contracts/
│   ├── cli.md           # polecenia, opcje, kody wyjścia
│   ├── content-format.md # format YAML treści, wariantów, faktów, zatruć
│   ├── manifest.md      # schemat manifest.json
│   └── corpus-layout.md # katalogi, nazwy plików, pliki zarządzane
├── checklists/requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
src/LegalAgent.Corpus/                       # NOWA biblioteka
├── LegalAgent.Corpus.csproj                 # PdfPig, YamlDotNet, → LegalAgent.PdfParser; Fonts/* jako EmbeddedResource
├── Fonts/                                   # Noto Sans Regular/Bold/Italic/Mono + OFL.txt (przeniesione z testów)
├── Pdf/
│   ├── SyntheticPdfBuilder.cs               # przeniesiony z testów (public), czcionki z zasobów
│   └── PdfIdNormalizer.cs                   # R4
├── Random/DeterministicRandom.cs            # SplitMix64 + FNV-1a (R3)
├── Content/                                 # wczytanie i walidacja zrodla/*.yaml (ContentLibrary, DocumentTypeDef, Fact, DocumentTemplate, ContentBlock, PoisonPattern, ActSource)
│   ├── ContentLoader.cs
│   └── TextTemplate.cs                      # {a|b}, {{fakt:…}}, {{ref:…}}, {{param:…}}, **…**, [^n]
├── Planning/
│   ├── RunParameters.cs                     # + odczyt/zapis przebieg.json
│   ├── CorpusPlanner.cs                     # przydział pul, wersje, nieaktualne, sprzeczności, zatrucia
│   └── DocumentPlan.cs
├── Composition/
│   ├── DocumentComposer.cs                  # plan → drzewo elementów; numeracja jednostek; operacje zatruć
│   └── Elements.cs
├── Typesetting/
│   ├── Typesetter.cs                        # uogólniony Flow: łamanie, listy, przypisy, kolumny, tabele, schemat, metryczka
│   ├── LayoutStyles.cs                      # jedna-kolumna, dwie-kolumny, tabela-dokument, taryfa-siatka, taryfa-bez-siatki, procedura
│   └── PageFitter.cs                        # trafienie w zakres stron (R6)
├── Truth/DocumentTruth.cs                   # prawda referencyjna (FR-106)
├── Manifest/ManifestWriter.cs               # manifest.json (contracts/manifest.md)
├── Output/CorpusWriter.cs                   # zapis atomowy, sprzątanie (R14), verify
├── Conversion/MarkdownRefresher.cs          # konwersja biblioteką (R13)
├── Validation/CorpusChecks.cs               # nazwy zabronione, unikalność, udział wspólnych, odwołania
└── CorpusGenerator.cs                       # fasada: Generate / Refresh / Verify

src/LegalAgent.Corpus.Cli/                   # NOWA cienka aplikacja
├── LegalAgent.Corpus.Cli.csproj
└── Program.cs                               # argumenty (contracts/cli.md), kody wyjścia

tests/LegalAgent.Corpus.Tests/               # NOWY projekt testowy
├── Unit/                                    # Random, TextTemplate, ContentLoader, Planner, Composer, Typesetter, PageFitter, Manifest, Writer, Checks, PdfIdNormalizer
├── Cli/ProgramTests.cs                      # opcje, kody wyjścia (katalogi tymczasowe)
├── Corpus/CorpusSampleTests.cs              # próbka: PDF == plik, Markdown == plik, SC-022 – SC-026
├── Corpus/CorpusFullTests.cs                # Category=CorpusFull: wszystkie dokumenty + SC-020, SC-027, SC-028, SC-031
└── Corpus/QualityMetrics.cs                 # liczenie metryk względem DocumentTruth

tests/LegalAgent.PdfParser.Tests/
├── Fixtures/SyntheticPdfBuilder.cs          # USUNIĘTY (→ LegalAgent.Corpus); Fonts/ usunięte
├── LegalAgent.PdfParser.Tests.csproj        # + ProjectReference LegalAgent.Corpus
└── Unit|Integration/…                       # czerwone przypadki R11 przed poprawkami parsera

src/LegalAgent.PdfParser/…                   # poprawki R11 (tylko jeśli pomiar korpusu je potwierdzi)

corpus/                                      # NOWY katalog danych — contracts/corpus-layout.md
├── README.md, przebieg.json, manifest.json
├── zrodla/ (typy, fakty, szablony, bloki, zatrucia, zabronione, akty)
├── akty/ (10 PDF + MD + ZRODLA.md)
├── regulaminy/ taryfy/ procedury/
└── zatrute/<typ>/<rodzaj>/

.github/workflows/ci.yml                     # + krok: LEGALAGENT_CORPUS_FULL=1, filter Category=CorpusFull
.gitattributes                               # corpus/**/*.md eol=lf, *.pdf binary
LegalAgent.slnx, Directory.Packages.props    # nowe projekty, YamlDotNet
README.md                                    # sekcja: korpus i LegalAgent.Corpus.Cli
```

**Structure Decision**: druga para biblioteka + CLI obok parsera, bo generator jest osobnym narzędziem
z własnym cyklem życia, a konstytucja wymaga logiki w bibliotece i cienkiej aplikacji. Parser nie
zależy od generatora; generator i testy parsera zależą od `LegalAgent.Corpus` tylko w zakresie
konstruktora PDF (testy) i od parsera (generator). Kolejność prac (dla `/speckit-tasks`):
(1) przeniesienie `SyntheticPdfBuilder` + `/ID` bez zmian goldenów; (2) rdzeń generatora na małej
treści testowej (PRNG → treść → plan → skład → prawda → manifest → zapis → CLI); (3) treść
regulaminów → pomiar → poprawki R11; (4) taryfy; (5) procedury; (6) wersje/sprzeczności;
(7) zatrucia; (8) akty; (9) pełny przebieg, CI, README.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Zależność `YamlDotNet` 16.3.0 | Setki stron polskiej prozy w plikach źródłowych muszą być czytelne i edytowalne bez kodu (FR-102); YAML ma bloki wieloliniowe bez escapowania | JSON (wbudowany) wymaga escapowania i jednej linii na akapit — nieczytelny w tej skali; własny format = parser i gramatyka do utrzymania (więcej kodu niż zależność) |
| Dwa nowe projekty w `src/` (+1 testowy) | Generator musi być uruchamiany jednym poleceniem i być wielokrotnego użytku (FR-100), a konstytucja wymaga logiki w bibliotece + cienkiej aplikacji + osobnego projektu testowego | Polecenie w CLI parsera — CLI parsera zależałoby od generatora i kodu testowego; generator w projekcie testowym — brak „jednego polecenia” i kontraktu parametrów; osobny projekt tylko na `SyntheticPdfBuilder` — dodatkowy projekt bez samodzielnej wartości |
| Commitowany korpus binarny (≈ 10 MB PDF) | Spec wymaga korpusu w repozytorium dla aplikacji RAG | Generowanie przy użyciu — aplikacja RAG jest osobna i nie powinna budować generatora; Git LFS — dodatkowe narzędzie przy małym rozmiarze |

## Stan prac i przekazanie (T126, 2026-10-09)

**Zrobione** — wszystkie zadania T001–T126 (`tasks.md`), gałąź `003-synthetic-bank-corpus`:

- Generator `LegalAgent.Corpus` + CLI `LegalAgent.Corpus.Cli` (`generate`, `refresh`, `verify`, `check`,
  wszystkie opcje z contracts/cli.md), treść w `corpus/zrodla/` (YAML), instrukcja `corpus/README.md`.
- Korpus w repozytorium (`corpus/przebieg.json`, ziarno 20261008): 83 wpisy manifestu — 30 dokumentów
  bazowych (po 10 regulaminów, taryf, procedur, 6 układów), 13 wcześniejszych wersji (3 dokumenty na
  typ w 2–3 wersjach), po 2 nieaktualne na typ, 4 pary sprzeczne (po 1 na typ + regulamin–taryfa),
  30 zatrutych (5 rodzajów × 3 typy × 2) i 10 aktów prawnych (6 z testów parsera, 4 pobrane z
  Dziennika Ustaw wg R12). Rozmiar: ≈ 13 MB PDF syntetycznych + 20 MB aktów.
- Testy: próbka (`CorpusSampleTests`, w każdym przebiegu) i pełny korpus (`CorpusFullTests`,
  `LEGALAGENT_CORPUS_FULL=1`, krok CI „Corpus full”), `verify` w CI, SC-021 jako test wydajności.

**Metryki** (pomiar w research.md „Pomiar korpusu (T076)”): wszystkie 73 dokumenty syntetyczne i
zatrute spełniają SC-022 – SC-026 (0 słów spoza PDF, ≥ 99,8% słów, nagłówki ≥ 98%, fałszywe ≤ 1%,
listy ≥ 98%, 100% stawek w wierszu i taryf jako jednej tabeli GFM); SC-020, SC-023 (100% tekstów
zatruć dosłownie w Markdown), SC-027, SC-028, SC-031 — zielone; `generate` całego korpusu ≈ 1,5 min.

**Poprawki biblioteki** (R11/T089, test-first w `CorpusLayoutsIntegrationTests`): taryfy bez siatki
(T083–T083j), tabela z siatką pod akapitami (T089a), tytuł rozdziału w dwóch wierszach (T089b),
dwie kolumny (T087–T087j: łączenie linii, interlinia w kolumnie, rynna, tabele w kolumnie, listy
przez kolumny i strony, znaczniki przypisów), kod formularza w wersalikach (T089d), fragment tabeli
z siatką (T089e), postrzępiona lewa kolumna (T089f–T089i), „§ 99.” w zawiniętym zdaniu (T086).
Goldeny parsera i prywatny korpus właściciela bez zmian, z wyjątkiem T089b.

**Otwarte decyzje dla właściciela**

1. **Goldeny T089b** (FR-163): dwa goldeny aktów zmienione — tytuł rozdziału w dwóch wierszach jest
   teraz jednym nagłówkiem (`dz-u-2020-287` Rozdział 2, `dz-u-2024-1646` Rozdział 2b); commit
   `fix: a chapter title wrapped over more lines continues the heading (T089b)` — do akceptacji lub
   cofnięcia.
2. **Obwieszczenie o ochronie danych** (`corpus/akty/dz-u-2019-1781-ochrona-danych.md`): cytowane we
   wstępie art. 109–157 (nieobjęte tekstem jednolitym) stają się nagłówkami „## Art. 110.” przed
   właściwą ustawą; tekst kompletny, struktura myląca — kandydat na osobną poprawkę parsera.
3. **Regulaminy „tabela-dokument”** (REG-01, REG-05): ich wewnętrzne tabele nie są tabelami GFM
   (układ zapisywany wg FR-080 jako sekcje); metryka tabel ich nie wymaga — do potwierdzenia.
4. **T097**: zmiany między wersjami realizują nadpisania faktów (historia `wartosci` i `alternatywy`);
   warianty bloków „po zmianie” nie zostały zaimplementowane.
5. **README korpusu** (T117) nie był sprawdzony przejściem „czysty klon” (SC-030).
