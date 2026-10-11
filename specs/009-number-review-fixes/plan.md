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
- 2026-10-11, US2 (T001–T008): `baseline-009/` (21 goldenów) i `number-check.txt` (15 regulaminów z zestawów FAQ:
  corp-1, 2, 3, 5, 6, ind-1…5, firm-2…6) w `Corpus/private`, poza git; prywatny korpus zielony na starcie.
  `NumberCheck` + `RegulationGate` (testy offline), kolumny `Liczby +`, `Liczby −`, `Odnośniki sklejone` w raporcie.
  - Fałszywy alarm miary (corp-3, ostatnia strona): cyfra stopki 6,7 pt po logo 22,7 pt brana za indeks górny →
    indeks musi stać ≤ 0,5 em od poprzedniej litery (test `FromPdf_SmallRaisedDigitFarFromThePreviousLetter_IsANumber`).
  - Klasa spoza specyfikacji: odnośniki przypisów niewykryte przez parser i sklejone ze słowem („Tak4”, „PESEL17”,
    „1dodaniu”, „pary walutowej1”, „BLIK9,10”) — ind-1, 2, 4, firm-2, 3, 5, 6, corp-5, reg1, regulamin-pdp; bramka
    byłaby czerwona na 11 z 15 regulaminów. **Decyzja właściciela (2026-10-11)**: miara traktuje liczbę przyklejoną
    do litery jako odnośnik, gdy ta sama wartość jest w PDF indeksem górnym; liczone osobno („Odnośniki sklejone”),
    nie jako nadmiarowe. „CO2”, „MT940”, „art. 32a” (pełne cyfry w PDF) bez zmian; „661”, „150.000” dalej nadmiarowe.
    Naprawa w parserze — kandydat na spec 010; opis w „Znanych ograniczeniach” (T021).
  - Etykiety definicji przypisów bez „)” scalone przez parser w jeden `[^1]` („[^1]: 1 … 2 … 3 …”, w PDF etykiety to
    indeks górny): ind-2 „3”, ind-4 „1” — wpisane jako dopuszczone w `number-check.txt` z komentarzem (to samo
    zjawisko w corp-5 s. 25 „1 W przypadku…” daje sklejony odnośnik „walutowej1”).
  - **Pomiar wyjściowy** (wersja 1.2.0; + nadmiarowe / − brakujące / odnośniki sklejone):

    | Dokument | + | − | skl. | Wartości |
    |---|---|---|---|---|
    | corp-2 | 8 | 1 | 0 | + 0×3, 1×2, 2×3; − 01.01.2022×1 (przepleciona stopka, US3) |
    | corp-5 | 2 | 2 | 1 | + 661×1, 682×1; − 66×1, 68×1 (US1) |
    | firm-1 | 6 | 6 | 0 | + 150.000, 230.000, 320.000, 450.000, 7, 9; − 1, 3, 20.000, 30.000, 50.000×2 (poza listą) |
    | ind-2 | 1 | 0 | 7 | + 3×1 (dopuszczone) |
    | ind-4 | 1 | 0 | 17 | + 1×1 (dopuszczone) |
    | reg1, regulamin-pdp | 1 | 0 | 9 | + 6×1 (poza listą) |
    | firm-2 / firm-3 / firm-5 / firm-6 / ind-1 | 0 | 0 | 4 / 9 / 11 / 1 / 15 | — |

    Pozostałe dokumenty: 0 / 0 / 0.
  - **Klasyfikacja brakujących (SC-101)** w regulaminach z listy: corp-2 „01.01.2022” — data rozbita przeplecionymi
    literami stopki (US3); corp-5 „66”, „68” — sklejone z indeksem (US1). Innych brakujących nie ma; utraty tekstu brak.
  - Bramka `RegulationNumbers_HaveNoExcess` czerwona dokładnie na corp-2 i corp-5 (commit red 48735e6).
