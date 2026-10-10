# Implementation Plan: Regulaminy z etykietami w wysuniętej kolumnie i paragrafami „§ N”

**Branch**: `007-corporate-regulation-layout` (od `main` po scaleniu PR #8) | **Date**: 2026-10-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/007-corporate-regulation-layout/spec.md`

## Summary

Cztery regulaminy mBanku dla firm (D-A…D-D) mają etykiety „1.”, „1/”, „a/” w wysuniętej kolumnie, paragrafy „§ N”
jako wyśrodkowany pogrubiony wiersz i słowniczek z terminem po lewej. Parser zamienia to w tabele zastępcze (TBL001:
132/16/58/79), dzieli zawinięte wiersze i gubi jednostki „§”.

Badania (research R1–R3, pomiar geometrii na prawdziwych stronach) pokazały, że układ etykiet jest **taki sam jak w
aktach ISAP** — różni się tylko składnia „1/”, „a/”, której parser nie zna. Podejście:

1. **US2 — etykiety z ukośnikiem**: nowe rodzaje `ArabicSlash`, `LetterSlash` w `ListLabelPatterns` i dopisanie ich
   tam, gdzie TableDetection i ListDetection obsługują „1)”, „a)”; uogólnienie `IsHangingList`. Eksperyment na kopii:
   TBL001 132→6, 16→0, 58→1, 79→2 (SC-080 spełnione), listy zagnieżdżone.
2. **US1 — „§ N”**: goły wiersz „§ 5” jako jednostka (wyróżniony, odosobniony), osłona przed wciągnięciem do tabeli
   bez siatki, wiersz z tytułem jako jeden nagłówek, poziom pod numerowanym rozdziałem.
3. **US3 — słowniczek**: region z liniami poziomymi dzielonymi na wspólnym x i etykietą + pogrubionym terminem po
   lewej → lista definicji (adnotacje `deflist.*`), nie tabela.
4. **US4 — regresja**: prywatny korpus z 15 dokumentami mBanku (punkt odniesienia przed zmianą), pomocnik miar w
   testach, pełny zestaw kontroli przed każdym commitem (FR-534).

## Technical Context

**Language/Version**: C# 13 / .NET 9 (`net9.0`), SDK z `global.json` — bez zmian

**Primary Dependencies**: bez zmian (`PdfPig`, `Microsoft.Extensions.*`)

**Storage**: N/A (prywatny korpus właściciela w `tests/LegalAgent.PdfParser.Tests/Corpus/private`, poza git)

**Testing**: xUnit v3 na MTP; testy etapów (`LayoutFactory`, `PageSketch`, `StageHarness`), repliki stron
(`SyntheticPdfBuilder`), goldeny `Corpus/acts` i `Corpus/banking`, prywatny korpus (`LEGALAGENT_PRIVATE_CORPUS`),
korpus syntetyczny (`refresh`, `verify`, `CorpusFull`)

**Target Platform**: Windows i Linux (CI ubuntu-latest); wynik deterministyczny

**Project Type**: biblioteka klas (`LegalAgent.PdfParser`) + CLI; bez zmian w aplikacjach

**Performance Goals**: bez regresji SC-007 (100 stron < 10 s); zmiany liniowe względem liczby wierszy

**Constraints**: wynik wyłącznie z tekstu PDF (bez dopisanych słów); reguły z geometrii i typografii, nie z nazw
dokumentów (FR-530); goldeny bez zmian bez zgody właściciela (FR-163/FR-531); prawdziwe PDF-y poza git (FR-533)

