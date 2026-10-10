# Tasks: Regulaminy z etykietami w wysuniętej kolumnie i paragrafami „§ N”

**Input**: Design documents from `specs/007-corporate-regulation-layout/` (plan, spec, research, data-model,
contracts/markdown-output.md, quickstart)

**Tests**: wymagane — konstytucja I (TDD): każda zmiana zachowania to osobny commit **red** (test pada) i **green**
(kod). Testy offline; prawdziwe PDF-y tylko w prywatnym korpusie (`LEGALAGENT_PRIVATE_CORPUS`, poza git).

**Zasady dla każdego commita zmieniającego parser (FR-534)**: przed commitem green uruchom pełny zestaw z
`quickstart.md` §2 (testy solucji, prywatny korpus z raportem miar, `refresh` + `verify`, `CorpusFull`). Goldeny
`Corpus/acts`, `Corpus/banking` i 5 dokumentów detalicznych z prywatnego korpusu — zmiana tylko za zgodą właściciela
(różnicę pokazać, zapisać w handoffie). Gałąź sprawdzać osobnym poleceniem przed każdym commitem.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: można równolegle (inne pliki, brak zależności)
- Etykiety historii: US1 (§ N), US2 (etykiety), US3 (słowniczek), US4 (regresja), US5 (opcjonalne)

---

## Phase 1: Setup (punkt odniesienia)

- [X] T001 Skopiuj 15 PDF-ów z `publish/mBank.FaqGenerator-win-x64/{ind,corp,downloads}` do
  `tests/LegalAgent.PdfParser.Tests/Corpus/private/` pod krótkimi nazwami `mbank-ind-1…5.pdf`, `mbank-corp-1…5.pdf`,
  `mbank-firm-1…5.pdf` (nazwy plików < 60 znaków; katalog jest poza git — sprawdź `git status`, że nic nie jest
  śledzone)
- [X] T002 Wygeneruj goldeny obecnego wyniku `mbank-*.expected.md` dla 15 dokumentów (CLI `legalagent-pdf convert`
  albo `UPDATE_GOLDEN=1` w `PrivateCorpusTests`) i uruchom prywatny korpus — wszystko zielone (punkt odniesienia)
- [X] T003 Utwórz nieśledzony plik `tests/LegalAgent.PdfParser.Tests/Corpus/private/layout-labels.txt` z nazwami
  dokumentów „układu etykiet” (D-A = zintegrowany rachunek, D-B = polecenie zapłaty, D-C = usługi gotówkowe, D-D =
  zasady współpracy) i progami SC-080 w formacie `nazwa;maxTbl001;maxPipeRows` (D-A 13;114, D-B 2;14, D-C 6;55, D-D
  8;67)

---

## Phase 2: Foundational (pomiar — blokuje wszystkie historie)

- [X] T004 [P] Test (red) pomocnika miar na krótkich ciągach Markdown i raporcie: `Tbl001`, `PipeRows`
  (wiersze z „ \| ”), `LooseLabelRows` (wiersz zaczynający się „N.”, „N/”, „x/”, także pogrubiony, poza elementem
  listy), `ParagraphText` (samodzielny wiersz „§ N” opcjonalnie pogrubiony, z tytułem, poza nagłówkiem),
  `TocHeadings`, `PageFooters` („N/M”) w `tests/LegalAgent.PdfParser.Tests/Unit/Fixtures/LayoutMetricsTests.cs`
- [X] T005 Pomocnik `LayoutMetrics` (green) w `tests/LegalAgent.PdfParser.Tests/Fixtures/LayoutMetrics.cs`
- [X] T006 Raport miar w `tests/LegalAgent.PdfParser.Tests/Corpus/PrivateCorpusTests.cs`: dla każdego dokumentu
  wypisz miary przy `LEGALAGENT_CORPUS_REPORT`; dla dokumentów z `layout-labels.txt` sprawdzaj progi SC-080 i
  `ParagraphText == 0` w osobnym teście z cechą `Category=Layout007` (dziś czerwony — oczekiwane do końca US1/US2;
  pozostałe testy prywatnego korpusu zielone). Zapisz wartości wyjściowe w handoffie planu