- 2026-10-11, przerwa po fazie 3: testy US1 T009 (etap) i T010 (replika s. 8 corp-5) zatwierdzone jako red (9ae9356;
  replika daje dziś „art. 66[^1] § 1 do § 3 oraz art. 682”). **Następny krok**: T011 (wzorzec `LegalNumberTail` w
  `TextNormalizationStage`), potem T012 (goldeny w wąskiej zgodzie). Prywatny korpus wymaga bezwzględnej ścieżki
  `LEGALAGENT_PRIVATE_CORPUS=C:/GIT/markdown_converter/tests/LegalAgent.PdfParser.Tests/Corpus/private`.
- 2026-10-11, US1 (T011–T012): `LegalNumberTail` = `(?:(?<!\p{L})[Aa]rt\.|§)\s*\d+[a-z]*[⁰¹²³⁴-⁹]*$` (eda7728);
  goldeny w wąskiej zgodzie (356f89f). Liczby miejsc (cyfry indeksu) zgodne z research: prawa-konsumenta 4
  („art. 385⁵, art. 556⁴, art. 556⁵ i art. 576⁵” — dwa fałszywe `[^2]` znikają, przypisy dalej przenumerowane),
  kredyt-konsumencki 1 („art. 720²”), prawo-bankowe 7 („art. 9²” ×2, „720²”, „479¹¹⁵”, „365¹”), usługi-płatnicze 3
  („art. 166¹¹²)”). Golden fragmentów ochrona-danych: „art. 22¹”, „22²” ×2, „22³”, „77⁵”, „23¹” (8 cyfr); znikają
  fałszywe `[^2]`–`[^4]`, więc definicje, które zagarniały, wracają do właściwego odnośnika (Art. 5a) albo są
  nieprzypisane (`[^14]` „Niniejsza ustawa … wdraża …”); preambuła krótsza → 21 zamiast 22 części, przesunięte
  identyfikatory. Sprawdzone skryptem: po cofnięciu indeksów na cyfry i usunięciu `[^n]` multizbiór wierszy Markdown
  (4 akty + ochrona-danych) jest równy dawnemu z dokładnością do fałszywych `[^n]`. Prywatny corp-5: tylko
  „66¹”, „68²” (golden zaktualizowany, poza git); `Corpus/banking` i pozostałe prywatne goldeny bez zmian.
  Bramka liczb czerwona już tylko na corp-2 (`0×3, 1×2, 2×3`). `corpus/akty` odświeżane w T019.
  - Ograniczenia zauważone przy przeglądzie (do „Znanych ograniczeń”): zakres „art. 720²–7205” — druga cyfra
    indeksu po myślniku zostaje cyfrą (ogon nie zaczyna się od „art.”); „art. 166¹¹²)” to w PDF odnośnik przypisu
    „112)” (`[^112]` istnieje), nie indeks artykułu — wcześniej sklejony „166112)”, teraz zapisany indeksem; ta sama
    klasa co odnośniki sklejone (kandydat spec 010).
- 2026-10-11, US3 (T013–T017): testy red 8c6bb48 (stopka obok logo — etap i replika strony; ochrona: indeks „¹” po
  „66”, „***)”, „123)” przy słowie, tekst 7 pt obok nagłówka 14 pt na wspólnej linii bazowej — zielone przed i po).
  T015 według data-model (ciągi ≥ 4 glifów jednego rozmiaru, odrzucenie w `LineBuilder.Accepts`) dawało zielone
  testy, ale pełny zestaw (T016) pokazał różnice w `Corpus/acts`: kredyt-konsumencki (załącznik — formularz
  informacyjny: wiersze tabeli rozbite, nagłówek „Wybór prawa właściwego” zamieniony w akapit) i obwieszczenie-msz
  (przypisy „\*\*\*)”, „\*\*\*\*)” scalone z poprzednim). Zawężenie (reguła tylko na ścieżce nakładania pionowego, bez
  tolerancji linii bazowej) — te same różnice w aktach, a stopka i tak niepełna. **Kryterium FR-661 niespełnione →
  T017**: zmiana wycofana (kod `LineAssemblyStage` bez zmian), testy stopki `Skip` z odwołaniem do „Znanych
  ograniczeń” (ochrona aktywna), wyjątek corp-2 `0,0,0,1,1,2,2,2` w `number-check.txt`, opis w README. Pełny zestaw
  z prywatnym korpusem zielony (2184, 9 pominiętych), bramka liczb zielona. Kandydat na spec 010: rozplatanie stopki
  w późniejszym etapie (np. wiersz z dużym napisem i ≥ 2 liniami bazowymi drobnego tekstu) zamiast w rdzeniu
  składania wierszy.