**Scale/Scope**: zmiany w `ListLabelPatterns`, `TableDetectionStage`, `ListDetectionStage`, `HeadingDetectionStage`
(+ `AssignLevels`), `LegalUnitPatterns`, `ReadingOrderStage` (słowniczek), `LayoutAnnotations`; 2 wartości enum;
~35 wymagań FR-500…FR-541, 8 kryteriów SC-080…SC-087

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Zasada / ograniczenie | Ocena | Jak spełnione |
|---|---|---|
| I. Test-First / TDD | ✅ | Każda zmiana zachowania: replika strony lub test etapu (red), osobny commit, potem kod (green). Testy offline; prawdziwe PDF-y tylko w prywatnym korpusie. |
| II. Wierność źródłu | ✅ | Etykiety i tytuły dosłownie, bez dopisanych znaków (także w słowniczku — decyzja właściciela); żaden tekst nie jest pomijany. |
| III. Powtarzalność | ✅ | Deterministyczne sortowania, kultura niezmienna we wzorcach; pomiar jednym pomocnikiem testowym. |
| IV. Odporność na błędy | ✅ | Brak wywołań zewnętrznych; region niespełniający warunków wraca do dotychczasowej ścieżki. |
| V. Bezpieczeństwo i konfiguracja | ✅ | Prawdziwe dokumenty i ich nazwy poza git (lista „układu etykiet” w nieśledzonym pliku). |
| VI. Prostota | ✅ | Rozszerzenie istniejących ścieżek list zamiast nowego etapu (R1); jedna nowa para adnotacji tylko dla słowniczka. Zero nowych zależności. |
| VII. Dokumentacja | ✅ | README parsera: etykiety „1/”, „a/”, „§ N”, słowniczek, zaktualizowane znane ograniczenia; handoff w planie. |
| Biblioteka ogólna | ✅ | Wzorce etykiet i „§” to ogólny skład polskich regulaminów; brak reguł dla mBanku. |
| API jawne, SemVer | ✅ | Kontrakt 1.2.0 (MINOR): dwie nowe wartości `ListLabelKind` ([contracts/markdown-output.md](./contracts/markdown-output.md)). |

**Wynik bramki (przed Phase 0)**: PASS.

**Re-check po Phase 1**: PASS — model zmienia się addytywnie ([data-model.md](./data-model.md)), bez nowych projektów i
zależności. Ryzyko regresji opisane w research (R1–R3) i osłonięte FR-534/SC-087.

## Project Structure

### Documentation (this feature)

```text
specs/007-corporate-regulation-layout/
├── plan.md              # ten plik
├── research.md          # Phase 0: R1 etykiety, R2 „§ N”, R3 słowniczek, R4 pomiar, R5 kolejność
├── data-model.md        # Phase 1: ListLabelKind, Section, adnotacje deflist.*, miary
├── quickstart.md        # Phase 1: walidacja
├── contracts/
│   └── markdown-output.md   # uzupełnienie kontraktu Markdown (1.2.0)
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/LegalAgent.PdfParser/
├── Model/ListItem.cs                    # ListLabelKind: ArabicSlash, LetterSlash
├── Text/ListLabelPatterns.cs            # wzorce „1/”, „a/” (cały token + tekst)
├── Text/LegalUnitPatterns.cs            # goły wiersz „§ N”
├── Layout/LayoutAnnotations.cs          # deflist.entry, deflist.side
├── Stages/TableDetectionStage.cs        # IsLabel/CellsOf, IsHangingList, osłona wiersza jednostki, słowniczek
├── Stages/ListDetectionStage.cs         # Rank, budowa elementów słowniczka
├── Stages/ReadingOrderStage.cs          # kolejność wpisów słowniczka
├── Stages/HeadingDetectionStage.cs      # goły „§ N”, tytuł bez podziału, poziom pod numerowanym rozdziałem
└── Rendering/MarkdownEscaper.cs         # etykieta „1/” bez ucieczki (sprawdzenie)

tests/LegalAgent.PdfParser.Tests/
├── Unit/Text/ListLabelPatternsTests.cs
├── Unit/Stages/HeadingDetectionStageTests.cs, TableDetectionStageTests.cs, ListDetectionStageTests.cs
├── Integration/HangingLabelLayoutTests.cs, ParagraphUnitHeadingTests.cs, GlossaryLayoutTests.cs   # repliki stron
├── Corpus/PrivateCorpusTests.cs         # + miary i progi SC (pomocnik LayoutMetrics)
└── Fixtures/LayoutMetrics.cs            # nowy pomocnik miar (R4)
```

