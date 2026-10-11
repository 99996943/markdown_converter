# Tasks: Poprawki parsera po przeglądzie liczb w regulaminach (indeks górny po „art.”, stopka z logo, kontrola liczb)

**Input**: Design documents from `specs/009-number-review-fixes/` (plan, spec, research, data-model,
contracts/number-check.md, quickstart)

**Tests**: wymagane — konstytucja I (TDD) i FR-672: każda zmiana zachowania to osobny commit **red** (test pada na
kodzie sprzed poprawki — sprawdzić uruchomieniem) i **green** (kod). Testy offline; repliki stron przez
`SyntheticPdfBuilder` (`src/LegalAgent.Corpus/Pdf/SyntheticPdfBuilder.cs`), bez nazw prawdziwych banków (logo w
replice: neutralny napis, np. „Bank.pl”); prawdziwe PDF-y tylko w prywatnym korpusie (`LEGALAGENT_PRIVATE_CORPUS`,
poza git).

**Zasady dla każdego commita zmieniającego parser (FR-673)**: przed commitem green uruchom pełny zestaw z
`quickstart.md` (§1 testy solucji, §2 prywatny korpus z raportem, §4 `refresh` + `verify` + `CorpusFull`).
Goldeny (FR-674):
- corp-1…6, firm-1…6 — wykonawca przegląda diff i aktualizuje, jeśli żadna miara się nie pogarsza i zbiory słów są
  czyste; oczekiwane tylko corp-5 (US1) i stopki corp-1, 2, 3, 5, 6 (US3), ewentualnie schematy firm-2, 5, 6 (US3);
- `Corpus/acts`, golden fragmentów `tests/LegalAgent.Chunking.Tests/Golden/dz-u-2019-1781-ochrona-danych.chunks.jsonl`,
  `corpus/akty` — **wąska zgoda**: wyłącznie „art. N” + cyfry → znaki indeksu górnego, zniknięcie fałszywego `[^n]` w
  tym miejscu, wynikające przenumerowanie przypisów i pola fragmentów zależne od tekstu; diff w handoffie;
- każda inna zmiana (ind-*, reg*, regulamin-pdp, `Corpus/banking`, inne goldeny fragmentów, inne pliki `corpus/`,
  albo inna różnica w plikach z wąskiej zgody) — **stop i pytanie do właściciela**.

Przed każdym commitem: `git branch --show-current` osobnym poleceniem (nigdy `main`); stage'uj tylko jawne ścieżki
(nigdy `git add -A`, bez `git stash -u`); trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: można równolegle (inne pliki, brak zależności)
- Etykiety historii: US1 (indeks po „art.”), US2 (kontrola liczb), US3 (stopka z logo), US4 (regresja i wydanie)

---

## Phase 1: Setup (punkt odniesienia)

