# Implementation Plan: LegalAgent.PdfParser — konwersja PDF aktów prawnych i regulaminów do Markdown

**Branch**: `001-legal-pdf-parser` (katalog funkcjonalności; praca na `main`) | **Date**: 2026-10-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-legal-pdf-parser/spec.md`

## Summary

Biblioteka klas .NET `LegalAgent.PdfParser` przyjmuje strumień PDF i zwraca ustrukturyzowany model
dokumentu (sekcje z numeracją jednostek redakcyjnych, akapity, listy, tabele, przypisy, zakresy
stron) oraz jego deterministyczny rendering do Markdown (CommonMark + tabele/przypisy GFM, znaczniki
`<!-- page: N -->`). Tekst jest pozyskiwany z PdfPig na poziomie pojedynczych liter (pozycja,
rozmiar efektywny, pogrubienie), a następnie przetwarzany przez uporządkowany, wymienny przez DI
potok 11 etapów: normalizacja Unicode → składanie linii po osi Y → usuwanie artefaktów stron
(odciski linii w strefach marginesu, wzorce numerów stron) → przypisy → tabele (pasy kolumn,
linie siatki, scalanie wierszy wieloliniowych i przez strony) → kolejność czytania → listy (wcięcia
+ hierarchia oznaczeń prawnych) → nagłówki (klasy rozmiarów czcionki + wzorce Dział/Rozdział/Art./§)
→ akapity → drzewo sekcji. Limity zasobów, tryb wyniku częściowego i raport diagnostyczny
zapewniają, że niepełny wynik nigdy nie jest cichym sukcesem. Solucja `.slnx` zawiera bibliotekę,
cienkie CLI i projekt testowy; wszystko budowane i testowane na Linuxie.

## Technical Context

**Language/Version**: C# 13 / .NET 9 (`net9.0`); budowanie SDK 10.0.x przypiętym w `global.json` (research R1)

**Primary Dependencies**:
- Biblioteka: `PdfPig` 0.1.16 (Apache-2.0), `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.20, `Microsoft.Extensions.Options` 9.0.20
- CLI: `Microsoft.Extensions.DependencyInjection` 9.0.20, `Microsoft.Extensions.Configuration.EnvironmentVariables` 9.0.20, `Microsoft.Extensions.Configuration.Binder` 9.0.20
- Testy: `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` (najnowsze stabilne, przypięte w `Directory.Packages.props`); czcionki Noto Sans (OFL) jako dane testowe

**Storage**: N/A (biblioteka bez stanu; CLI zapisuje plik wynikowy atomowo)

**Testing**: xUnit v3; syntetyczne PDF generowane w testach (`PdfDocumentBuilder`), korpus golden files, testy metryk jakości SC, testy determinizmu, testy wydajności (kategoria `Performance`)

**Target Platform**: Linux (CI ubuntu-latest) i Windows; wymagane ICU na Linuxie (R7)

**Project Type**: biblioteka klas + cienka aplikacja konsolowa

**Performance Goals**: 100 stron < 10 s (SC-007); pamięć liniowa względem liczby stron

**Constraints**: offline, deterministyczny wynik bajt-w-bajt (poza `Report.Elapsed`), limity domyślne 100 MB / 2000 stron / 120 s, brak API specyficznych dla Windows, brak reguł specyficznych dla wydawcy