**Structure Decision**: wyłącznie biblioteka parsera i jej testy; `LegalAgent.Chunking`, `LegalAgent.Faq` i aplikacje
bez zmian kodu (korzystają z nowych nagłówków i list automatycznie).

## Kolejność realizacji (wskazówka dla /speckit-tasks)

Harmonogram: konsultacje dzień 3, implementacja do dnia 8, zamrożenie dni 9–10.

1. **Przygotowanie (dzień 3–4)**: prywatny korpus +15 dokumentów z goldenami obecnego wyniku i lista „układu
   etykiet”; pomocnik `LayoutMetrics` + raport miar w teście prywatnego korpusu (pomiar wyjściowy jako test).
2. **US2 (dzień 4–5)**: wzorce „1/”, „a/” (red/green), `IsLabel`/`CellsOf`, `Rank`, `KeepLabelledBoldTextInLists`,
   uogólnienie `IsHangingList`; repliki R2 (zagnieżdżenie, trzy poziomy + część wspólna, kontrola tabeli danych).
   Pełny zestaw kontroli (FR-534) i przegląd różnic D-A…D-D z właścicielem.
3. **US1 (dzień 5–6)**: C1 goły „§ N” + C3; C4 tytuł; C5 poziom; C2 osłona w regionach bez siatki — każde osobno
   red/green; repliki R1a–R1d.
4. **US3 (dzień 6–8)**: słowniczek (adnotacje, ReadingOrder, ListDetection); replika R3 i wariant z „1.”.
5. **US5 (opcjonalnie, tylko jeśli dzień 8 pozwala)**: spis treści z kropkami, numery „N/M”.
6. **Zamknięcie (dzień 8)**: README parsera, CLAUDE.md, handoff, miary końcowe, przebieg FAQ na D-A…D-D (SC-083).

## Stan prac i przekazanie

- 2026-10-10: spec (z clarify: format słowniczka, reguła regresji 5+6 dokumentów, FR-534/SC-087), research R1–R5 na
  prawdziwych stronach D-A…D-D, data-model, kontrakt Markdown 1.2.0, quickstart. Następny krok: `/speckit-tasks`.
- Dane pomiarowe i narzędzia sond (PdfPig) w scratchpadzie sesji — nie są częścią repozytorium.
- 2026-10-10 (implementacja, T001–T006): prywatny korpus +15 dokumentów (`mbank-ind-1…5`, `mbank-corp-1…5`,
  `mbank-firm-1…5`; D-A = corp-1, D-B = corp-2, D-C = corp-3, D-D = corp-5) z goldenami obecnego wyniku — prywatny
  korpus zielony (19 dokumentów). `LayoutMetrics` (test pomocniczy) i raport miar w `PrivateCorpusTests`
  (`LEGALAGENT_CORPUS_REPORT=1` → `layout-metrics.md` obok PDF-ów; kopia wyjściowa `layout-metrics.baseline.md`);
  test `Category=Layout007` czerwony zgodnie z planem. Miary wyjściowe:

  | Dokument | TBL001 | Wiersze „ \| ” | Etykiety poza listą | „§ N” jako tekst | Spis treści | „N/M” |
  |---|---|---|---|---|---|---|
  | D-A (corp-1) | 132 | 1137 | 1155 | 154 | 0 | 0 |
  | D-B (corp-2) | 16 | 142 | 149 | 36 | 9 | 0 |
  | D-C (corp-3) | 58 | 552 | 566 | 123 | 0 | 0 |
  | corp-4 | 0 | 0 | 0 | 0 | 0 | 0 |
  | D-D (corp-5) | 79 | 665 | 642 | 15 | 0 | 0 |
  | firm-1 | 3 | 76 | 0 | 0 | 0 | 6 |
  | firm-2 | 2 | 17 | 34 | 0 | 0 | 0 |
  | firm-3 | 0 | 0 | 0 | 0 | 0 | 0 |
  | firm-4 | 0 | 0 | 7 | 0 | 0 | 0 |
  | firm-5 | 1 | 2 | 0 | 0 | 0 | 0 |
  | ind-1 | 0 | 0 | 0 | 0 | 0 | 0 |
  | ind-2 | 7 | 62 | 0 | 0 | 0 | 35 |
  | ind-3 | 0 | 0 | 1 | 0 | 0 | 0 |
  | ind-4 | 6 | 82 | 0 | 0 | 0 | 0 |
  | ind-5 | 0 | 0 | 0 | 0 | 0 | 0 |