- [X] T001 Skopiuj bieżące goldeny `tests/LegalAgent.PdfParser.Tests/Corpus/private/*.expected.md` (21 dokumentów,
  stan po PR #11, wersja 1.2.0) do nieśledzonego `tests/LegalAgent.PdfParser.Tests/Corpus/private/baseline-009/`;
  sprawdź `git status`, że nic z `Corpus/private` nie jest śledzone, i że prywatny korpus jest zielony na starcie
- [X] T002 Utwórz nieśledzony `tests/LegalAgent.PdfParser.Tests/Corpus/private/number-check.txt` według
  `contracts/number-check.md`: nagłówek-komentarz i 15 regulaminów z przeglądu liczb (wszystkie dokumenty prywatne
  z zestawów FAQ: `mbank-corp-1`, `mbank-corp-2`, `mbank-corp-3`, `mbank-corp-5`, `mbank-corp-6`, `mbank-ind-1` …
  `mbank-ind-5`, `mbank-firm-2` … `mbank-firm-6`; bez `mbank-firm-1`, `mbank-corp-4` oraz reg1–3 i regulamin-pdp,
  które są poza zestawami FAQ i tylko raportowane — FR-658); bez pól wyjątków; listę zapisz w handoffie spec 009

---

## Phase 2: Foundational

Brak osobnych zadań blokujących: pomiar (US2) jest pierwszą historią, bo daje stan wyjściowy i czerwoną bramkę
prywatną dla US1 i US3.

---

## Phase 3: User Story 2 — Stała kontrola liczb w testach (Priority: P1) 🎯 MVP

**Goal**: miara liczb PDF vs Markdown w pomocniku i raporcie; bramka na liczbach nadmiarowych regulaminów z listy.

**Independent Test**: `NumberCheckTests` na syntetycznym PDF (poprawny indeks → 0 rozbieżności, sklejony →
nadmiarowa); na prywatnym korpusie miara wykrywa corp-5 661/682 i firm-1 150.000/230.000/320.000/450.000.

- [ ] T003 [US2] Testy (red) w nowym `tests/LegalAgent.PdfParser.Tests/Unit/Fixtures/NumberCheckTests.cs`:
  - `FromMarkdown`: liczby `[0-9]+(?:[.,][0-9]+)*`; pomija `<!-- page: 3 -->`, `[^2]`, `[^2]:` na początku wiersza,
    ukośniki ucieczki (`- 1\)` → „1”), `**150**` → „150”; „66¹” → tylko „66”; „170 103 364” → trzy liczby;
    „01.01.2022” i „150.000” jako jedna liczba;
  - `FromPdf` na PDF z `SyntheticPdfBuilder` (tekst 8 pt): „art. 66” + podniesiona „1” 4 pt → liczby {66}, bez „1” i
    bez „661”; stopka „2/3” na stronach 1–3 usunięta przez parser (`ArtifactSummary` „#/#”) → nie jest liczona;
  - `Compare`: Markdown „art. 66¹” vs ten PDF → `Excess` i `Missing` puste; Markdown „art. 661” → `Excess` = {661×1},
    `Missing` = {66×1}; dokument bez warstwy tekstowej → `Skipped` niepuste;
  - `RegulationGate.Evaluate(listText, diffs)` (czysta funkcja logiki bramki, SC-102): lista
    `mbank-a
mbank-b;0,0
# komentarz` + `NumberDiff` z nadmiarowymi → porażki tylko dla dokumentów z listy, minus
    dopuszczone liczby (każde wystąpienie na liście dopuszcza jedno nadmiarowe), komunikat
    `mbank-a: nadmiarowe 661×1, 682×1`; dokument z listy bez wyniku → porażka „no such PDF”; same brakujące → brak
    porażki
  Sprawdź, że testy nie kompilują się / padają przed T004
- [ ] T004 [US2] Pomocnik (green T003) w nowym `tests/LegalAgent.PdfParser.Tests/Fixtures/NumberCheck.cs` (z klasą
  `RegulationGate` — parsowanie `number-check.txt` według `contracts/number-check.md` i ocena porażek) według
  `data-model.md`: `NumberDiff(Excess, Missing, PdfCount, MarkdownCount, Skipped)` z wielozbiorami
  `IReadOnlyDictionary<string,int>`; strona PDF — litery PdfPig w kolejności strumienia, podział tokenów na białym
  znaku, przerwie > 0,15 em, zmianie linii bazowej > 0,2 × rozmiar i przejściu do/od cyfry indeksu („≤ 0,8 rozmiaru
  poprzedniej pełnej litery i podniesiona ≥ 0,15 jej rozmiaru — nie jest liczbą”); wiersze surowe z
  `LineFingerprint.Compute` równym `ArtifactSummary.Pattern` na stronie z `Pages` pomijane (jeśli `LineFingerprint`
  jest niedostępny z testów, sprawdź `InternalsVisibleTo`; nie kopiuj logiki); klucz liczby bez normalizacji
  separatorów
- [ ] T005 [US2] `tests/LegalAgent.PdfParser.Tests/Fixtures/LayoutMetrics.cs`: pola `NumbersExcess`,
  `NumbersMissing` w `LayoutMeasures` (sumy krotności), wypełniane przeciążeniem `Measure(markdown, report, pdfPath)`;
  test w `tests/LegalAgent.PdfParser.Tests/Unit/Fixtures/LayoutMetricsTests.cs` (red → green w jednym kroku testów
  pomocniczych, commit jako test pomocnika)
- [ ] T006 [US2] Raport w `tests/LegalAgent.PdfParser.Tests/Corpus/PrivateCorpusTests.cs`: kolumny `Liczby +` i
  `Liczby −` w tabeli miar, sekcja `## Liczby nadmiarowe i brakujące` (dokumenty z niezerową miarą, wartości z
  krotnościami, format `+ 661×1, 682×1; − 66×2`), `BaselineDirectory` = `baseline-009` (sekcja zbiorów słów vs
  `baseline-009`); zaktualizuj komentarz `<remarks>` klasy (spec 009)
- [ ] T007 [US2] Uruchom raport (`quickstart.md` §2) i sprawdź znane przypadki: corp-5 → nadmiarowe 661, 682;
  firm-1 → 150.000, 230.000, 320.000, 450.000; corp-2 → cyfry przeplecionej daty. Jeśli regulamin z listy ma inną
  nadmiarową liczbę (fałszywy alarm), popraw tokenizację w `NumberCheck.cs` (nie listę) z testem w
  `NumberCheckTests.cs` i opisz przypadek. Zapisz pomiar wyjściowy (liczby + / − dla każdego dokumentu z niezerową
  miarą) w handoffie `specs/009-number-review-fixes/plan.md`, „Stan prac i przekazanie”; dla regulaminów z listy
  sklasyfikuj każdą liczbę brakującą (etykieta definicji przypisu, duplikat warstwy tekstowej, rozbity wiersz, inne —
  „inne” sprawdź w PDF, czy tekst nie zginął; utrata tekstu → nowe zadanie T0xxa i pytanie do właściciela) (SC-101)
- [ ] T008 [US2] Bramka (red na prywatnym korpusie) `RegulationNumbers_HaveNoExcess` w
  `tests/LegalAgent.PdfParser.Tests/Corpus/PrivateCorpusTests.cs` według `contracts/number-check.md`: brak pliku
  `number-check.txt` → `Assert.SkipUnless`; poza tym tylko wywołuje `RegulationGate.Evaluate` (logika przetestowana
  offline w T003) i asertuje brak porażek. Sprawdź, że pada na corp-5 i corp-2; commit jako red (opis: zielony po US1 i US3)

**Checkpoint**: miara i bramka gotowe, stan wyjściowy zmierzony.

---

## Phase 4: User Story 1 — Numery artykułów z indeksem górnym (Priority: P1)

**Goal**: „art. 66¹”, „art. 68²” (i 48 miejsc w aktach) zamiast sklejonych cyfr lub fałszywych `[^n]`.

**Independent Test**: replika s. 8 corp-5 daje „art. 66¹ § 1 do § 3 oraz art. 68²”; „Art.”, „§”, przypisy po słowach
i zwykłe liczby bez zmian.

- [ ] T009 [P] [US1] Testy etapu (red) w `tests/LegalAgent.PdfParser.Tests/Unit/Stages/TextNormalizationStageTests.cs`:
  „art. 66” + podniesiona mała „1” → „¹”; „art. 68” + „2” → „²”; wielocyfrowy „art. 479” + „1”, „1”, „5” → „¹¹⁵”;
  „Art. 5” + „2” i „§ 7” + „1” jak dotąd; „kart. 5” + mała podniesiona „1” → cyfra bez zmian (lookbehind
  `(?<!\p{L})`); „art. 66” + „1” tej samej wielkości → „1”; „umowy” + mała podniesiona „3” → „3” (dla
  `FootnoteDetection`)
- [ ] T010 [P] [US1] Replika strony (red) w `tests/LegalAgent.PdfParser.Tests/Integration/CorpusLayoutsIntegrationTests.cs`:
  tekst 8 pt „… postanowień art. 66¹ § 1 do § 3 oraz art. 68² Kodeksu cywilnego …” (cyfry indeksu 4 pt, podniesione),
  na tej samej stronie przypis „1) …” na dole i odnośnik „¹” po słowie w treści; Markdown zawiera
  „art. 66¹ § 1 do § 3 oraz art. 68²”, odnośnik po słowie jako `[^1]`, brak „661”, „682”
- [ ] T011 [US1] Wzorzec (green T009–T010) w `src/LegalAgent.PdfParser/Stages/TextNormalizationStage.cs`:
  `LegalNumberTail` = `(?:(?<!\p{L})[Aa]rt\.|§)\s*\d+[a-z]*[⁰¹²³⁴-⁹]*$`; zaktualizuj komentarz klasy („after art./Art. N
  / § N”). Kryteria rozmiaru i podniesienia bez zmian
- [ ] T012 [US1] Goldeny: uruchom pełny zestaw (FR-673) z `UPDATE_GOLDEN=1` dla `Corpus/acts` i
  `tests/LegalAgent.Chunking.Tests` (golden ochrona-danych); prywatny korpus: przejrzyj diff corp-5 (tylko „661”,
  „682” → „66¹”, „68²”) i zaktualizuj golden; sprawdź skryptem, że każda zmieniona linia w
  `tests/LegalAgent.PdfParser.Tests/Corpus/acts/*.expected.md` i w goldenie fragmentów mieści się w wąskiej zgodzie
  (oczekiwane: prawa-konsumenta 4, kredyt-konsumencki 1, prawo-bankowe 7, usługi-płatnicze 3 miejsca; ochrona-danych
  8); `Corpus/banking` i pozostałe prywatne goldeny bez zmian — inaczej stop. Bramka liczb: corp-5 bez nadmiarowych.
  Diff i liczby miejsc w handoffie

**Checkpoint**: US1 gotowe; bramka liczb czerwona już tylko przez corp-2 (stopka).

---

## Phase 5: User Story 3 — Stopka z danymi banku bez przeplecionych liter (Priority: P2)

**Goal**: cztery ciągłe wiersze stopki w kolejności PDF i osobny napis logo w corp-1, 2, 3, 5, 6.

**Independent Test**: replika ostatniej strony (4 wiersze 6,7 pt co 7 pt od x ≈ 175, logo 22,7 pt od x ≈ 40 na linii
bazowej ostatniego wiersza) bez przeplecionych liter; indeksy i odnośniki przy słowach bez zmian.

- [ ] T013 [P] [US3] Testy etapu (red) w `tests/LegalAgent.PdfParser.Tests/Unit/Stages/LineAssemblyStageTests.cs`:
  logo 22,7 pt + cztery wiersze 6,7 pt (geometria z research R3) → pięć wierszy: każdy wiersz stopki pełny
  („Gospodarczy …”, „podatkowej …”, „01.01.2022 r. wynosi …”), logo osobno; ochrona (zielone także przed zmianą):
  indeks „¹” 4 pt tuż po „66” w wierszu 8 pt, odnośnik „***)” i „123)” 5 pt tuż po słowie w wierszu 8 pt, nagłówek
  14 pt i tekst 7 pt na wspólnej linii bazowej oddzielone zwykłą spacją — wynik jak dotąd (jeden wiersz)