**Scale/Scope**: dokumenty 1–2000 stron; ~11 etapów potoku, ~70 wymagań FR; korpus ≥ 4 akty + ≥ 4 dokumenty bankowe (syntetyczne)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Zasada / ograniczenie | Ocena | Jak spełnione |
|-----------------------|-------|---------------|
| I. Test-First / TDD | ✅ | Każdy FR ma test na syntetycznym PDF pisany przed etapem; testy offline i deterministyczne; małe, niezależne etapy (`IPipelineStage`). `tasks.md` uporządkuje test → kod. |
| II. Wierność źródłu | ✅ | Brak treści spoza PDF; `SourceInfo` (SourceId, SHA-256), zakresy stron w każdej sekcji/bloku, znaczniki stron; strony pominięte jawnie oznaczone; nagłówki nie są zgadywane (US2-5). |
| III. Powtarzalność | ✅ | R13 (brak równoległości, jawne sortowania, LF, kultura niezmienna); test determinizmu; CLI zapisuje atomowo; jedno polecenie `dotnet test` / `convert`. |
| IV. Odporność na błędy | ✅ | Limity rozmiaru/stron/czasu (FR-009b), dedykowane wyjątki, flaga `IsComplete`, kody wyjścia CLI ≠ 0. Brak wywołań sieciowych. |
| V. Bezpieczeństwo i konfiguracja | ✅ | Brak sekretów; progi w opcjach, CLI czyta nadpisania ze zmiennych środowiskowych; brak zaszytych źródeł. |
| VI. Prostota / zależności | ✅ (uzasadnione) | Biblioteka: 3 zależności (PdfPig — wymóg zleceniodawcy; DI.Abstractions i Options — wymóg DI). CLI: 3 pakiety Microsoft.Extensions (DI, zmienne środowiskowe, binder — zasada V). Bez Verify, FluentAssertions, System.CommandLine, Markdig. Wersje przypięte centralnie. |
| VII. Dokumentacja | ✅ | README: cel, instalacja (w tym runtime 9.0 i ICU), konfiguracja opcji, użycie biblioteki i CLI, testy. XML-doc publicznego API. |
| Platforma: C# .NET 9, Linux | ⚠️ ryzyko | Spełnione (`net9.0`, CI Linux). **Ryzyko**: koniec wsparcia .NET 9 — 10.11.2026 (R1). Zalecana poprawka konstytucji na .NET 10 LTS — decyzja właściciela; plan pozwala na zmianę jedną edycją `Directory.Build.props` i `Directory.Packages.props`. Lokalnie brak runtime 9.0 GA — do doinstalowania. |
| Biblioteka + aplikacja, biblioteka ogólna | ✅ | `LegalAgent.PdfParser` (cała logika) + `LegalAgent.PdfParser.Cli` (argumenty, env, kody wyjścia). Biblioteka nie zależy od konsoli/globalnego stanu. |
| Model strukturalny → Markdown, chunking poza zakresem | ✅ | `LegalDocument` + `IMarkdownRenderer`; `Section.Path` ułatwia chunking w innym projekcie. |
| API jawne, SemVer | ✅ | [contracts/public-api.md](./contracts/public-api.md) v1.0.0; XML-doc; `PublicApiAnalyzers` — nie (YAGNI), zmiany kontrolowane przeglądem kontraktu. |
| Solucja `.slnx`, wszystkie projekty, build/test na Linuxie | ✅ | `LegalAgent.slnx` z 3 projektami; CI ubuntu. |
| Osobny projekt testowy | ✅ | `tests/LegalAgent.PdfParser.Tests`. |
| Format OKF (FAQ) | N/A | Ta funkcjonalność nie generuje FAQ. |

**Wynik bramki (przed Phase 0)**: PASS z jednym ryzykiem platformowym zgłoszonym do decyzji
(nie jest naruszeniem — projekt spełnia literę konstytucji).

**Re-check po Phase 1**: PASS — kontrakty i model danych nie wprowadziły nowych zależności ani
projektów; rendering jako osobna usługa (nie etap) upraszcza API; brak naruszeń do uzasadnienia.

## Project Structure

### Documentation (this feature)