- 2026-10-10 (US2, T007–T015 z T012a, T013a, T014a, T014b): etykiety „1/”, „a/” (`ArabicSlash`, `LetterSlash`);
  `IsHangingList` — każda komórka poza ostatnią to samotna etykieta (także „a.”, „ii.”), dowolna liczba pasm;
  `CellsOf` łączy „1.”, „1/”, „a/” z tekstem przy odstępie ≤ 2 em; ciąg „N.” pomija zagnieżdżone „N.” na innym
  wcięciu. `Rank` dla etykiet z ukośnikiem świadomie NIE dodany (zagnieżdżenie z wcięcia; ranga zepsułaby „1.” pod
  „1/”). Miary po US2 (TBL001 / wiersze „ \| ” / etykiety poza listą):

  | Dokument | Przed | Po US2 | SC-080 (≤) |
  |---|---|---|---|
  | D-A (corp-1) | 132 / 1137 / 1155 | 6 / 43 / 20 | 13 / 114 ✅ |
  | D-B (corp-2) | 16 / 142 / 149 | 0 / 0 / 0 | 2 / 14 ✅ |
  | D-C (corp-3) | 58 / 552 / 566 | 1 / 15 / 25 | 6 / 55 ✅ |
  | D-D (corp-5) | 79 / 665 / 642 | 2 / 20 / 34 | 8 / 67 ✅ |
  | firm-2 | 2 / 17 / 34 | 0 / 0 / 24 | nie gorzej ✅ |

  Pozostałe wiersze „ \| ” i etykiety poza listą to głównie słowniczki (US3) i prawdziwe tabele. 5 dokumentów
  detalicznych i pozostałe dokumenty prywatne — bez zmian; goldeny `Corpus/acts`, `Corpus/banking`, korpus
  syntetyczny (`refresh`/`verify`) i `CorpusFull` — bez zmian. Zaktualizowane goldeny prywatne: D-A…D-D i firm-2
  (kopie wyjściowe w `Corpus/private/baseline-007/`). Otwarte: w firm-2 pytanie z lewej kolumny ramki („Potwierdzamy,
  że…”) wplata się w tekst elementu listy (było tak i przed zmianą); „a.”, „ii.” zostają tekstem (nie są etykietami
  listy).