**Checkpoint**: punkt odniesienia i pomiar gotowe — zmiany parsera mogą się zaczynać.

---

## Phase 3: User Story 2 — Etykiety „1/”, „a/” jako listy (Priority: P1) 🎯 MVP

**Goal**: ustępy „1.”, punkty „1/” i litery „a/” z etykietą w wysuniętej kolumnie są zagnieżdżonymi elementami list,
bez TBL001 i bez podziału zawiniętych wierszy (FR-510…FR-515).

**Independent Test**: repliki stron z `research.md` R1 (geometria: tekst 7 pt, interlinia 10, ustęp x 39,7/53,9,
punkt 53,9/68,0, litera 68,0/82,2) dają listy z kontraktu `contracts/markdown-output.md`; kontrola: tabela danych z „1/”
w pierwszej kolumnie zostaje tabelą GFM.

- [X] T007 [P] [US2] Testy (red) wzorców w `tests/LegalAgent.PdfParser.Tests/Unit/Text/ListLabelPatternsTests.cs`:
  „1/”, „12/”, „1a/” → `ArabicSlash`; „a/”, „aa/” → `LetterSlash` (cały token 1–3 cyfry z opcjonalną literą albo 1–2
  małe litery + „/”, po nim tekst); NIE etykiety: „7/2017”, „13/36”, „4/49”, „Klient/Klienci”, „km/h”, samotne „i/”,
  token bez tekstu po nim
- [X] T008 [US2] Wartości `ArabicSlash`, `LetterSlash` w `src/LegalAgent.PdfParser/Model/ListItem.cs` (XML-doc) i
  wzorce w `src/LegalAgent.PdfParser/Text/ListLabelPatterns.cs` (green T007)
- [X] T009 [P] [US2] Replika (red) „zagnieżdżone 1/”: ustęp „2.” (etykieta x 40, tekst 54) z punktami „1/”, „2/” (x 54 /
  68), drugi punkt zawinięty do x 68, potem ustęp „3.”; oczekiwane `- 2\.` z `  - 1/`, `  - 2/` (scalona kontynuacja),
  `- 3\.`, brak TBL001 — w `tests/LegalAgent.PdfParser.Tests/Integration/HangingLabelLayoutTests.cs`
- [X] T010 [P] [US2] Replika (red) „trzy poziomy i część wspólna”: „1.” → „1/” (tekst kończy się „:”) → „a/”, „b/” (x 68 /
  82), potem wiersz bez etykiety na x 54 (część wspólna ustępu); oraz kontynuacja punktu na następnej stronie — w tym
  samym pliku testów
- [X] T011 [P] [US2] Test kontrolny (powinien przejść już dziś i po zmianie): tabela opłat z 3 kolumnami tekstu i
  „1/” w pierwszej kolumnie zostaje tabelą GFM — w `HangingLabelLayoutTests.cs`
- [X] T012 [US2] `TableDetectionStage.IsLabel`/`CellsOf` łączą etykiety `ArabicSlash`, `LetterSlash` i (tylko w
  obszarze etykiet) `ArabicDot` z tekstem; `KeepLabelledBoldTextInLists` obejmuje nowe rodzaje — w
  `src/LegalAgent.PdfParser/Stages/TableDetectionStage.cs`
- [X] T013 [US2] `ListDetectionStage.Rank`: `ArabicSlash` = 2, `LetterSlash` = 3 — w
  `src/LegalAgent.PdfParser/Stages/ListDetectionStage.cs`; sprawdź `MarkdownEscaper.EscapeListLabel` (etykieta „1/”
  bez ucieczki) w `src/LegalAgent.PdfParser/Rendering/MarkdownEscaper.cs`