- [ ] T014 [P] [US3] Replika strony (red) w `tests/LegalAgent.PdfParser.Tests/Integration/CorpusLayoutsIntegrationTests.cs`:
  ostatnia strona z treścią i stopką jak w T013 (neutralne nazwy: „Bank.pl”, „Bank S.A., ul. Przykładowa 1 …”);
  Markdown zawiera cztery wiersze stopki jako ciągły tekst w kolejności PDF, żadne słowo nie jest przeplecione,
  napis logo jako osobny fragment
- [ ] T015 [US3] Warunek (green T013–T014) w `src/LegalAgent.PdfParser/Stages/LineAssemblyStage.cs` według
  `data-model.md`: przed `GroupIntoLines` wyznacz ciągi tekstu własnego (glify bez białych znaków, „jednego rozmiaru
  (± 0,1 pt), na wspólnej linii bazowej (`BaselineToleranceRatio` × rozmiar)”, przerwy ≤ `NearGlyphEm` × własny
  rozmiar, „ciąg ma ≥ 4 glify”, stała `OwnLineMinGlyphs = 4`); w `LineBuilder.Accepts` glif ciągu mniejszy niż
  `MarkerSizeRatio` × zarodek i dalej niż `NearGlyphEm` × mniejszy rozmiar od glifów wiersza → `false` przed obiema
  ścieżkami. Deterministycznie; `StepSequenceStage` (T040a, `RejoinGapFactor`) bez zmian