- 2026-10-10 (US1, T016–T026 z T019a, T019b, T021a, T025a): goły „§ N” (`LegalUnitMatch.Bare`) jest jednostką tylko
  na wierszu wyróżnionym (wyśrodkowany w kolumnie albo na stronie z szerokimi marginesami, albo odosobniony i
  pogrubiony/powiększony); nagłówek to wiersz źródła („§ 5”, „§25” — bez dopisanej kropki ani spacji); ListDetection
  zamyka listę przed takim wierszem. „§ 3. Tytuł” wyśrodkowany i pogrubiony → jeden nagłówek (tylko „§”, nie artykuły
  — pogrubione artykuły tekstów jednolitych zostają jak były). Poziomy: numerowany rozdział „2. …” (typograficzny w
  dokumencie prawnym) jest rodzeństwem poprzedniego numerowanego rozdziału; jednostka pod nim (także pod nienumerowanym
  śródtytułem w nim) — poziom niżej. TableDetection (bez siatki): wiersz jednostki nie jest dołączany nad ziarnem, a
  region jest przed nim cięty; wiersz spisu treści z kropkami prowadzącymi nie jest wierszem jednostki.
  Miary po US1: „§ N” jako tekst 154/36/123/15 → 0/0/0/0 (SC-081); test `Category=Layout007` zielony.
  **Do akceptacji właściciela**: skutek uboczny w korpusie syntetycznym — `corpus/akty/dz-u-2019-1781-ochrona-danych`
  (tekst jednolity ustawy): tabela zastępcza z cytowanymi zmianami (ucięta na cytowanym „§ 4.”) staje się tekstem
  ciągłym; odświeżone `.md` i `.chunks.jsonl`, golden chunków zaktualizowany (bez utraty tekstu; `verify` czysty).
  Goldeny `Corpus/acts` i `Corpus/banking` bez zmian. Zaktualizowane goldeny prywatne D-A…D-D.
  Otwarte: w D-A fałszywy nagłówek „Rozdział I. (Prowadzenie…). Jest on załącznikiem” (odwołanie na początku
  zawiniętego wiersza; było już w punkcie odniesienia) przesuwa rozdziały 17–18 o poziom niżej; w D-A termin
  słowniczka „15/ umowa rachunku bankowego/” jako nagłówek (US3).

- 2026-10-10 (US4, T027–T029): po US1+US2 — 5 dokumentów detalicznych i pozostałe 6 dla firm (poza firm-2, którego
  miary się poprawiły: TBL001 2 → 0, wiersze „ \| ” 17 → 0, etykiety poza listą 34 → 24) bez zmian Markdown; goldeny
  aktów, `verify` i `CorpusFull` (Corpus 425, Chunking 111) zielone po każdym commicie parsera.

- 2026-10-10 (US3, T030–T036): `Stages/GlossaryDetection` (wywoływane przez TableDetection) — linie poziome dzielone na
  wspólnym x (≥ 2), brak pionowych, lewa strona każdego wpisu = etykieta („1/”, „1.”, „1)”) + pogrubione słowa; wpis =
  pas między liniami (pierwszy od wiersza nad pierwszą linią, który nie jest zdaniem wstępnym; ostatni do przerwy
  > 2 interlinii albo oznaczenia jednostki); pierwszy pas strony bez etykiety kontynuuje wpis z poprzedniej strony.
  Wiersze cięte na granicy kolumn, adnotacje `deflist.entry`/`deflist.side`, termin przed definicją (T034: kolejność
  ustala GlossaryDetection, ReadingOrder wyłącza te wiersze z wykrywania kolumn). ListDetection: termin nigdy nie
  jest nagłówkiem, wiersze wpisu idą razem mimo odstępów; `KeepLabelledBoldTextInLists` obejmuje „1/”, „a/”.
  Miary końcowe (TBL001 / wiersze „ \| ” / etykiety poza listą / „§ N” jako tekst):

  | Dokument | Punkt odniesienia | Po US2+US1+US3 |
  |---|---|---|
  | D-A (corp-1) | 132 / 1137 / 1155 / 154 | 5 / 25 / 6 / 0 |
  | D-B (corp-2) | 16 / 142 / 149 / 36 | 0 / 0 / 0 / 0 |
  | D-C (corp-3) | 58 / 552 / 566 / 123 | 0 / 0 / 0 / 0 |
  | D-D (corp-5) | 79 / 665 / 642 / 15 | 0 / 0 / 0 / 0 |

  SC-086: słowniczki D-A (s. 4, 20–21 i w załączniku), D-B, D-C, D-D — każda definicja to jeden element
  „- 1/ **termin** definicja”, wyliczenia zagnieżdżone, część wspólna po wyliczeniu jako akapit elementu. Pozostałe
  wiersze „ \| ” w D-A to prawdziwe tabele (s. 38, 47–48). Kontrola tekstu (zbiór słów goldenów przed/po): bez utraty.
  Pozostałe 15 dokumentów prywatnych, goldeny aktów/bankowe, `verify`, `CorpusFull` — bez zmian.