```text
specs/001-legal-pdf-parser/
├── plan.md              # Ten plik
├── research.md          # Phase 0: decyzje R1–R16
├── data-model.md        # Phase 1: model publiczny, opcje, model układu, kolejność etapów
├── quickstart.md        # Phase 1: przewodnik walidacji
├── contracts/
│   ├── public-api.md    # API biblioteki (DI, fasada, etapy, wyjątki, gwarancje)
│   ├── markdown-output.md # format wyniku i niezmienniki
│   └── cli.md           # składnia CLI i kody wyjścia
├── checklists/requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
LegalAgent.slnx
global.json
Directory.Build.props          # net9.0, Nullable, TreatWarningsAsErrors, Deterministic, analizatory kultury CA1304/CA1305/CA1307/CA1309/CA1310 jako błędy
Directory.Packages.props       # centralne, przypięte wersje pakietów
README.md
.github/workflows/ci.yml       # ubuntu-latest: build + test

src/
├── LegalAgent.PdfParser/
│   ├── PdfMarkdownConverter.cs            # fasada IPdfMarkdownConverter (+ CreateDefault)
│   ├── PdfParserServiceCollectionExtensions.cs
│   ├── Exceptions/                        # PdfParserException i pochodne
│   ├── Options/                           # PdfParserOptions + grupy + walidator
│   ├── Model/                             # LegalDocument, Section, bloki, inliny, raport (publiczne)
│   ├── Input/                             # buforowanie strumienia z limitem, otwieranie PDF, SHA-256
│   ├── Pipeline/                          # IPipelineStage, PipelineContext, uruchamianie etapów
│   ├── Layout/                            # LayoutPage/Line/Word/Glyph, geometria, klasteryzacja
│   ├── Stages/
│   │   ├── PageExtractionStage.cs
│   │   ├── TextNormalizationStage.cs
│   │   ├── LineAssemblyStage.cs
│   │   ├── ArtifactRemovalStage.cs
│   │   ├── FootnoteDetectionStage.cs
│   │   ├── TableDetectionStage.cs
│   │   ├── ReadingOrderStage.cs
│   │   ├── ListDetectionStage.cs
│   │   ├── HeadingDetectionStage.cs
│   │   ├── BlockAssemblyStage.cs
│   │   └── DocumentBuildStage.cs
│   ├── Text/                              # ligatury, dzielenie wyrazów, Levenshtein, wzorce PL (regex)
│   └── Rendering/                         # MarkdownRenderer, ucieczka znaków
└── LegalAgent.PdfParser.Cli/
    └── Program.cs                         # argumenty, env → opcje, zapis atomowy, kody wyjścia

tests/
└── LegalAgent.PdfParser.Tests/
    ├── Fixtures/          # SyntheticPdfBuilder (PdfDocumentBuilder + Noto Sans), Fonts/ (OFL)
    ├── Unit/              # testy per etap / per FR
    ├── Rendering/         # niezmienniki Markdown, ucieczka
    ├── Integration/       # DI, anulowanie, błędy, limity, CLI (proces)
    ├── Corpus/
    │   ├── acts/          # ustawy z ISAP (*.pdf + *.expected.md)
    │   ├── banking/       # syntetyczne regulaminy/taryfy (*.pdf generowane skryptem testowym + *.expected.md)
    │   ├── errors/        # zaszyfrowany, uszkodzona strona, nie-PDF, skan
    │   ├── GoldenTests.cs
    │   └── QualityMetricsTests.cs
    ├── DeterminismTests.cs
    └── PerformanceTests.cs
```

**Structure Decision**: Jedna solucja `LegalAgent.slnx` z trzema projektami wymaganymi przez
konstytucję (biblioteka, CLI, testy). Logika podzielona na etapy potoku w osobnych plikach, by każdy
FR był testowalny niezależnie (TDD). Model publiczny oddzielony od roboczego modelu układu, aby
zmiany heurystyk nie łamały kontraktu SemVer.

## Kolejność realizacji (wskazówka dla /speckit-tasks)

1. Szkielet solucji, `Directory.*.props`, CI, `SyntheticPdfBuilder` (fundament TDD).
2. US1 + US5 (P1): wejście/limity/wyjątki, ekstrakcja, normalizacja, linie, artefakty, akapity,
   rendering podstawowy, DI, anulowanie, determinizm → MVP.
3. US2 (P1): nagłówki typograficzne + jednostki redakcyjne, drzewo sekcji, `Path`, przypisy.
4. US3 (P2): listy. US4 (P2): tabele. (niezależne — mogą iść równolegle)
5. US6 (P3): raport, znaczniki pominięć, postęp.
6. Korpus golden + metryki SC + test wydajności; README.

## Uwagi do implementacji (2026-10-07)

- **Runtime**: .NET 9.0.20 zainstalowany lokalnie — ryzyko „brak runtime 9.0 GA” zamknięte; pozostajemy przy `net9.0` (bez migracji na .NET 10 w tej funkcjonalności).
- **U3 (z /speckit-analyze)**: przy T037 podjąć i zapisać w research.md R4 decyzję o `ParsingOptions.UseLenientParsing` (rekomendacja: `true` + jawne wykrywanie błędów odczytu stron, by FR-009/FR-009a były rozróżnialne); przy T082 opisać w `Corpus/errors/README.md`, jak powstaje `broken-page.pdf` i potwierdzić testem, że PdfPig faktycznie zgłasza błąd dla strony 2.
- **U4 (z /speckit-analyze)**: przed T093 przygotować niezależne dane referencyjne dla SC-003/SC-004 — `Corpus/acts/<nazwa>.structure.txt` (lista oznaczeń jednostek: działy, rozdziały, Art./§ w kolejności, spisana z ISAP/tekstu, nie z wyniku konwersji); T095 liczy metryki nagłówków względem tych plików.
- Pozostałe uwagi niskiej wagi z analizy (C1 — fixture PDF z samymi ograniczeniami uprawnień w T023/T024, C3 — asercja `SourceId` w T034) uwzględnić przy odpowiednich zadaniach.

## Complexity Tracking

Brak naruszeń konstytucji wymagających uzasadnienia. Zależności ponad bibliotekę PDF są opisane w
Constitution Check (zasada VI).