- [X] T013a [US2] Replika (red) z D-A s. 24: ustępy „1.” cytowane pod punktem „1/” zagnieżdżają się według kolumny
  etykiet; zewnętrzne „1.”, „2.” zostają ciągiem (FR-051: najbliższy kandydat „N.” na tym samym wcięciu, z pominięciem
  zagnieżdżonych). Decyzja do T013: `Rank` dla `ArabicSlash`/`LetterSlash` NIE został dodany — ranga 1 („1.”) pod
  rangą 2 („1/”) zamykałaby rodzica; zagnieżdżenie wynika z wcięcia. „1/” renderowane bez ucieczki (sprawdzone)
- [X] T014 [US2] Jeśli T009/T010 nadal pokazują trzy kolumny: uogólnij `IsHangingList` na „każda kolumna poza ostatnią
  zawiera wyłącznie etykiety” w `TableDetectionStage.cs` (green T009–T010; T011 nadal zielony)
- [X] T012a [US2] Replika (kontrola regresji): długi ciąg ustępów „9.”–„14.” z dwucyfrowymi etykietami, potem punkty
  „1/” i litery „a/” — jedna lista, bez tabeli (przechodzi po T008) — w `HangingLabelLayoutTests.cs`
- [X] T014a [US2] Replika (red) z D-D s. 8: wyliczenie „a.”, „b.”, „c.” (litera z kropką) w kolumnie etykiet ustępu
  nie jest tabelą zastępczą; `IsHangingList`: każda komórka poza ostatnią to samotna etykieta (także „a.”), dowolna
  liczba pasm (green razem z T014)
- [X] T014b [US2] Replika (red) z D-A s. 43: wyliczenie „i.”, „ii.”, „iii.” (rzymskie z kropką) pod literą „a/” nie
  jest tabelą zastępczą; `IsHangingLabel` przyjmuje małe rzymskie z kropką (green)
- [X] T015 [US2] Pełna kontrola regresji (FR-534), miary D-A…D-D (oczekiwane ≈ TBL001 6/0/1/2), przegląd różnic
  prywatnego korpusu: 5 detalicznych — pokaż właścicielowi; 6 pozostałych dla firm — miary nie gorsze; zaktualizuj
  goldeny D-A…D-D po akceptacji; commit `refresh` korpusu, jeśli się zmienił

**Checkpoint**: SC-080 i SC-082 spełnione; US2 działa samodzielnie.

---

## Phase 4: User Story 1 — Paragrafy „§ N” jako jednostki (Priority: P1)

**Goal**: każdy samodzielny wyróżniony wiersz „§ N” (także z tytułem) jest nagłówkiem jednostki o poziom niżej niż
numerowany rozdział (FR-500…FR-502). Każda przyczyna z research R2 (C1–C5) — osobna para red/green.

**Independent Test**: repliki R1a–R1d (tekst 7 pt, rozdział pogrubiony 9 pt x 40, „§” pogrubiony 9 pt wyśrodkowany
x 292–304, 24 pt pod rozdziałem) w `tests/LegalAgent.PdfParser.Tests/Integration/ParagraphUnitHeadingTests.cs`.

- [X] T016 [P] [US1] Testy (red) wzorca: goły wiersz „§ 5” (cały wiersz) pasuje jako jednostka; „§ 5 ust. 2”, „§ 5,”
  i „zgodnie z § 5” nie — w `tests/LegalAgent.PdfParser.Tests/Unit/Text/LegalUnitPatternsTests.cs`
- [X] T017 [US1] `LegalUnitPatterns.TryMatch` przyjmuje goły wiersz `^§\s*N$` (artykuły bez zmian) w
  `src/LegalAgent.PdfParser/Text/LegalUnitPatterns.cs` (green T016)