- 2026-10-11, US4 (T018–T024) — **spec zakończona**:
  - Wersja 1.2.1 (`Directory.Build.props`; w `corpus/manifest.json` `parserVersion` i ręcznie `generatorVersion`).
    `refresh` zmienił wyłącznie 7 aktów w `corpus/akty` (`*.md`, `*.chunks.jsonl`) i manifest: 31 cyfr zamienionych
    na indeks (ochrona-danych 6, prawa-konsumenta 4, kredyt-konsumencki 1, prawo-bankowe 7, usługi-płatnicze 3,
    aml 3, kredyt-hipoteczny 7; research liczył 33 razem z dwoma „Art. 22²/22³” w ochronie danych, które były już
    poprawne). Po cofnięciu indeksów i usunięciu `[^n]` wiersze identyczne (skrypt multizbioru). `verify` czysty
    (172c232).
  - Pełny zestaw z `LEGALAGENT_CORPUS_FULL=1`, prywatnym korpusem i raportem: 2184, 0 niepowodzeń, 2 pominięte;
    `Category=Performance` osobno 4/4. Słowa vs `baseline-009` (SC-105): zmieniony tylko golden corp-5.
  - **Miary przed/po** (+ nadmiarowe / − brakujące):

    | Dokument | Przed | Po | Uwagi |
    |---|---|---|---|
    | corp-5 | 2 / 2 (661, 682 / 66, 68) | 0 / 0 | US1 |
    | corp-2 | 8 / 1 | 8 / 1 | stopka (US3 wycofane), wyjątek w `number-check.txt` |
    | firm-1 | 6 / 6 | 6 / 6 | poza listą (tabela limitów, „Znane ograniczenia”) |
    | ind-2, ind-4 | 1 / 0 | 1 / 0 | scalone definicje przypisów, dopuszczone |
    | reg1, regulamin-pdp | 1 / 0 | 1 / 0 | poza listą |

    Przeplecione wiersze stopki: 5 dokumentów corp przed i po (wycofanie FR-661). Brakujące po zmianach (SC-101):
    tylko corp-2 „01.01.2022” (stopka).
  - **Zmienione goldeny**: prywatny corp-5 (2 miejsca); `Corpus/acts` 4 akty (15 cyfr) i golden fragmentów
    ochrona-danych — w wąskiej zgodzie właściciela; `Corpus/banking`, pozostałe prywatne, syntetyczne — bez zmian.
  - README („Znane ograniczenia”, kontrola liczb) i CLAUDE.md zaktualizowane (f08dcfb, pisał Sonnet, przejrzane).
    `publish/` (lib, nuget 1.2.1, exe) przebudowany z f08dcfb; exe i dll raportują 1.2.1 (exe nieuruchamiany).
  - Gałąź niewypchnięta. Kandydaci na spec 010: odnośniki przypisów sklejone ze słowem / po numerze artykułu,
    scalone definicje przypisów, stopka obok logo w późniejszym etapie, tabela limitów firm-1.

## Complexity Tracking

Brak naruszeń konstytucji — sekcja pusta.