- [ ] T016 [US3] Pełny zestaw (FR-673) i decyzja FR-661: dopuszczalne różnice tylko w stopkach corp-1, 2, 3, 5, 6 i
  (bez pogorszenia miar) w schematach firm-2, 5, 6; `Corpus/acts`, `Corpus/banking`, goldeny fragmentów, korpus
  syntetyczny, ind-*, reg*, regulamin-pdp bez zmian; `CorpusFull` bez spadku. Spełnione → zaktualizuj prywatne
  goldeny dla firm, bramka liczb zielona, opis w handoffie. Niespełnione → T017
- [ ] T017 [US3] Tylko jeśli T016 wykazało ryzyko (FR-661): wycofaj T015 (testy T013/T014 oznacz `Skip` z odwołaniem
  do „Znanych ograniczeń”, ochrona zostaje aktywna), dopisz do `tests/LegalAgent.PdfParser.Tests/Corpus/private/number-check.txt`
  wyjątek corp-2 z liczbami przeplecionej daty i komentarzem, opisz zjawisko w `src/LegalAgent.PdfParser/README.md`
  („Znane ograniczenia”) i uzasadnienie w handoffie

**Checkpoint**: US3 gotowe (albo udokumentowane); bramka liczb zielona.

---

## Phase 6: User Story 4 — Bez regresji, miary i wydanie (Priority: P1) + Polish