- [X] T018 [P] [US1] Replika R1a (red): rozdział „2. Rachunki bankowe oraz rachunek VAT”, pod nim wyśrodkowany „§ 5”,
  potem ustęp „1.” z punktem „1/”; oczekiwane `#### § 5` pod `### 2. …` i lista; negatyw R1d: „§ 5 ust. 2” na początku
  zawiniętego wiersza i samotne „§ 5” zawinięte ze zdania — bez nagłówka
- [X] T019 [US1] HeadingDetection uznaje goły „§ N” tylko, gdy wiersz jest odosobniony i wyróżniony (wyśrodkowany,
  pogrubiony lub powiększony) i nie kontynuuje zdania; ListDetection zamyka listę przed takim wierszem — w
  `src/LegalAgent.PdfParser/Stages/HeadingDetectionStage.cs` i `ListDetectionStage.cs` (green R1a bez poziomu, R1d)
- [X] T019a [US1] Replika (red) z D-B: wyśrodkowany pogrubiony „§ N” w interlinii tekstu tuż pod elementem listy
  i tuż pod nagłówkiem rozdziału jest nagłówkiem jednostki; wyśrodkowanie (także względem strony, gdy kolumna nie
  wynika z wierszy zwykłego tekstu) wystarcza; ListDetection zamyka listę przed takim wierszem (green)
- [X] T020 [P] [US1] Test (red) poziomu: jednostka pod otwartym nagłówkiem typograficznym `^\d+\.\s+\p{Lu}` (numerowany
  rozdział) dostaje poziom rozdziału + 1; pod nienumerowanym rodzicem — bez zmian (np. „A. Banki państwowe” → Art.) —
  w `tests/LegalAgent.PdfParser.Tests/Unit/Stages/HeadingDetectionStageTests.cs`
- [X] T021 [US1] Poziom w `AssignLevels` (green T020, R1a w pełni) w `HeadingDetectionStage.cs`
- [X] T021a [US1] Replika (red) z D-C: nienumerowany śródtytuł (9 pt, zwykły) w numerowanym rozdziale nie zamyka
  rozdziału; jego jednostki stoją poziom niżej niż śródtytuł; następny numerowany rozdział jest rodzeństwem (green)
- [ ] T022 [P] [US1] Replika R1c (red): wyśrodkowany pogrubiony „§ 3. Porady ogólne” nad obszarem etykiet → jeden
  nagłówek z całym wierszem, oznaczenie „§ 3”, tytuł nie trafia do treści
- [ ] T023 [US1] `LegalHeading` bez podziału reszty, gdy oznaczenie i reszta są pogrubione, tekst ciągły nie, a wiersz
  jest odosobniony lub wyśrodkowany (green T022) w `HeadingDetectionStage.cs`
- [ ] T024 [P] [US1] Replika R1b (red): „§ 5” bezpośrednio nad ≥ 3 dwukomórkowymi wierszami bez siatki, które zostają
  tabelą (np. tabela danych) → „§ 5” nie jest wierszem tabeli
- [ ] T025 [US1] Osłona w `TableDetectionStage.cs`: w regionach bez siatki wiersz pasujący do oznaczenia jednostki
  (także gołego) nie jest dołączany nad ziarnem, a region jest przed nim cięty; siatki i tabele-dokumenty bez zmian
  (green T024)
- [ ] T026 [US1] Pełna kontrola regresji (FR-534); miara `ParagraphText` = 0 w D-A…D-D (SC-081); goldeny aktów bez
  zmian (SC-087); przegląd różnic i aktualizacja goldenów D-A…D-D po akceptacji

**Checkpoint**: US1 i US2 razem — test `Category=Layout007` zielony.

---

## Phase 5: User Story 4 — Bez regresji (Priority: P1, ciągle)

**Goal**: SC-084, SC-085, SC-087 potwierdzone po US1+US2 (i ponownie po US3).