- 2026-10-10 (US5, Polish): T037 — wpisy spisu treści z kropkami prowadzącymi nie są nagłówkami (D-B: 9 zdublowanych
  nagłówków rozdziałów zniknęło). **T038 (stopki „N/M”) nie zrobione** — zmieniłoby dokument detaliczny ind-2
  (35 wystąpień), więc wymaga decyzji właściciela. T039/T040 — README parsera i CLAUDE.md. **T041 nie wykonane** —
  przebieg FAQ wymaga klucza Azure właściciela (`quickstart.md` §4); offline: każdy „§ N” D-A…D-D jest nagłówkiem
  jednostki, więc `UnitMatcher` je znajdzie. Testy wydajności (SC-007) zielone.
  Stan końcowy: wszystkie testy solucji zielone (2002), prywatny korpus 19/19 z `Category=Layout007`, `verify` czysty,
  `CorpusFull` zielony. Do decyzji właściciela: (1) zmiana w korpusie syntetycznym `dz-u-2019-1781-ochrona-danych`
  (US1/T025), (2) T038, (3) otwarte punkty z US1/US2 (fałszywy „Rozdział I.” w D-A, pytania ramek w firm-2,
  „a.”/„ii.” jako tekst). Gałąź niewypchnięta od 8020df5.
- **2026-10-10 — decyzje właściciela.** (1) Zmiana w korpusie syntetycznym `dz-u-2019-1781-ochrona-danych`
  **zaakceptowana** (tabela zastępcza z cytowanymi zmianami → tekst ciągły, `.md`, `.chunks.jsonl` i golden chunków
  jak w commicie 68202eb). Gałąź wypchnięta do PR. T038, T041 i otwarte punkty — po merge'u.
- **2026-10-10 — T041 (SC-083) i T041a.** Przebieg FAQ właściciela (gpt-4.1-mini) na 5 dokumentach dla firm
  (D-A, D-B, D-C, D-D + załącznik). Pierwszy przebieg szedł na starym `.exe` (zbudowanym przed spec 007; TBL001
  132/58/79/16) — odrzucenia „jednostka nie występuje” w D1, D2, D3 („§ 5.”), wpis spisu treści jako jednostka w D4.
  Po przebudowie: konwersja TBL001 5/0/0/0, **zero odrzuceń „jednostka … nie występuje”** (SC-083 spełnione),
  kandydaci 26 → 38, FAQ 10/10. D3 przekroczyło `MaxOutputTokens` 8192 (8571 tokenów, nie pętla) — przebieg
  z `FAQGEN__AzureOpenAI__MaxOutputTokens=16384`. Pozostałe odrzucenia cytatów: etykiety „1/”, „a/” nie były
  pomijane przy porównaniu (D-B „§ 10”, „§ 12”, „§ 36”, D-C „§ 42”) — T041a: `FaqGrounding.ListLabel` obejmuje
  etykiety ukośnikowe. Zostają cytaty, w które model wplata tekst nagłówka („§ 5 System…”, „III. PŁATNOŚCI…”). T041b (decyzja właściciela): domyślne
  `AzureOpenAI:MaxOutputTokens` 16384 (`AppSettings`, `appsettings.json`, README, kontrakt CLI spec 006).

## Complexity Tracking

Brak naruszeń konstytucji — sekcja pusta.
