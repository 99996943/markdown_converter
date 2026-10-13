# Implementation Plan: Linki z FAQ do miejsca źródła (strona PDF-u i nagłówek Markdownu)

**Branch**: `013-faq-source-links` (od `012-footnote-original-labels`, wersja 1.4.0) | **Date**: 2026-10-13 |
**Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/013-faq-source-links/spec.md`

## Summary

W wierszu źródła FAQ nazwa dokumentu prowadzi do PDF-u od razu na stronę jednostki (`#page=N`), a jednostka do
nagłówka jej sekcji w Markdownie dokumentu zapisanym przez aplikację (`../downloads/<plik>.md#kotwica`). Tekst
wiersza bez zmian. `UnitExtractor` zwraca obok jednostek ich położenie (strona początku sekcji z parsera, kotwica
nagłówka z zapisanego Markdownu, algorytm GitHub/VS Code sprawdzony w źródłach VS Code — research R3);
`UnitMatcher.Find` wiąże jednostkę zacytowaną przez model z pozycją listy; aplikacja liczy ścieżkę względną od pliku
FAQ do Markdownu dokumentu; `FaqMarkdownRenderer` pisze linki. Biblioteki 1.5.0 (MINOR, zmiany addytywne).

## Technical Context

**Language/Version**: C# 13 / .NET 9 (`net9.0`), SDK z `global.json` — bez zmian

**Primary Dependencies**: bez zmian (`Microsoft.SemanticKernel.Abstractions` w `LegalAgent.Faq`, parser z projektu);
bez biblioteki slug — algorytm na kategoriach Unicode .NET (R3)

**Storage**: pliki: `downloads/<plik>.md` (już zapisywane), `faq/FAQ_mBank.md`

**Testing**: xUnit v3 na MTP; `tests/LegalAgent.Faq.Tests` (kotwice, położenie jednostek, `UnitMatcher.Find`,
renderer, generator), `tests/mBank.FaqGenerator.Tests` (`AppHarness`, fałszywy model, ścieżka względna); offline

**Target Platform**: Windows i Linux (CI ubuntu-latest); ścieżki `Path.GetRelativePath`, separator `/` w linku

**Project Type**: biblioteka `LegalAgent.Faq` + aplikacja konsolowa `mBank.FaqGenerator`

**Performance Goals**: bez znaczenia (jedno przejście po nagłówkach 5 dokumentów)

**Constraints**: tekst wiersza źródła bez zmian (FR-1004); `resource` bez `#page` (FR-1005); biblioteka bez nazw
banku, hostów i plików (FR-1006); każdy brak danych → wiersz jak dziś (FR-1003); testy offline (FR-1007)

**Scale/Scope**: `Conversion/UnitExtractor.cs`, `Conversion/DocumentSetConverter.cs`, `Conversion/Model/ConversionModels.cs`,
`Model/FaqDocumentInput.cs`, `Model/FaqSourceDocument.cs`, nowe `Model/FaqUnitLocation.cs` i `MarkdownAnchors.cs`,
`UnitMatcher.cs`, `FaqGenerator.cs`, `FaqMarkdownRenderer.cs`; aplikacja `FaqStage.cs`, `Program.cs`; README obu,
kontrakt; 8 wymagań FR-1000…FR-1007, 4 kryteria SC-140…SC-143

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Zasada / ograniczenie | Ocena | Jak spełnione |
|---|---|---|
| I. Test-First / TDD | ✅ | Testy kotwic, położenia, `Find`, renderera i aplikacji jako commit red, potem green. Nowe typy i puste właściwości `init` potrzebne do kompilacji testów — w commicie red (jak spec 012 T003). |
| II. Wierność źródłu | ✅ | Treść FAQ bez zmian; dochodzą tylko adresy linków (FR-432 spec 006: formatowanie źródła). |
| III. Powtarzalność | ✅ | Kotwice i ścieżki deterministyczne; renderer daje ten sam tekst dla tego samego wejścia. |
| IV. Odporność na błędy | ✅ | Brak strony, kotwicy albo ścieżki → wiersz jak dziś, FAQ powstaje (FR-1003); bez nowych kodów wyjścia. |
| V. Bezpieczeństwo i konfiguracja | ✅ | Bez nowych opcji i sekretów; testy bez sieci i Azure. |
| VI. Prostota | ✅ | Jeden nowy typ danych, jedna klasa kotwic, jedna funkcja `Find`; bez nowych zależności. |
| VII. Dokumentacja | ✅ | Addendum kontraktu pliku FAQ, README biblioteki i aplikacji (ograniczenie linków na GitHubie), CLAUDE.md, handoff. |
| API jawne, SemVer | ✅ | Zmiany addytywne → 1.5.0 (MINOR). |