- [ ] T027 [US4] Raport miar dla 15 dokumentów (`LEGALAGENT_CORPUS_REPORT=1`) porównany z wartościami wyjściowymi z
  T006; tabela różnic w handoffie `specs/007-corporate-regulation-layout/plan.md`
- [ ] T028 [US4] Różnice Markdown 5 dokumentów detalicznych pokazane właścicielowi; po akceptacji aktualizacja ich
  goldenów prywatnych (albo poprawka parsera, jeśli różnica jest regresją)
- [ ] T029 [US4] `LEGALAGENT_CORPUS_FULL=1 dotnet test tests/LegalAgent.Corpus.Tests` i
  `tests/LegalAgent.Chunking.Tests` (`CorpusChunksFullTests`) zielone; wynik w handoffie

---

## Phase 6: User Story 3 — Słowniczek (Priority: P2)

**Goal**: każda definicja to jeden element `- 1/ **termin** definicja…` z zagnieżdżonymi wyliczeniami (FR-520,
FR-521, clarify Q1).

**Independent Test**: replika R3 (zdanie wstępne x 39,7 przecinające granicę 181,4; etykieta pogrubiona 45,4, termin
pogrubiony 59,5 wyśrodkowany pionowo; definicja 187,1, 5 wierszy z „a/”, „b/”, „c/” na 187,1/201,3; linie poziome
dzielone 39,7–181,4 i 181,4–555,6 pod każdym wpisem) w
`tests/LegalAgent.PdfParser.Tests/Integration/GlossaryLayoutTests.cs`.

- [ ] T030 [P] [US3] Replika R3 (red) z trzema wpisami (wyliczenie w definicji, krótki wpis „2/ Bank”, zawinięty termin)
  i wariant z etykietami „1.”; oczekiwany wynik z `contracts/markdown-output.md` (słowniczek), bez TBL001
- [ ] T031 [P] [US3] Test kontrolny: taryfa z kolumną „Lp.” („1.”, „2.”) i siatką pozostaje tabelą GFM
- [ ] T032 [US3] Adnotacje `deflist.entry` (numer wpisu od 1) i `deflist.side` (`term` / `definition`) w
  `src/LegalAgent.PdfParser/Layout/LayoutAnnotations.cs`
- [ ] T033 [US3] Rozpoznanie słowniczka w `TableDetectionStage.cs`: region bez linii pionowych z ≥ 2 liniami
  poziomymi dzielonymi na wspólnym x, lewa strona każdego wpisu = etykieta (`ArabicSlash`, `ArabicDot`,
  `ArabicParen`) + pogrubiony termin; wpis = pas między liniami, pierwszy od wiersza przecinającego granicę kolumn
  (albo poprzedzającego nagłówka), strona bez górnej linii kontynuuje wpis; wiersze dostają adnotacje zamiast roli
  `Table`
- [ ] T034 [US3] Kolejność w `src/LegalAgent.PdfParser/Stages/ReadingOrderStage.cs`: wiersze `term` wpisu, potem
  `definition` w kolejności y
- [ ] T035 [US3] Budowa elementu w `ListDetectionStage.cs`: etykieta, pogrubiony termin (zawinięcia złączone), spacja,
  definicja; wyliczenia definicji zagnieżdżone (logika US2) (green T030, T031)
- [ ] T036 [US3] Pełna kontrola regresji (FR-534); SC-086 sprawdzone na słowniczkach D-A…D-D (D-A ma dwa: s. 4 i
  20–21); przegląd różnic i aktualizacja goldenów D-A…D-D po akceptacji

**Checkpoint**: US3 działa; wszystkie kryteria SC-080…SC-087 spełnione.

---

## Phase 7: User Story 5 — Drobne artefakty (Priority: P3, opcjonalnie — tylko jeśli dzień 8 pozwala)

