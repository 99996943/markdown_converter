# Implementation Plan: Poprawki parsera po przeglądzie liczb w regulaminach

**Branch**: `009-number-review-fixes` (od `main` po scaleniu PR #11) | **Date**: 2026-10-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/009-number-review-fixes/spec.md`

## Summary

Dwie usterki z przeglądu liczb i stała kontrola liczb w testach, zbadane sondami PdfPig na wszystkich PDF-ach
([research.md](./research.md)). Bez nowego etapu, bez nowych zależności, kontrakt Markdown 1.3.0 bez zmian:

1. **US2 — miara liczb** (R2): pomocnik testów `NumberCheck` porównuje wielozbiór liczb z warstwy tekstowej PDF
   (PdfPig bezpośrednio; cyfry indeksu górnego oddzielone; wiersze artefaktów z raportu pominięte) z liczbami
   Markdownu. Kolumny w raporcie prywatnego korpusu; bramka `number-check.txt` pada na liczbach nadmiarowych
   regulaminów z listy. Najpierw, żeby zmierzyć stan wyjściowy i dać czerwony test prywatny dla US1 i US3.
2. **US1 — „art. 66¹”** (R1): `LegalNumberTail` przyjmuje też „art.” (`(?<!\p{L})[Aa]rt\.`). Ten sam błąd jest w
   aktach (15 miejsc w `Corpus/acts`, 33 w `corpus/akty`, 8 w goldenie fragmentów ochrona-danych; dziś np.
   „art. 3855”, „art. 556[^2]”) — właściciel z góry zgodził się na te różnice, jeśli są wyłącznie tą zamianą.
3. **US3 — stopka z logo** (R3): w `LineAssemblyStage` drobne glify tworzące własny wiersz tekstu (≥ 4 glify jednego
   rozmiaru na wspólnej linii bazowej) nie dołączają do wiersza dużo większego napisu, gdy są od niego dalej niż
   `NearGlyphEm` × mniejszy rozmiar. Naprawia stopki corp-1, 2, 3, 5, 6. Kryterium wycofania FR-661.
4. **US4 — regresja i wydanie**: `baseline-009/`, porównanie zbiorów słów i miara liczb przed/po, pełny zestaw
   kontroli przed każdym commitem parsera; wersja 1.2.1, `refresh`/`verify`, README, CLAUDE.md, publish/.

## Technical Context

**Language/Version**: C# 13 / .NET 9 (`net9.0`), SDK z `global.json` — bez zmian

**Primary Dependencies**: bez zmian (`PdfPig` 0.1.16 — w testach używany przez referencję projektu parsera)

**Storage**: N/A (prywatny korpus właściciela w `tests/LegalAgent.PdfParser.Tests/Corpus/private`, poza git; nowe
nieśledzone pliki `number-check.txt` i `baseline-009/`)

**Testing**: xUnit v3 na MTP; testy etapów (`TextNormalizationStageTests`, `LineAssemblyStageTests` z
`LayoutFactory`), repliki stron (`SyntheticPdfBuilder`), test pomocnika `NumberCheck` na syntetycznym PDF, goldeny
`Corpus/acts` i `Corpus/banking`, goldeny fragmentów, prywatny korpus (`LEGALAGENT_PRIVATE_CORPUS`), korpus
syntetyczny (`refresh`, `verify`, `CorpusFull`)

**Target Platform**: Windows i Linux (CI ubuntu-latest); wynik deterministyczny

**Project Type**: biblioteka klas (`LegalAgent.PdfParser`) + CLI; aplikacje bez zmian kodu

**Performance Goals**: bez regresji SC-007 (100 stron < 10 s); wyznaczenie ciągów tekstu własnego liniowe po
posortowaniu glifów strony; `NumberCheck` tylko w testach

**Constraints**: wynik wyłącznie z tekstu PDF (FR-670); reguły z geometrii i typografii (FR-671); goldeny chronione
zmieniane tylko według FR-674 (wąska zgoda dla aktów); prawdziwe PDF-y i nazwy banków poza git

**Scale/Scope**: zmiany w `TextNormalizationStage` (wzorzec), `LineAssemblyStage` (ciągi tekstu własnego w
`GroupIntoLines`/`LineBuilder.Accepts`); testy: `NumberCheck` (nowy), `LayoutMetrics`, `PrivateCorpusTests`;
~20 wymagań FR-650…FR-676, 7 kryteriów SC-100…SC-106

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Zasada / ograniczenie | Ocena | Jak spełnione |
|---|---|---|
| I. Test-First / TDD | ✅ | US1 i US3: test etapu + replika strony (red, osobny commit) → kod (green). US2: test `NumberCheck` na syntetycznym PDF (red) → pomocnik (green); bramka prywatna czerwona do czasu US1/US3. Testy offline. |
| II. Wierność źródłu | ✅ | Zmienia się tylko zapis cyfr indeksu (znak `¹` zamiast cyfry/fałszywego `[^n]`) i podział liter na wiersze; nic nie jest dopisywane ani usuwane. Porównanie zbiorów słów i miara liczb to sprawdzają. |
| III. Powtarzalność | ✅ | Deterministyczne sortowania glifów i progi; miara liczb deterministyczna (kolejność strumienia, wielozbiory). |
| IV. Odporność na błędy | ✅ | Brak wywołań zewnętrznych; miara pomija dokument bez warstwy tekstowej z adnotacją. |
| V. Bezpieczeństwo i konfiguracja | ✅ | `number-check.txt`, `baseline-009/` i prywatne PDF-y poza git; repliki bez nazw banków (logo w replice: neutralny napis, np. „Bank.pl”). |
| VI. Prostota | ✅ | Zmiana jednego wzorca i jednego warunku w składaniu wierszy; bez nowych opcji, etapów, adnotacji i zależności. |
| VII. Dokumentacja | ✅ | README parsera (indeks po „art.”, stopka z logo, „Znane ograniczenia” z listy FR-676), CLAUDE.md (miara liczb, `number-check.txt`), handoff. |
| Biblioteka ogólna | ✅ | Reguły opisują geometrię; brak nazw dokumentów. |
| API jawne, SemVer | ✅ | Publiczne API bez zmian; poprawki zachowania → 1.2.1 (PATCH); kontrakt 1.3.0 bez zmian. |

**Wynik bramki (przed Phase 0)**: PASS.

**Re-check po Phase 1**: PASS — model biblioteki bez zmian ([data-model.md](./data-model.md)); nowy interfejs to
wyłącznie format pliku listy i kolumn raportu w testach ([contracts/number-check.md](./contracts/number-check.md));
ryzyko US3 (rdzeń składania wierszy) ma zmierzony zasięg (R3) i kryterium wycofania FR-661.

## Project Structure

### Documentation (this feature)

```text
specs/009-number-review-fixes/
├── plan.md              # ten plik
├── research.md          # Phase 0: R1 indeks po „art.”, R2 miara liczb, R3 stopka z logo, R4 wersje i kolejność
├── data-model.md        # Phase 1: NumberDiff, lista regulaminów, ciąg tekstu własnego
├── quickstart.md        # Phase 1: walidacja
├── contracts/
│   └── number-check.md  # format number-check.txt i kolumn raportu
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/LegalAgent.PdfParser/
├── Stages/TextNormalizationStage.cs     # LegalNumberTail: (?<!\p{L})[Aa]rt\.|§
└── Stages/LineAssemblyStage.cs          # ciągi tekstu własnego; LineBuilder.Accepts odrzuca daleki glif takiego ciągu

tests/LegalAgent.PdfParser.Tests/
├── Fixtures/NumberCheck.cs              # nowy: liczby PDF (PdfPig) vs Markdown, NumberDiff, RegulationGate
├── Fixtures/LayoutMetrics.cs            # Measure(...) z NumberDiff, gdy podano PDF
├── Unit/Fixtures/NumberCheckTests.cs    # syntetyczny PDF: indeks poprawny 0 rozbieżności, sklejony → nadmiarowa
├── Unit/Stages/TextNormalizationStageTests.cs   # „art. 66¹”, „art. 68²”, „Art.”, „§”, „kart. 5”, zwykłe liczby
├── Unit/Stages/LineAssemblyStageTests.cs        # logo + 4 wiersze stopki; odnośnik „***)” i indeks przy słowie
├── Integration/CorpusLayoutsIntegrationTests.cs # replika s. 8 corp-5 (+ przypis na stronie) i ostatniej strony ze stopką
└── Corpus/PrivateCorpusTests.cs         # kolumny liczb w raporcie, RegulationNumbers_HaveNoExcess

tests/LegalAgent.PdfParser.Tests/Corpus/acts/*.expected.md        # 4 akty — wąska zgoda (FR-674)
tests/LegalAgent.Chunking.Tests/Golden/dz-u-2019-1781-ochrona-danych.chunks.jsonl   # wąska zgoda
corpus/akty/*.md, *.chunks.jsonl, corpus/manifest.json             # refresh (wąska zgoda + wersja)
Directory.Build.props                                               # 1.2.1
src/LegalAgent.PdfParser/README.md, CLAUDE.md                       # dokumentacja
```

**Structure Decision**: jak w spec 007/008 — poprawki w istniejących etapach biblioteki, miary w `Fixtures/` testów
parsera, prywatne listy obok PDF-ów.

## Kolejność realizacji (wskazówka dla /speckit-tasks)

1. **Przygotowanie**: kopia prywatnych goldenów do `Corpus/private/baseline-009/`; `baseline-008` w raporcie
   zamienione na `baseline-009`.
2. **US2**: `NumberCheckTests` (red) → `NumberCheck` (green); kolumny w `LayoutMetrics`/raporcie (commit testów
   pomocniczych); sprawdzenie znanych przypadków (corp-5, firm-1, corp-2) i pomiar wyjściowy w handoffie;
   `number-check.txt` (15 regulaminów) i `RegulationNumbers_HaveNoExcess` — czerwony na prywatnym korpusie (corp-5,
   corp-2), commit jako red.
3. **US1**: testy etapu + replika s. 8 corp-5 z przypisem „1” na stronie (red) → wzorzec (green). Goldeny: corp-5,
   4 × `Corpus/acts`, golden fragmentów ochrona-danych — diff sprawdzony skryptem: każda zmieniona linia różni się
   tylko cyframi→indeksem i `[^n]`. FR-673.
4. **US3**: testy etapu (logo + stopka; ochrona: indeks przy słowie, „***)”, „123)” przy słowie, nagłówek 14 pt +
   tekst 7 pt na wspólnej linii bazowej) + replika ostatniej strony (red) → warunek w `LineAssemblyStage` (green).
   Goldeny corp-1, 2, 3, 5, 6 (stopki), ewentualnie firm-2, 5, 6 (bez pogorszenia miar). Jeśli FR-661 — wycofanie,
   wpis w README, wyjątek w `number-check.txt`. FR-673.
5. **Zamknięcie**: bramka liczb zielona; `<Version>` 1.2.1; `refresh` + `verify` (manifest, `corpus/akty`) — diff
   `corpus/` sprawdzony jak w kroku 3; README parsera („Znane ograniczenia” z FR-676), CLAUDE.md, tabela miar
   przed/po i zbiory słów w handoffie, testy wydajności, przebudowa publish/.

## Stan prac i przekazanie

- 2026-10-11: spec, clarify (decyzja US3: naprawa w kodzie z wycofaniem wg FR-661; wąska zgoda na zmiany aktów z
  US1), plan, research R1–R4, data-model, kontrakt `number-check.md`, quickstart. Gałąź `009-number-review-fixes`
  bez commitów. `tasks.md`: 24 zadania (T001–T024). Następny krok: `/speckit-implement` od T001.

## Complexity Tracking

Brak naruszeń konstytucji — sekcja pusta.