**Goal**: spójne wydanie 1.2.1 z udokumentowanymi miarami i goldenami.

**Independent Test**: `quickstart.md` §1–§4 zielone; `verify` czysty; handoff kompletny.

- [ ] T018 [US4] `Directory.Build.props`: `<Version>` 1.2.0 → 1.2.1
- [ ] T019 [US4] `dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh`, potem `verify`; sprawdź diff
  `corpus/`: manifest (wersja 1.2.1) i `corpus/akty/*.md`, `*.chunks.jsonl` wyłącznie w wąskiej zgodzie (oczekiwane
  33 miejsca w 7 aktach); każda inna zmiana `corpus/` — stop i pytanie
- [ ] T020 [US4] Pełny zestaw: `dotnet test LegalAgent.slnx --filter "Category!=Performance"`, prywatny korpus z
  raportem, `LEGALAGENT_CORPUS_FULL=1` (`CorpusFull`), testy wydajności (`Category=Performance`, SC-007); porównanie
  zbiorów słów vs `baseline-009` — zmiany tylko w corp-5 i stopkach (SC-105)
- [ ] T021 [P] [US4] `src/LegalAgent.PdfParser/README.md`: indeks górny po „art.”, stopka z logo (US3), miara liczb
  i `number-check.txt` (testy); „Znane ograniczenia” (FR-676): tabela limitów firm-1 (4 stałe kolumny z komórkami
  wielowierszowymi na różnych liniach bazowych, TBL001, przeplatanie komórek), znaczniki przypisów w indeksie górnym
  przed liczbą („¹50.000” → „150.000”), podtytuł „obowiązują od …” jako nagłówek, wiersze strony tytułowej jako
  nagłówki („## Warszawa, Listopad 2020”, „## mBank.pl” w corp-2 i corp-6 — opisane bez nazw banków, jeśli README
  jest publiczne), niewykryty tytuł formularza (corp-4)
- [ ] T022 [P] [US4] `CLAUDE.md`: zmiany spec 009 (wzorzec „art.” w `TextNormalization`, ciągi tekstu własnego w
  `LineAssembly`, `NumberCheck` i `number-check.txt` w opisie zmiennych/testów prywatnego korpusu, `baseline-009`)
- [ ] T023 [US4] Handoff `specs/009-number-review-fixes/plan.md` („Stan prac i przekazanie”): tabela miar przed/po
  (liczby + / − dla każdego dokumentu z niezerową miarą, przeplecione wiersze 5 → 0), klasyfikacja liczb brakujących
  po zmianach (SC-101), lista zmienionych goldenów z
  uzasadnieniem (corp-5, stopki corp, akty w wąskiej zgodzie z liczbą miejsc), wynik researchu R3, decyzja T016/T017;
  zaktualizuj `tasks.md` (`[X]`)
- [ ] T024 [US4] Przebuduj `publish/` jak w spec 007/008 (polecenie z handoffu spec 008) i sprawdź, że exe raportuje
  wersję 1.2.1

---

## Dependencies & Execution Order

- **Setup (T001–T002)** → **US2 (T003–T008)** → **US1 (T009–T012)** → **US3 (T013–T017)** → **US4 (T018–T024)**.
- US2 przed US1/US3: bramka i pomiar wyjściowy potrzebne do red na prywatnym korpusie i do tabeli przed/po.
- US1 i US3 są od siebie niezależne w kodzie (inne etapy), ale oba aktualizują goldeny prywatne corp-5 — wykonuj
  sekwencyjnie, żeby diffy goldenów były jednoznaczne.
- T018 (wersja) przed T019 (`refresh` zapisuje wersję do manifestu).
- Wewnątrz historii: testy (red, osobny commit) → kod (green) → goldeny.

## Parallel Opportunities

- US1: T009 i T010 (różne pliki testów) równolegle, potem T011.
- US3: T013 i T014 równolegle, potem T015.
- US4: T021 i T022 (README, CLAUDE.md) równolegle po T020.

## Implementation Strategy

- **MVP**: Setup + US2 — trwała kontrola liczb z raportem i bramką (czerwoną, wskazującą corp-5 i corp-2).
- **Przyrost 1**: US1 — usuwa zmianę sensu w corp-5 i w aktach (najważniejsza poprawka treści).
- **Przyrost 2**: US3 — kosmetyka stopek z kryterium wycofania.
- **Zamknięcie**: US4 — wersja, korpus, dokumentacja, publish/. Co nie zdąży, trafia do handoffu jako następny krok.