**Wynik bramki (przed Phase 0)**: PASS. **Re-check po Phase 1**: PASS — [data-model.md](./data-model.md),
[contracts/faq-file-links.md](./contracts/faq-file-links.md).

## Project Structure

### Documentation (this feature)

```text
specs/013-faq-source-links/
├── plan.md              # ten plik
├── research.md          # R1 położenie, R2 Find, R3 kotwice (VS Code), R4 #page, R5 ścieżka, R6 tekst linku, R7 wersje
├── data-model.md        # FaqUnitLocation, rozszerzenia modeli, MarkdownAnchors, wiersz źródła
├── quickstart.md
├── contracts/
│   └── faq-file-links.md    # addendum do spec 006 faq-file.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/LegalAgent.Faq/
├── MarkdownAnchors.cs                 # nowy: slug + kotwice dokumentu
├── UnitMatcher.cs                     # Find
├── Conversion/UnitExtractor.cs        # Locate
├── Conversion/DocumentSetConverter.cs # ConvertedDocument.UnitLocations
├── Conversion/Model/ConversionModels.cs
├── Model/FaqUnitLocation.cs           # nowy
├── Model/FaqDocumentInput.cs, Model/FaqSourceDocument.cs  # UnitLocations, MarkdownLink
├── FaqGenerator.cs                    # przepisuje położenie do FaqSourceDocument
├── FaqMarkdownRenderer.cs             # linki w wierszu źródła
└── README.md
src/mBank.FaqGenerator/
├── FaqStage.cs                        # MarkdownLink (ścieżka względna), UnitLocations
├── Program.cs                         # katalog pobranych plików do FaqStage
└── README.md
Directory.Build.props                  # 1.5.0

tests/LegalAgent.Faq.Tests/            # MarkdownAnchorsTests, UnitExtractorTests, UnitMatcherTests, RendererTests, FaqGeneratorTests
tests/mBank.FaqGenerator.Tests/        # przebieg z fałszywym modelem: linki i kotwice w FAQ
```

**Structure Decision**: bez nowych projektów.

## Kolejność realizacji (wskazówka dla /speckit-tasks)

1. **Foundational**: `FaqUnitLocation`, puste właściwości `UnitLocations`/`MarkdownLink` (kompilacja testów red).
2. **US1 (P1, `#page=N`)**: red — `UnitExtractor.Locate` (strony), `UnitMatcher.Find`, renderer (nazwa z `#page`,
   adres z `#`, brak jednostki), generator przepisuje położenie; green; test aplikacji (strona w FAQ).
3. **US2 (P2, nagłówek Markdownu)**: red — `MarkdownAnchors` (tabela przykładów z algorytmu VS Code, powtórzenia,
   ucieczki), kotwice w `Locate`, renderer (link jednostki, ucieczka `[`/`]`, brak ścieżki/kotwicy), aplikacja
   (ścieżka względna `--output`/`--faq-output`, inny dysk → bez linku, SC-141 kotwica ∈ nagłówki); green.
4. **Polish**: kontrakt spec 006 (odsyłacz do addendum), README biblioteki i aplikacji, CLAUDE.md, wersja 1.5.0,
   `generate`/`verify` korpusu (wersja w manifeście), `publish/`, SC-142 ręcznie przez właściciela (prawdziwy
   przebieg FAQ), handoff.

## Stan prac i przekazanie

- 2026-10-13: spec (wariant 3), clarify (nazwa → `#page`, jednostka → nagłówek; linki do Markdownu zawsze), plan z
  research R1–R7. Algorytm kotwic sprawdzony w źródłach VS Code (`githubSlugifier` = `github-slugger`). Następny
  krok: `/speckit-tasks`. Tasks: 26 zadań (T001–T026), następny krok `/speckit-implement`.

## Complexity Tracking

Brak naruszeń konstytucji.