- [ ] T037 [P] [US5] Replika (red) i poprawka: wiersze spisu treści z kropkami prowadzącymi i numerem strony nie są
  nagłówkami (FR-540) — `HeadingDetectionStage.cs`, test w `HeadingsIntegrationTests.cs`
- [ ] T038 [P] [US5] Replika (red) i poprawka: stopka „N/M” nie zostaje w tekście (FR-541) — `ArtifactRemovalStage`,
  test w `ArtifactCleanupIntegrationTests.cs`

---

## Phase 8: Polish & Cross-Cutting

- [ ] T039 [P] README parsera `src/LegalAgent.PdfParser/README.md`: etykiety „1/”, „a/”, paragrafy „§ N”, słowniczek,
  zaktualizowane „Znane ograniczenia”
- [ ] T040 [P] `CLAUDE.md`: nowe adnotacje `deflist.*` w opisie komunikacji etapów; kontrakt 1.2.0
- [ ] T041 Przebieg FAQ na D-A…D-D (`quickstart.md` §4): brak odrzuceń „jednostka „§ N” nie występuje” (SC-083); wynik
  w handoffie
- [ ] T042 Handoff „Stan prac i przekazanie” w `plan.md`: zrobione zadania, miary przed/po, różnice goldenów i decyzje
  właściciela, otwarte punkty; aktualizacja pamięci projektu

---

## Dependencies & Execution Order

- **Setup (T001–T003)** → **Foundational (T004–T006)** → historie.
- **US2 (T007–T015)** najpierw (research R5): usuwa tabele zastępcze, które pochłaniają też wiersze „§”.
- **US1 (T016–T026)** po US2; wewnątrz: C1 (T016–T019) → poziom (T020–T021) → tytuł (T022–T023) → osłona tabel
  (T024–T025).
- **US4 (T027–T029)** po US1+US2 i ponownie po US3.
- **US3 (T030–T036)** po US2 (korzysta z etykiet ukośnikowych i zagnieżdżenia).
- **US5** opcjonalnie; **Polish** na końcu (dzień 8). Dni 9–10: zamrożenie zmian parsera.

### Parallel Opportunities

- T004 równolegle z T001–T003.
- US2: T007, T009, T010, T011 (różne pliki testów) równolegle; T012–T014 sekwencyjnie (ten sam plik etapu).
- US1: testy red T016, T018, T020, T022, T024 można pisać równolegle; zielone w kolejności C1 → poziom → tytuł →
  osłona.
- US3: T030 i T031 równolegle.
- Polish: T039 i T040 równolegle.

## Parallel Example: User Story 2

```text
T007 ListLabelPatternsTests.cs   (wzorce „1/”, „a/”)
T009 HangingLabelLayoutTests.cs  (replika: zagnieżdżone 1/)
T011 HangingLabelLayoutTests.cs  (kontrola: tabela danych z „1/”) — po T009 w tym samym pliku
```

## Implementation Strategy

### MVP (dni 3–5)

1. Setup + pomiar (T001–T006).
2. US2 (T007–T015) → STOP i walidacja: TBL001 w D-A…D-D na poziomie 6/0/1/2, goldeny aktów bez zmian. Już to
   radykalnie poprawia dokumenty dla firm.

### Incremental Delivery

1. US1 (dni 5–6) → nagłówki „§” dla RAG i FAQ.
2. US4 przegląd regresji (ciągle, raport po każdej historii).
3. US3 (dni 6–8) → słowniczek.
4. US5 tylko przy zapasie czasu; Polish dzień 8; zamrożenie dni 9–10.

## Notes

- Nowe poprawki w trakcie dopisuj jako T0xxa (np. T019a) i do handoffu.
- Prawdziwe dokumenty i ich nazwy nigdy w commitach; repliki stron wyłącznie syntetyczne, bez tekstu z PDF-ów banku.
- Goldeny parsera i 5 detalicznych dokumentów prywatnych — zmiana tylko za zgodą właściciela (FR-163, SC-085).
